using System;
using System.Net.Http;

namespace RevitMcpPlugin.AI;

public static class LlmProviderFactory
{
    private static readonly HttpClient SharedHttpClient = new();

    public static ILlmProvider Create(ProviderConfig provider, string modelId)
    {
        return provider.Protocol switch
        {
            "anthropic" => new AnthropicProtocolAdapter(provider, modelId, SharedHttpClient),
            "openai" => new OpenAIProtocolAdapter(provider, modelId, SharedHttpClient),
            _ => throw new ArgumentException($"Unknown protocol: {provider.Protocol}")
        };
    }
}
