using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace RevitMcpPlugin.AI
{
    /// <summary>
    /// Provider abstraction for LLM API calls.
    ///
    /// Canonical message format is Anthropic-shaped JArray:
    ///   [ { role: "user"|"assistant", content: string|JArray } ]
    /// Each provider adapter translates to/from the provider-native format.
    ///
    /// Tools are passed as Anthropic-shaped JArray:
    ///   [ { name, description, input_schema } ]
    ///
    /// Responses are returned in Anthropic-shaped JObject:
    ///   { content: [...], stop_reason, usage }
    /// This keeps the orchestrator (LlmOrchestrationService) provider-agnostic
    /// without forcing a heavy refactor of its tool-loop logic.
    /// </summary>
    public interface ILlmProvider
    {
        /// <summary>"anthropic" / "openai" / "gemini"</summary>
        string ProviderName { get; }

        /// <summary>Concrete model id, e.g. "claude-sonnet-4-6" / "gpt-5.5" / "gemini-3.1-pro-preview".</summary>
        string ModelId { get; }

        Task<JObject> SendNonStreamingAsync(
            JArray messages,
            string systemPrompt,
            JArray tools,
            CancellationToken ct,
            int maxTokens,
            bool jsonMode = false);

        Task<StreamResult> SendStreamingAsync(
            JArray messages,
            string systemPrompt,
            Action<string> onTextDelta,
            CancellationToken ct,
            int maxTokens);
    }

    public class StreamResult
    {
        public string FullText { get; set; } = "";
        public int InputTokens { get; set; }
        public int OutputTokens { get; set; }
        public int CachedInputTokens { get; set; }
        public int CacheCreationInputTokens { get; set; }
        public Newtonsoft.Json.Linq.JArray? ToolUseBlocks { get; set; }
    }
}
