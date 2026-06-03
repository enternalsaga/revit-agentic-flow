using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace RevitMcpPlugin.AI
{
    public class ModelInfo
    {
        public string Id { get; set; } = "";
        public string Label { get; set; } = "";
        public string Speed { get; set; } = "medium"; // fast, medium, slow
    }

    public class ProviderConfig
    {
        public string Protocol { get; set; } = "anthropic"; // "anthropic" or "openai"
        public string BaseUrl { get; set; } = "";
        public string ApiKey { get; set; } = "";
        public List<ModelInfo> Models { get; set; } = new();
        public Dictionary<string, string>? Headers { get; set; }
    }

    public class AiConfig
    {
        public string ActiveModel { get; set; } = "claude-sonnet-4-6";
        public Dictionary<string, ProviderConfig> Providers { get; set; } = new();
    }

    public class AiConfigService
    {
        private static readonly string ConfigDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RevitMCP");
        private static readonly string ConfigPath = Path.Combine(ConfigDir, "ai_config.json");

        public AiConfig Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    var json = File.ReadAllText(ConfigPath);
                    return JsonConvert.DeserializeObject<AiConfig>(json) ?? CreateDefault();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AiConfigService] Failed to load config: {ex.Message}");
            }

            return CreateDefault();
        }

        public void Save(AiConfig config)
        {
            try
            {
                if (!Directory.Exists(ConfigDir))
                    Directory.CreateDirectory(ConfigDir);

                var json = JsonConvert.SerializeObject(config, Formatting.Indented);
                File.WriteAllText(ConfigPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AiConfigService] Failed to save config: {ex.Message}");
            }
        }

        public (ProviderConfig provider, string modelId)? ResolveActiveModel(AiConfig config)
        {
            foreach (var kvp in config.Providers)
            {
                foreach (var model in kvp.Value.Models)
                {
                    if (model.Id == config.ActiveModel)
                        return (kvp.Value, model.Id);
                }
            }

            return null;
        }

        public static AiConfig CreateDefault()
        {
            return new AiConfig
            {
                ActiveModel = "claude-sonnet-4-6",
                Providers = new Dictionary<string, ProviderConfig>
                {
                    ["anthropic"] = new ProviderConfig
                    {
                        Protocol = "anthropic",
                        BaseUrl = "https://api.anthropic.com",
                        ApiKey = "",
                        Models = new List<ModelInfo>
                        {
                            new() { Id = "claude-sonnet-4-6", Label = "Claude Sonnet 4.6", Speed = "fast" },
                            new() { Id = "claude-opus-4-7", Label = "Claude Opus 4.7", Speed = "slow" }
                        }
                    },
                    ["openai"] = new ProviderConfig
                    {
                        Protocol = "openai",
                        BaseUrl = "https://api.openai.com",
                        ApiKey = "",
                        Models = new List<ModelInfo>
                        {
                            new() { Id = "gpt-4.1", Label = "GPT-4.1", Speed = "medium" },
                            new() { Id = "gpt-4.1-mini", Label = "GPT-4.1 Mini", Speed = "fast" }
                        }
                    },
                    ["gemini"] = new ProviderConfig
                    {
                        Protocol = "openai",
                        BaseUrl = "https://generativelanguage.googleapis.com/v1beta/openai",
                        ApiKey = "",
                        Models = new List<ModelInfo>
                        {
                            new() { Id = "gemini-2.5-pro", Label = "Gemini 2.5 Pro", Speed = "medium" },
                            new() { Id = "gemini-2.5-flash", Label = "Gemini 2.5 Flash", Speed = "fast" }
                        }
                    },
                    ["deepseek"] = new ProviderConfig
                    {
                        Protocol = "openai",
                        BaseUrl = "https://api.deepseek.com",
                        ApiKey = "",
                        Models = new List<ModelInfo>
                        {
                            new() { Id = "deepseek-chat", Label = "DeepSeek Chat", Speed = "fast" }
                        }
                    },
                    ["glm"] = new ProviderConfig
                    {
                        Protocol = "openai",
                        BaseUrl = "https://open.bigmodel.cn/api/paas",
                        ApiKey = "",
                        Models = new List<ModelInfo>
                        {
                            new() { Id = "glm-4-plus", Label = "GLM-4 Plus", Speed = "medium" }
                        }
                    },
                    ["openrouter"] = new ProviderConfig
                    {
                        Protocol = "openai",
                        BaseUrl = "https://openrouter.ai/api",
                        ApiKey = "",
                        Models = new List<ModelInfo>
                        {
                            new() { Id = "anthropic/claude-sonnet-4", Label = "Claude Sonnet 4 (OpenRouter)", Speed = "fast" }
                        },
                        Headers = new Dictionary<string, string>
                        {
                            ["X-Title"] = "RevitMCP"
                        }
                    }
                }
            };
        }
    }
}
