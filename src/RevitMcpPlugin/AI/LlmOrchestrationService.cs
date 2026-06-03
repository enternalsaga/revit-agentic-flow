using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace RevitMcpPlugin.AI;

/// <summary>
/// LLM Orchestration Service — provider-agnostic tool-use loop with streaming chat.
///
/// Stripped from bibim's 710-line version: no Roslyn compile/retry, no code extraction,
/// no planner, no history summariser, no debug recorder. Pure orchestration.
/// </summary>
public class LlmOrchestrationService
{
    private static readonly HttpClient _httpClient = CreateHttpClient();

    private readonly ILlmProvider _provider;

    public string ProviderName => _provider.ProviderName;
    public string ModelId => _provider.ModelId;

    public event Action<string> OnStreamingDelta;
    public event Action<string> OnStatusUpdate;
    public event Action<TokenUsageInfo> OnTokenUsage;

    public LlmOrchestrationService(ILlmProvider provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    /// <summary>
    /// Factory: resolve the active model from AiConfig and create an orchestration service.
    /// Returns null if no matching model is found in any provider.
    /// </summary>
    public static LlmOrchestrationService? CreateFromConfig(AiConfig config)
    {
        var configService = new AiConfigService();
        var resolved = configService.ResolveActiveModel(config);
        if (resolved == null) return null;
        var (provider, modelId) = resolved.Value;
        return new LlmOrchestrationService(LlmProviderFactory.Create(provider, modelId));
    }

    /// <summary>
    /// Send a streaming chat message without tools.
    /// </summary>
    public async Task<LlmResponse> SendMessageAsync(
        JArray messages,
        string systemPrompt,
        CancellationToken ct = default,
        int maxTokens = 8192)
    {
        var requestId = Guid.NewGuid().ToString("N").Substring(0, 8);
        var sw = Stopwatch.StartNew();
        var response = new LlmResponse { RequestId = requestId };

        try
        {
            OnStatusUpdate?.Invoke("Generating response...");

            var stream = await _provider.SendStreamingAsync(
                messages, systemPrompt, delta => OnStreamingDelta?.Invoke(delta), ct, maxTokens);

            response.Text = stream.FullText;
            response.InputTokens = stream.InputTokens;
            response.OutputTokens = stream.OutputTokens;
            response.CachedInputTokens = stream.CachedInputTokens;
            response.CacheCreationInputTokens = stream.CacheCreationInputTokens;
            response.Success = true;

            TokenTracker.Track("streaming", _provider.ProviderName, _provider.ModelId,
                stream.InputTokens, stream.OutputTokens, requestId,
                stream.CachedInputTokens, stream.CacheCreationInputTokens);

            OnTokenUsage?.Invoke(new TokenUsageInfo
            {
                RequestId = requestId,
                Model = _provider.ModelId,
                InputTokens = stream.InputTokens,
                OutputTokens = stream.OutputTokens,
                CachedInputTokens = stream.CachedInputTokens,
                CacheCreationInputTokens = stream.CacheCreationInputTokens,
                ElapsedMs = sw.ElapsedMilliseconds
            });

            Debug.WriteLine($"[LlmOrchestration] rid={requestId} provider={_provider.ProviderName} " +
                $"model={_provider.ModelId} in={stream.InputTokens} out={stream.OutputTokens} " +
                $"cache_read={stream.CachedInputTokens} cache_create={stream.CacheCreationInputTokens} " +
                $"ms={sw.ElapsedMilliseconds}");
        }
        catch (OperationCanceledException)
        {
            response.Success = false;
            response.ErrorMessage = "Request cancelled.";
            throw;
        }
        catch (Exception ex)
        {
            response.Success = false;
            response.ErrorMessage = ex.Message;
            response.IsContextLengthExceeded = IsContextLengthError(ex.Message);
            Debug.WriteLine($"[LlmOrchestration] SendMessage error: {ex.Message}");
        }
        finally
        {
            OnStatusUpdate?.Invoke("");
        }

        return response;
    }

    /// <summary>
    /// Agent tool-use loop. Provider-agnostic — works with any ILlmProvider.
    /// Loops: LLM response -> tool_use -> execute -> tool_result -> LLM (max iterations).
    /// </summary>
    public async Task<LlmResponse> GenerateWithToolsAsync(
        JArray messages,
        string systemPrompt,
        JArray toolDefinitions,
        Func<string, string, CancellationToken, Task<string>> toolExecutor,
        int maxTurns = 10,
        CancellationToken ct = default)
    {
        var requestId = Guid.NewGuid().ToString("N").Substring(0, 8);
        var result = new LlmResponse { RequestId = requestId };

        try
        {
            for (int turn = 0; turn < maxTurns; turn++)
            {
                ct.ThrowIfCancellationRequested();

                OnStatusUpdate?.Invoke(turn == 0 ? "Thinking..." : "Using tools...");
                Debug.WriteLine($"[LlmOrchestration] rid={requestId} provider={_provider.ProviderName} " +
                    $"model={_provider.ModelId} tool-turn={turn}");

                JObject response;
                try
                {
                    response = await _provider.SendNonStreamingAsync(
                        messages, systemPrompt, toolDefinitions, ct, 8192);
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.ErrorMessage = $"API request failed: {ex.Message}";
                    result.IsContextLengthExceeded = IsContextLengthError(ex.Message);
                    Debug.WriteLine($"[LlmOrchestration] API error: {ex.Message}");
                    return result;
                }

                int inTok = response["usage"]?["input_tokens"]?.Value<int>() ?? 0;
                int outTok = response["usage"]?["output_tokens"]?.Value<int>() ?? 0;
                int cachedTok = response["usage"]?["cache_read_input_tokens"]?.Value<int>() ?? 0;
                int cacheCreateTok = response["usage"]?["cache_creation_input_tokens"]?.Value<int>() ?? 0;

                result.InputTokens += inTok;
                result.OutputTokens += outTok;
                result.CachedInputTokens += cachedTok;
                result.CacheCreationInputTokens += cacheCreateTok;

                TokenTracker.Track("tool-loop", _provider.ProviderName, _provider.ModelId,
                    inTok, outTok, requestId, cachedTok, cacheCreateTok);

                OnTokenUsage?.Invoke(new TokenUsageInfo
                {
                    RequestId = requestId,
                    Model = _provider.ModelId,
                    InputTokens = inTok,
                    OutputTokens = outTok,
                    CachedInputTokens = cachedTok,
                    CacheCreationInputTokens = cacheCreateTok,
                    ElapsedMs = 0
                });

                string stopReason = response["stop_reason"]?.ToString();
                var content = response["content"] as JArray ?? new JArray();

                // BIBIM-007: defensive tool_use coercion.
                // Providers may emit a tool_use block then truncate trailing text,
                // reporting a wrong stop_reason. If content has tool_use, force that branch
                // to ensure correct tool_use / tool_result pairing.
                bool hasToolUse = false;
                foreach (var block in content)
                {
                    if (block is JObject blockObj &&
                        blockObj["type"]?.ToString() == "tool_use")
                    {
                        hasToolUse = true;
                        break;
                    }
                }
                if (hasToolUse && stopReason != "tool_use")
                {
                    Debug.WriteLine($"[LlmOrchestration] rid={requestId} turn={turn} " +
                        $"provider reported stop_reason={stopReason} but content has tool_use blocks; coercing");
                    stopReason = "tool_use";
                }

                // end_turn / max_tokens: model finished or was truncated
                if (stopReason == "end_turn" || stopReason == "max_tokens")
                {
                    if (stopReason == "max_tokens")
                        Debug.WriteLine($"[LlmOrchestration] rid={requestId} turn={turn} response truncated by max_tokens");

                    string finalText = ExtractTextFromContent(content);

                    // max_tokens truncation mid-generation — request continuation
                    if (stopReason == "max_tokens" && string.IsNullOrWhiteSpace(finalText) && turn < maxTurns - 1)
                    {
                        messages.Add(new JObject { ["role"] = "assistant", ["content"] = content });
                        messages.Add(new JObject
                        {
                            ["role"] = "user",
                            ["content"] = "[CONTINUATION_REQUIRED] Your response was cut off. " +
                                "Continue EXACTLY where you left off."
                        });
                        continue;
                    }

                    result.Text = finalText;
                    result.Success = true;
                    return result;
                }

                // tool_use: execute each tool, feed results back
                if (stopReason == "tool_use")
                {
                    messages.Add(new JObject
                    {
                        ["role"] = "assistant",
                        ["content"] = content
                    });

                    var toolResults = new JArray();
                    foreach (JObject block in content)
                    {
                        if (block["type"]?.ToString() != "tool_use") continue;

                        string toolId = block["id"]?.ToString();
                        string toolName = block["name"]?.ToString();
                        string toolInput = block["input"]?.ToString(Newtonsoft.Json.Formatting.None) ?? "{}";

                        if (string.IsNullOrEmpty(toolId) || string.IsNullOrEmpty(toolName))
                        {
                            Debug.WriteLine($"[LlmOrchestration] rid={requestId} " +
                                $"malformed tool_use block: id={toolId ?? "null"}, name={toolName ?? "null"} — skipping");
                            continue;
                        }

                        OnStatusUpdate?.Invoke($"Using {toolName}...");
                        Debug.WriteLine($"[LlmOrchestration] rid={requestId} tool={toolName}");

                        string toolOutput;
                        try
                        {
                            toolOutput = await toolExecutor(toolName, toolInput, ct);
                        }
                        catch (Exception ex)
                        {
                            toolOutput = $"[Tool Error] {ex.Message}";
                            Debug.WriteLine($"[LlmOrchestration] tool.{toolName} error: {ex.Message}");
                        }

                        toolResults.Add(new JObject
                        {
                            ["type"] = "tool_result",
                            ["tool_use_id"] = toolId,
                            ["content"] = toolOutput
                        });
                    }

                    if (toolResults.Count == 0)
                    {
                        Debug.WriteLine($"[LlmOrchestration] rid={requestId} turn={turn} " +
                            "stop_reason=tool_use but no valid tool blocks found");
                        result.Success = false;
                        result.ErrorMessage = "Provider returned tool_use stop reason but no valid tool blocks were found.";
                        return result;
                    }

                    messages.Add(new JObject
                    {
                        ["role"] = "user",
                        ["content"] = toolResults
                    });

                    continue;
                }

                // Unexpected stop reason
                result.Success = false;
                result.ErrorMessage = $"Unexpected stop_reason: {stopReason}";
                return result;
            }

            result.Success = false;
            result.ErrorMessage = $"Tool loop exceeded max_turns ({maxTurns}).";
        }
        catch (OperationCanceledException)
        {
            result.Success = false;
            result.ErrorMessage = "Cancelled.";
            throw;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.IsContextLengthExceeded = IsContextLengthError(ex.Message);
            Debug.WriteLine($"[LlmOrchestration] GenerateWithTools error: {ex.Message}");
        }
        finally
        {
            OnStatusUpdate?.Invoke("");
        }

        return result;
    }

    // ───────────────────────────── helpers ─────────────────────────────

    /// <summary>
    /// Build a messages JArray from a simple history of (role, text) pairs.
    /// Kept for callers that still use List&lt;ChatMessage&gt; or similar.
    /// </summary>
    public static JArray BuildMessagesArray(IEnumerable<(string role, string text)> history)
    {
        var arr = new JArray();
        if (history == null) return arr;
        foreach (var (role, text) in history)
        {
            arr.Add(new JObject
            {
                ["role"] = role,
                ["content"] = text ?? string.Empty
            });
        }
        return arr;
    }

    private static string ExtractTextFromContent(JArray content)
    {
        var sb = new StringBuilder();
        if (content == null) return string.Empty;
        foreach (JObject block in content)
        {
            if (block["type"]?.ToString() == "text")
                sb.Append(block["text"]?.ToString());
        }
        return sb.ToString();
    }

    private static bool IsContextLengthError(string message)
    {
        if (string.IsNullOrEmpty(message)) return false;
        return message.IndexOf("prompt is too long", StringComparison.OrdinalIgnoreCase) >= 0 ||
               message.IndexOf("context_length_exceeded", StringComparison.OrdinalIgnoreCase) >= 0 ||
               message.IndexOf("maximum context length", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static HttpClient CreateHttpClient()
    {
        return new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(5)
        };
    }
}

// ───────────────────────────── result types ─────────────────────────────

public class LlmResponse
{
    public string RequestId { get; set; } = "";
    public bool Success { get; set; }
    public string Text { get; set; } = "";
    public string ErrorMessage { get; set; } = "";
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int CachedInputTokens { get; set; }
    public int CacheCreationInputTokens { get; set; }
    public bool IsContextLengthExceeded { get; set; }
}

public class TokenUsageInfo
{
    public string RequestId { get; set; } = "";
    public string Model { get; set; } = "";
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int CachedInputTokens { get; set; }
    public int CacheCreationInputTokens { get; set; }
    public long ElapsedMs { get; set; }
}
