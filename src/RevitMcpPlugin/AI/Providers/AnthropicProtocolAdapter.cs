using System;
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
    public class AnthropicProtocolAdapter : ILlmProvider
    {
        private readonly string _apiKey;
        private readonly string _modelId;
        private readonly string _endpoint;
        private readonly HttpClient _httpClient;
        private readonly Dictionary<string, string>? _customHeaders;

        public string ProviderName => "anthropic";
        public string ModelId => _modelId;

        public AnthropicProtocolAdapter(ProviderConfig config, string modelId, HttpClient httpClient)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));
            if (string.IsNullOrEmpty(config.ApiKey))
                throw new ArgumentException("Anthropic API key is required.", nameof(config));
            if (string.IsNullOrEmpty(modelId))
                throw new ArgumentException("Model id is required.", nameof(modelId));
            _apiKey = config.ApiKey;
            _modelId = modelId;
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _customHeaders = config.Headers;
            _endpoint = BuildEndpoint(config.BaseUrl);
        }

        private static string BuildEndpoint(string baseUrl)
        {
            if (string.IsNullOrEmpty(baseUrl))
                return "https://api.anthropic.com/v1/messages";

            var trimmed = baseUrl.TrimEnd('/');
            if (trimmed.EndsWith("/v1/messages", StringComparison.OrdinalIgnoreCase))
                return trimmed;

            return $"{trimmed}/v1/messages";
        }

        public async Task<JObject> SendNonStreamingAsync(
            JArray messages, string systemPrompt, JArray tools,
            CancellationToken ct, int maxTokens, bool jsonMode = false)
        {
            var requestBody = new JObject
            {
                ["model"] = _modelId,
                ["max_tokens"] = maxTokens,
                ["system"] = new JArray
                {
                    new JObject
                    {
                        ["type"] = "text",
                        ["text"] = systemPrompt ?? string.Empty,
                        ["cache_control"] = new JObject { ["type"] = "ephemeral" }
                    }
                },
                ["messages"] = messages ?? new JArray()
            };
            if (tools != null && tools.Count > 0)
                requestBody["tools"] = MarkLastToolForCaching(tools);

            // Strip 'name' from tool_result blocks (Anthropic rejects unknown fields)
            var messagesArray = requestBody["messages"] as JArray ?? new JArray();
            foreach (JToken msgToken in messagesArray)
            {
                if (!(msgToken is JObject msg)) continue;
                if (!(msg["content"] is JArray contentArr)) continue;
                foreach (JToken blockToken in contentArr)
                {
                    if (!(blockToken is JObject block)) continue;
                    if (block["type"]?.ToString() == "tool_result")
                        block.Remove("name");
                }
            }

            using (var request = new HttpRequestMessage(HttpMethod.Post, _endpoint))
            {
                request.Content = new StringContent(
                    requestBody.ToString(Formatting.None), Encoding.UTF8, "application/json");
                request.Headers.Add("x-api-key", _apiKey);
                request.Headers.Add("anthropic-version", "2023-06-01");
                request.Headers.Add("anthropic-beta", "prompt-caching-2024-07-31");
                AddCustomHeaders(request);

                using (var response = await _httpClient.SendAsync(request, ct))
                {
                    string body = await response.Content.ReadAsStringAsync();
                    if (!response.IsSuccessStatusCode)
                        throw new HttpRequestException($"Anthropic API {(int)response.StatusCode}: {body}");
                    return JObject.Parse(body);
                }
            }
        }

        public async Task<StreamResult> SendStreamingAsync(
            JArray messages, string systemPrompt, Action<string> onTextDelta,
            CancellationToken ct, int maxTokens)
        {
            var requestBody = new JObject
            {
                ["model"] = _modelId,
                ["max_tokens"] = maxTokens,
                ["system"] = new JArray
                {
                    new JObject
                    {
                        ["type"] = "text",
                        ["text"] = systemPrompt ?? string.Empty,
                        ["cache_control"] = new JObject { ["type"] = "ephemeral" }
                    }
                },
                ["stream"] = true,
                ["messages"] = messages ?? new JArray()
            };

            using (var request = new HttpRequestMessage(HttpMethod.Post, _endpoint))
            {
                request.Content = new StringContent(
                    requestBody.ToString(Formatting.None), Encoding.UTF8, "application/json");
                request.Headers.Add("x-api-key", _apiKey);
                request.Headers.Add("anthropic-version", "2023-06-01");
                request.Headers.Add("anthropic-beta", "prompt-caching-2024-07-31");
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
                AddCustomHeaders(request);

                using (var httpResponse = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct))
                {
                    if (!httpResponse.IsSuccessStatusCode)
                    {
                        string error = await httpResponse.Content.ReadAsStringAsync();
                        throw new HttpRequestException($"Anthropic API {(int)httpResponse.StatusCode}: {error}");
                    }

                    var fullText = new StringBuilder();
                    int inputTokens = 0, outputTokens = 0, cachedTokens = 0, cacheCreationTokens = 0;

                    using (var stream = await httpResponse.Content.ReadAsStreamAsync())
                    using (var reader = new StreamReader(stream))
                    {
                        string? currentEvent = null;
                        var currentData = new StringBuilder();
                        string? line;
                        while ((line = await reader.ReadLineAsync()) != null)
                        {
                            if (line.Length == 0)
                            {
                                ProcessSseEvent(currentEvent, currentData.ToString(), fullText, onTextDelta,
                                    ref inputTokens, ref outputTokens, ref cachedTokens, ref cacheCreationTokens);
                                currentEvent = null;
                                currentData.Clear();
                                continue;
                            }
                            if (line.StartsWith("event:", StringComparison.OrdinalIgnoreCase))
                            {
                                currentEvent = line.Substring("event:".Length).Trim();
                                continue;
                            }
                            if (line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                            {
                                if (currentData.Length > 0) currentData.AppendLine();
                                currentData.Append(line.Substring("data:".Length).TrimStart());
                            }
                        }
                        ProcessSseEvent(currentEvent, currentData.ToString(), fullText, onTextDelta,
                            ref inputTokens, ref outputTokens, ref cachedTokens, ref cacheCreationTokens);
                    }

                    return new StreamResult
                    {
                        FullText = fullText.ToString(),
                        InputTokens = inputTokens,
                        OutputTokens = outputTokens,
                        CachedInputTokens = cachedTokens,
                        CacheCreationInputTokens = cacheCreationTokens
                    };
                }
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

        private static JArray MarkLastToolForCaching(JArray tools)
        {
            var copy = new JArray();
            for (int i = 0; i < tools.Count; i++)
            {
                if (i == tools.Count - 1 && tools[i] is JObject lastTool)
                {
                    var cloned = (JObject)lastTool.DeepClone();
                    cloned["cache_control"] = new JObject { ["type"] = "ephemeral" };
                    copy.Add(cloned);
                }
                else
                {
                    copy.Add(tools[i]);
                }
            }
            return copy;
        }

        private static void ProcessSseEvent(
            string? eventName, string data, StringBuilder fullText, Action<string> onTextDelta,
            ref int inputTokens, ref int outputTokens, ref int cachedTokens, ref int cacheCreationTokens)
        {
            if (string.IsNullOrWhiteSpace(data) || data == "[DONE]") return;
            try
            {
                var payload = JObject.Parse(data);
                string? type = payload["type"]?.ToString();
                string effectiveType = !string.IsNullOrWhiteSpace(type) ? type! : (eventName ?? "");
                switch (effectiveType)
                {
                    case "content_block_delta":
                        string? deltaType = payload["delta"]?["type"]?.ToString();
                        if (string.Equals(deltaType, "text_delta", StringComparison.OrdinalIgnoreCase))
                        {
                            string? deltaText = payload["delta"]?["text"]?.ToString();
                            if (!string.IsNullOrEmpty(deltaText))
                            {
                                fullText.Append(deltaText);
                                onTextDelta?.Invoke(deltaText!);
                            }
                        }
                        break;
                    case "message_start":
                        var startUsage = payload["message"]?["usage"];
                        inputTokens = startUsage?["input_tokens"]?.Value<int>() ?? inputTokens;
                        outputTokens = startUsage?["output_tokens"]?.Value<int>() ?? outputTokens;
                        cachedTokens = startUsage?["cache_read_input_tokens"]?.Value<int>() ?? cachedTokens;
                        cacheCreationTokens = startUsage?["cache_creation_input_tokens"]?.Value<int>() ?? cacheCreationTokens;
                        break;
                    case "message_delta":
                        outputTokens = payload["usage"]?["output_tokens"]?.Value<int>() ?? outputTokens;
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AnthropicProtocolAdapter] SSE parse skipped: {ex.Message}");
            }
        }
    }
}
