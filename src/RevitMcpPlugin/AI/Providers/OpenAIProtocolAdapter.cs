using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RevitMcpPlugin.AI
{
    /// <summary>
    /// OpenAI Chat Completions protocol adapter.
    /// Translates Anthropic-shaped messages/tools to/from OpenAI format.
    /// Works with OpenAI, Gemini (openai-compatible endpoint), and any
    /// other provider that exposes /v1/chat/completions.
    /// </summary>
    public class OpenAIProtocolAdapter : ILlmProvider
    {
        private readonly string _apiKey;
        private readonly string _modelId;
        private readonly string _endpoint;
        private readonly HttpClient _httpClient;
        private readonly Dictionary<string, string>? _customHeaders;
        private readonly bool _isGeminiAuth;

        public string ProviderName => "openai";
        public string ModelId => _modelId;

        public OpenAIProtocolAdapter(ProviderConfig config, string modelId, HttpClient httpClient)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));
            if (string.IsNullOrEmpty(config.ApiKey))
                throw new ArgumentException("OpenAI API key is required.", nameof(config));
            if (string.IsNullOrEmpty(modelId))
                throw new ArgumentException("Model id is required.", nameof(modelId));
            _apiKey = config.ApiKey;
            _modelId = modelId;
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _customHeaders = config.Headers;
            _isGeminiAuth = config.BaseUrl?.IndexOf("generativelanguage.googleapis.com", StringComparison.OrdinalIgnoreCase) >= 0 == true;
            _endpoint = BuildEndpoint(config.BaseUrl ?? "", _isGeminiAuth ? _apiKey : null);
        }

        private static string BuildEndpoint(string baseUrl, string? geminiApiKey = null)
        {
            if (string.IsNullOrEmpty(baseUrl))
                return "https://api.openai.com/v1/chat/completions";

            var trimmed = baseUrl.TrimEnd('/');
            if (trimmed.EndsWith("/v1/chat/completions", StringComparison.OrdinalIgnoreCase))
            {
                if (geminiApiKey != null)
                    return $"{trimmed}?key={geminiApiKey}";
                return trimmed;
            }

            var endpoint = $"{trimmed}/v1/chat/completions";
            if (geminiApiKey != null)
                endpoint += $"?key={geminiApiKey}";
            return endpoint;
        }

        public async Task<JObject> SendNonStreamingAsync(
            JArray messages, string systemPrompt, JArray tools,
            CancellationToken ct, int maxTokens, bool jsonMode = false)
        {
            var openAiMessages = TranslateMessagesToOpenAI(messages, systemPrompt);

            var requestBody = new JObject
            {
                ["model"] = _modelId,
                ["max_tokens"] = maxTokens,
                ["messages"] = openAiMessages
            };

            if (jsonMode)
                requestBody["response_format"] = new JObject { ["type"] = "json_object" };

            if (tools != null && tools.Count > 0)
                requestBody["tools"] = TranslateToolsToOpenAI(tools);

            using (var request = new HttpRequestMessage(HttpMethod.Post, _endpoint))
            {
                request.Content = new StringContent(
                    requestBody.ToString(Formatting.None), Encoding.UTF8, "application/json");
                if (!_isGeminiAuth)
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
                AddCustomHeaders(request);

                using (var response = await _httpClient.SendAsync(request, ct))
                {
                    string body = await response.Content.ReadAsStringAsync();
                    if (!response.IsSuccessStatusCode)
                        throw new HttpRequestException($"OpenAI API {(int)response.StatusCode}: {body}");

                    var openAiResponse = JObject.Parse(body);
                    return TranslateResponseToAnthropic(openAiResponse);
                }
            }
        }

        public async Task<StreamResult> SendStreamingAsync(
            JArray messages, string systemPrompt, Action<string> onTextDelta,
            CancellationToken ct, int maxTokens)
        {
            var openAiMessages = TranslateMessagesToOpenAI(messages, systemPrompt);

            var requestBody = new JObject
            {
                ["model"] = _modelId,
                ["max_tokens"] = maxTokens,
                ["messages"] = openAiMessages,
                ["stream"] = true
            };

            // Some providers require stream_options to emit usage in chunks
            requestBody["stream_options"] = new JObject { ["include_usage"] = true };

            using (var request = new HttpRequestMessage(HttpMethod.Post, _endpoint))
            {
                request.Content = new StringContent(
                    requestBody.ToString(Formatting.None), Encoding.UTF8, "application/json");
                if (!_isGeminiAuth)
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
                AddCustomHeaders(request);

                using (var httpResponse = await _httpClient.SendAsync(
                    request, HttpCompletionOption.ResponseHeadersRead, ct))
                {
                    if (!httpResponse.IsSuccessStatusCode)
                    {
                        string error = await httpResponse.Content.ReadAsStringAsync();
                        throw new HttpRequestException($"OpenAI API {(int)httpResponse.StatusCode}: {error}");
                    }

                    var fullText = new StringBuilder();
                    var toolCalls = new List<JObject>();
                    int inputTokens = 0, outputTokens = 0;
                    var toolCallBuffers = new Dictionary<int, StringBuilder>();

                    using (var stream = await httpResponse.Content.ReadAsStreamAsync())
                    using (var reader = new StreamReader(stream))
                    {
                        string? line;
                        while ((line = await reader.ReadLineAsync()) != null)
                        {
                            if (!line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                                continue;

                            string data = line.Substring("data:".Length).TrimStart();
                            if (data == "[DONE]")
                            {
                                FinalizeAllToolCalls(toolCalls, toolCallBuffers);
                                break;
                            }

                            if (string.IsNullOrWhiteSpace(data))
                                continue;

                            try
                            {
                                ProcessSseChunk(data, fullText, toolCalls, toolCallBuffers,
                                    onTextDelta, ref inputTokens, ref outputTokens);
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine(
                                    $"[OpenAIProtocolAdapter] SSE parse skipped: {ex.Message}");
                            }
                        }
                    }

                    return new StreamResult
                    {
                        FullText = fullText.ToString(),
                        InputTokens = inputTokens,
                        OutputTokens = outputTokens,
                        ToolUseBlocks = toolCalls.Count > 0 ? new JArray(toolCalls) : null
                    };
                }
            }
        }

        // ─── Translation: Anthropic messages → OpenAI messages ────────────────

        private static JArray TranslateMessagesToOpenAI(JArray? messages, string? systemPrompt)
        {
            var result = new JArray();

            // System prompt becomes a system role message
            if (!string.IsNullOrWhiteSpace(systemPrompt))
            {
                result.Add(new JObject
                {
                    ["role"] = "system",
                    ["content"] = systemPrompt
                });
            }

            if (messages == null || messages.Count == 0)
                return result;

            // Flush any pending tool messages after processing all messages
            var pendingToolMessages = new List<JObject>();

            foreach (JToken msgToken in messages)
            {
                if (!(msgToken is JObject msg)) continue;
                string role = msg["role"]?.ToString() ?? "user";

                if (role == "assistant")
                {
                    var openAiMsg = TranslateAssistantMessage(msg);
                    result.Add(openAiMsg);
                }
                else if (role == "user")
                {
                    var toolResults = TranslateUserMessage(msg, pendingToolMessages);
                    // Flush pending tool messages before the user message
                    foreach (var tm in pendingToolMessages)
                        result.Add(tm);
                    pendingToolMessages.Clear();

                    result.Add(toolResults);
                }
                else
                {
                    // Unknown role, pass through as-is
                    result.Add(msg.DeepClone());
                }
            }

            // Flush any remaining pending tool messages
            foreach (var tm in pendingToolMessages)
                result.Add(tm);

            return result;
        }

        /// <summary>
        /// Translates an Anthropic assistant message to OpenAI format.
        /// Anthropic: content array with text + tool_use blocks
        /// OpenAI: content string + top-level tool_calls array
        /// </summary>
        private static JObject TranslateAssistantMessage(JObject msg)
        {
            var content = msg["content"];
            var textParts = new StringBuilder();
            var toolCalls = new JArray();

            if (content is JArray contentArray)
            {
                foreach (JToken block in contentArray)
                {
                    if (!(block is JObject b)) continue;
                    string type = b["type"]?.ToString() ?? "";

                    if (type == "text")
                    {
                        if (textParts.Length > 0) textParts.Append("\n");
                        textParts.Append(b["text"]?.ToString() ?? "");
                    }
                    else if (type == "tool_use")
                    {
                        string toolId = b["id"]?.ToString() ?? "";
                        string toolName = b["name"]?.ToString() ?? "";
                        JToken input = b["input"] ?? new JObject();

                        toolCalls.Add(new JObject
                        {
                            ["id"] = toolId,
                            ["type"] = "function",
                            ["function"] = new JObject
                            {
                                ["name"] = toolName,
                                ["arguments"] = input.ToString(Formatting.None)
                            }
                        });
                    }
                }
            }
            else if (content != null)
            {
                textParts.Append(content.ToString());
            }

            var result = new JObject
            {
                ["role"] = "assistant",
                ["content"] = textParts.Length > 0 ? textParts.ToString() : null
            };

            if (toolCalls.Count > 0)
                result["tool_calls"] = toolCalls;

            return result;
        }

        /// <summary>
        /// Translates an Anthropic user message to OpenAI format.
        /// Extracts tool_result blocks as separate OpenAI "tool" role messages.
        /// Returns the user message (non-tool content) and appends tool messages to pendingToolMessages.
        /// </summary>
        private static JObject TranslateUserMessage(JObject msg, List<JObject> pendingToolMessages)
        {
            var content = msg["content"];
            var userParts = new JArray();
            bool hasToolResults = false;

            if (content != null && content.Type == JTokenType.String)
            {
                // Simple string content — just pass through
                return new JObject
                {
                    ["role"] = "user",
                    ["content"] = content.ToString()
                };
            }

            if (content is JArray contentArray)
            {
                foreach (JToken block in contentArray)
                {
                    if (!(block is JObject b)) continue;
                    string type = b["type"]?.ToString() ?? "";

                    if (type == "tool_result")
                    {
                        hasToolResults = true;
                        string toolUseId = b["tool_use_id"]?.ToString() ?? "";
                        string resultContent = b["content"]?.ToString() ?? "";

                        pendingToolMessages.Add(new JObject
                        {
                            ["role"] = "tool",
                            ["tool_call_id"] = toolUseId,
                            ["content"] = resultContent
                        });
                    }
                    else
                    {
                        // text, image, or other blocks — keep in user content
                        userParts.Add(b.DeepClone());
                    }
                }
            }
            else if (content != null)
            {
                return new JObject
                {
                    ["role"] = "user",
                    ["content"] = content.ToString()
                };
            }

            if (userParts.Count == 0 && hasToolResults)
            {
                // User message had only tool_result blocks; OpenAI needs at least
                // some content in the user message before tool results. Emit a
                // minimal placeholder so the conversation alternation stays valid.
                return new JObject
                {
                    ["role"] = "user",
                    ["content"] = "tool_results"
                };
            }

            if (userParts.Count == 1 && userParts[0] is JObject singleBlock
                && singleBlock["type"]?.ToString() == "text")
            {
                // Single text block → flatten to string content
                return new JObject
                {
                    ["role"] = "user",
                    ["content"] = singleBlock["text"]?.ToString() ?? ""
                };
            }

            return new JObject
            {
                ["role"] = "user",
                ["content"] = userParts
            };
        }

        // ─── Translation: Anthropic tools → OpenAI tools ────────────────────

        private static JArray TranslateToolsToOpenAI(JArray anthropicTools)
        {
            var openAiTools = new JArray();
            foreach (JObject tool in anthropicTools)
            {
                openAiTools.Add(new JObject
                {
                    ["type"] = "function",
                    ["function"] = new JObject
                    {
                        ["name"] = tool["name"]?.ToString(),
                        ["description"] = tool["description"]?.ToString(),
                        ["parameters"] = tool["input_schema"] ?? new JObject()
                    }
                });
            }
            return openAiTools;
        }

        // ─── Translation: OpenAI response → Anthropic response ────────────────

        private static JObject TranslateResponseToAnthropic(JObject openAiResponse)
        {
            var content = new JArray();
            string? stopReason = "end_turn";
            int inputTokens = 0, outputTokens = 0;

            // Usage
            var usage = openAiResponse["usage"];
            if (usage != null)
            {
                inputTokens = usage["prompt_tokens"]?.Value<int>() ?? 0;
                outputTokens = usage["completion_tokens"]?.Value<int>() ?? 0;
            }

            var choice = openAiResponse["choices"]?[0];
            if (choice != null)
            {
                var message = choice["message"];
                if (message != null)
                {
                    // Text content
                    string? textContent = message["content"]?.ToString();
                    if (!string.IsNullOrEmpty(textContent))
                    {
                        content.Add(new JObject
                        {
                            ["type"] = "text",
                            ["text"] = textContent
                        });
                    }

                    // Tool calls
                    var toolCalls = message["tool_calls"] as JArray;
                    if (toolCalls != null && toolCalls.Count > 0)
                    {
                        stopReason = "tool_use";
                        foreach (JToken tc in toolCalls)
                        {
                            string id = tc["id"]?.ToString() ?? "";
                            string name = tc["function"]?["name"]?.ToString() ?? "";
                            string argumentsJson = tc["function"]?["arguments"]?.ToString() ?? "{}";

                            JToken input;
                            try
                            {
                                input = JObject.Parse(argumentsJson);
                            }
                            catch
                            {
                                input = new JObject();
                            }

                            content.Add(new JObject
                            {
                                ["type"] = "tool_use",
                                ["id"] = id,
                                ["name"] = name,
                                ["input"] = input
                            });
                        }
                    }

                    // Finish reason → stop_reason
                    string? finishReason = choice["finish_reason"]?.ToString();
                    if (finishReason == "tool_calls")
                        stopReason = "tool_use";
                    else if (finishReason == "stop")
                        stopReason = "end_turn";
                    else if (finishReason == "length")
                        stopReason = "max_tokens";
                }
            }

            return new JObject
            {
                ["content"] = content,
                ["stop_reason"] = stopReason,
                ["usage"] = new JObject
                {
                    ["input_tokens"] = inputTokens,
                    ["output_tokens"] = outputTokens
                }
            };
        }

        // ─── Streaming SSE chunk processing ───────────────────────────────────

        private static void ProcessSseChunk(
            string data, StringBuilder fullText, List<JObject> toolCalls,
            Dictionary<int, StringBuilder> toolCallBuffers,
            Action<string> onTextDelta, ref int inputTokens, ref int outputTokens)
        {
            var payload = JObject.Parse(data);
            var choices = payload["choices"];

            // Usage-only chunk (stream_options.include_usage)
            if (choices == null || !choices.HasValues)
            {
                var usage = payload["usage"];
                if (usage != null)
                {
                    inputTokens = usage["prompt_tokens"]?.Value<int>() ?? inputTokens;
                    outputTokens = usage["completion_tokens"]?.Value<int>() ?? outputTokens;
                }
                return;
            }

            var choice = choices[0];
            if (choice == null) return;

            var delta = choice["delta"];
            if (delta == null || delta.Type == JTokenType.Null)
            {
                // Null delta can carry usage in some providers
                var chunkUsage = payload["usage"];
                if (chunkUsage != null)
                {
                    inputTokens = chunkUsage["prompt_tokens"]?.Value<int>() ?? inputTokens;
                    outputTokens = chunkUsage["completion_tokens"]?.Value<int>() ?? outputTokens;
                }
                return;
            }

            // Text delta
            string? contentDelta = delta["content"]?.ToString();
            if (!string.IsNullOrEmpty(contentDelta))
            {
                fullText.Append(contentDelta);
                onTextDelta?.Invoke(contentDelta!);
            }

            // Tool call delta
            var toolCallDeltas = delta["tool_calls"] as JArray;
            if (toolCallDeltas != null)
            {
                foreach (JToken tcDelta in toolCallDeltas)
                {
                    int index = tcDelta["index"]?.Value<int>() ?? 0;
                    string? id = tcDelta["id"]?.ToString();
                    string? funcName = tcDelta["function"]?["name"]?.ToString();
                    string? arguments = tcDelta["function"]?["arguments"]?.ToString();

                    if (!toolCallBuffers.TryGetValue(index, out var buffer))
                    {
                        buffer = new StringBuilder();
                        toolCallBuffers[index] = buffer;
                    }

                    if (!string.IsNullOrEmpty(id) || !string.IsNullOrEmpty(funcName))
                    {
                        // First chunk for this tool call — store id and name
                        if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(funcName))
                        {
                            toolCalls.Add(new JObject
                            {
                                ["type"] = "tool_use",
                                ["id"] = id,
                                ["name"] = funcName,
                                ["input"] = new JObject()
                            });
                        }
                    }

                    if (!string.IsNullOrEmpty(arguments))
                        buffer.Append(arguments);

                    // Finalize tool call input when we see the next index or finish
                    FinalizeCompletedToolCalls(toolCalls, toolCallBuffers, index);
                }
            }

            // Finalize any remaining tool calls on this chunk
            // (the [DONE] sentinel outside the loop handles the last one)
        }

        private static void FinalizeCompletedToolCalls(
            List<JObject> toolCalls, Dictionary<int, StringBuilder> toolCallBuffers, int currentIndex)
        {
            // Finalize buffers for indices strictly less than current
            var keysToRemove = new List<int>();
            foreach (var kvp in toolCallBuffers)
            {
                if (kvp.Key < currentIndex && kvp.Value.Length > 0)
                {
                    FinalizeToolCall(toolCalls, toolCallBuffers, kvp.Key, kvp.Value);
                    keysToRemove.Add(kvp.Key);
                }
            }
            foreach (var k in keysToRemove)
                toolCallBuffers.Remove(k);
        }

        private static void FinalizeAllToolCalls(
            List<JObject> toolCalls, Dictionary<int, StringBuilder> toolCallBuffers)
        {
            foreach (var kvp in toolCallBuffers)
            {
                if (kvp.Value.Length > 0)
                    FinalizeToolCall(toolCalls, toolCallBuffers, kvp.Key, kvp.Value);
            }
            toolCallBuffers.Clear();
        }

        private static void FinalizeToolCall(
            List<JObject> toolCalls, Dictionary<int, StringBuilder> toolCallBuffers,
            int index, StringBuilder buffer)
        {
            string argsJson = buffer.ToString();
            try
            {
                var parsed = JObject.Parse(argsJson);
                if (index < toolCalls.Count)
                    toolCalls[index]["input"] = parsed;
            }
            catch
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[OpenAIProtocolAdapter] Failed to parse tool call arguments for index {index}");
            }
        }

        private void AddCustomHeaders(HttpRequestMessage request)
        {
            if (_customHeaders == null) return;
            foreach (var header in _customHeaders)
            {
                request.Headers.Add(header.Key, header.Value);
            }
        }
    }
}
