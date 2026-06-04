using System;

namespace RevitMcpPlugin.AI;

public static class LlmProviderFactory
{
    public static ILlmProvider Create(ProviderConfig provider, string modelId)
    {
        return provider.Protocol switch
        {
            "anthropic" => new AnthropicProtocolAdapter(provider, modelId),
            "openai" => new OpenAIProtocolAdapter(provider, modelId),
            _ => throw new ArgumentException($"Unknown protocol: {provider.Protocol}")
        };
    }
}
