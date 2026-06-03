using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Autodesk.Revit.UI;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RevitMcpPlugin.Core;

namespace RevitMcpPlugin.AI;

public class AiPanelProvider : IDockablePaneProvider, IDisposable
{
    private WebView2? _webView;
    private Grid? _hostGrid;
    private WebView2Bridge? _bridge;
    private bool _initialized;
    private LlmOrchestrationService? _llmService;
    private readonly List<(string role, string text)> _chatHistory = new();
    private readonly object _chatHistoryLock = new();
    private CancellationTokenSource? _streamingCts;
    private AiConfig _config = AiConfigService.CreateDefault();
    private readonly AiConfigService _configService = new();

    public FrameworkElement HostElement => _hostGrid ?? throw new InvalidOperationException("AI pane is not initialized.");
    public WebView2Bridge Bridge => _bridge ?? throw new InvalidOperationException("AI bridge is not initialized.");

    public static readonly DockablePaneId PanelId =
        new DockablePaneId(new Guid("A1A2A3A4-B5C6-D7E8-F9A0-B1C2D3E4F5A6"));

    private static readonly string SystemPrompt = SystemPromptBuilder.Build();

    public void SetupDockablePane(DockablePaneProviderData data)
    {
        _hostGrid = new Grid();
        _webView = new WebView2
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        _hostGrid.Children.Add(_webView);

        data.FrameworkElement = _hostGrid;
        data.InitialState = new DockablePaneState
        {
            DockPosition = DockPosition.Right,
            TabBehind = DockablePanes.BuiltInDockablePanes.ProjectBrowser
        };

        _ = InitializeWebViewAsync();
    }

    private async Task InitializeWebViewAsync()
    {
        if (_initialized) return;
        if (_webView == null) return;
        try
        {
            string userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RevitMCP", "WebView2");
            var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
            await _webView.EnsureCoreWebView2Async(env);

            _bridge = new WebView2Bridge(_webView);
            _bridge.Initialize();
            RegisterBridgeHandlers();

            _config = _configService.Load();

            string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
            string wwwrootDir = Path.Combine(assemblyDir, "wwwroot");

            if (Directory.Exists(wwwrootDir))
            {
                _webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    "revit-mcp.local", wwwrootDir,
                    CoreWebView2HostResourceAccessKind.Allow);
                _webView.CoreWebView2.Navigate("https://revit-mcp.local/index.html");
            }
            else
            {
                _webView.CoreWebView2.NavigateToString(
                    "<html><body style='font-family:Segoe UI;padding:20px'>" +
                    "<h2>AI Chat frontend not built.</h2>" +
                    "<p>Run: <code>cd frontend &amp;&amp; npm run build</code></p></body></html>");
            }

            _initialized = true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AiPanelProvider] Init failed: {ex.Message}");
        }
    }

    private void RegisterBridgeHandlers()
    {
        var bridge = _bridge ?? throw new InvalidOperationException("AI bridge is not initialized.");

        bridge.On("send_message", async (payload) =>
        {
            try
            {
                var payloadObj = JObject.Parse(payload ?? "{}");
                string text = payloadObj["text"]?.ToString() ?? "";

                if (string.IsNullOrWhiteSpace(text)) return;

                lock (_chatHistoryLock)
                    _chatHistory.Add(("user", text));

                _streamingCts?.Cancel();
                _streamingCts = new CancellationTokenSource();

                EnsureLlmService();

                if (_llmService == null)
                {
                    Post("streaming_end", new
                    {
                        text = "No LLM configured. Please set your API key in Settings.",
                        usage = new { input_tokens = 0, output_tokens = 0 }
                    });
                    return;
                }

                _llmService.OnStreamingDelta -= OnStreamingDelta;
                _llmService.OnStreamingDelta += OnStreamingDelta;
                _llmService.OnStatusUpdate -= OnStatusUpdate;
                _llmService.OnStatusUpdate += OnStatusUpdate;

                Post("status_update", "Thinking...");

                var messages = LlmOrchestrationService.BuildMessagesArray(_chatHistory);
                string commandJsonPath = FindCommandJsonPath();
                JArray? toolDefinitions = null;
                Func<string, string, CancellationToken, Task<string>>? toolExecutor = null;

                if (!string.IsNullOrEmpty(commandJsonPath) && File.Exists(commandJsonPath))
                {
                    var builder = new ToolDefinitionBuilder();
                    toolDefinitions = builder.BuildFromCommandJson(commandJsonPath);
                }

                var executor = GetCommandExecutor();
                if (toolDefinitions != null && toolDefinitions.Count > 0 && executor != null)
                {
                    toolExecutor = (toolName, toolInput, ct) =>
                    {
                        var request = new JObject
                        {
                            ["jsonrpc"] = "2.0",
                            ["method"] = toolName,
                            ["params"] = JObject.Parse(toolInput),
                            ["id"] = "1"
                        };
                        return Task.FromResult(executor.ExecuteJson(request.ToString(Formatting.None)));
                    };
                }

                LlmResponse response;
                if (toolExecutor != null && toolDefinitions != null)
                {
                    response = await _llmService.GenerateWithToolsAsync(
                        messages, SystemPrompt, toolDefinitions,
                        toolExecutor, ct: _streamingCts.Token);
                }
                else
                {
                    response = await _llmService.SendMessageAsync(
                        messages, SystemPrompt, ct: _streamingCts.Token);
                }

                string responseText = response.Success
                    ? response.Text
                    : $"Error: {response.ErrorMessage}";

                Post("streaming_end", new
                {
                    text = responseText,
                    usage = new
                    {
                        input_tokens = response.InputTokens,
                        output_tokens = response.OutputTokens,
                        cached_input_tokens = response.CachedInputTokens
                    }
                });

                lock (_chatHistoryLock)
                    _chatHistory.Add(("assistant", responseText));
            }
            catch (OperationCanceledException)
            {
                Post("status_update", null);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AiPanelProvider] send_message error: {ex.Message}");
                Post("streaming_end", new { text = $"Error: {ex.Message}", usage = new { input_tokens = 0, output_tokens = 0 } });
            }
            finally
            {
                Post("status_update", null);
            }
        });

        bridge.On("load_settings", (payload) =>
        {
            try
            {
                _config = _configService.Load();
                Post("settings_loaded", _config);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AiPanelProvider] load_settings error: {ex.Message}");
            }
        });

        bridge.On("save_settings", (payload) =>
        {
            try
            {
                _config = JsonConvert.DeserializeObject<AiConfig>(payload ?? "{}") ?? AiConfigService.CreateDefault();
                _configService.Save(_config);
                _llmService = null;
                Post("settings_saved", true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AiPanelProvider] save_settings error: {ex.Message}");
                Post("settings_saved", false);
            }
        });

        bridge.On("clear_history", (payload) =>
        {
            lock (_chatHistoryLock) _chatHistory.Clear();
            TokenTracker.ResetSession();
        });
    }

    private void EnsureLlmService()
    {
        if (_llmService != null) return;
        _llmService = LlmOrchestrationService.CreateFromConfig(_config);
    }

    private void OnStreamingDelta(string delta) => Post("streaming_delta", new { delta });
    private void OnStatusUpdate(string? status) => Post("status_update", status);

    private CommandExecutor? GetCommandExecutor()
    {
        return App.Service?.Executor;
    }

    private void Post(string type, object? payload = null)
    {
        try { _bridge?.PostMessage(type, payload); }
        catch { /* bridge not ready */ }
    }

    private static string FindCommandJsonPath()
    {
        string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
        return Path.Combine(assemblyDir, "command.json");
    }

    public void Dispose()
    {
        _streamingCts?.Cancel();
        _streamingCts?.Dispose();
        _bridge?.Dispose();
    }
}
