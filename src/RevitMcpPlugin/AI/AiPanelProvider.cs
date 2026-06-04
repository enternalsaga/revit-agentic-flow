using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Autodesk.Revit.UI;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RevitMcpPlugin.Configuration;
using RevitMcpPlugin.Core;
using RevitMcpSdk.Rag;

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

    private static readonly string DiagLogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RevitMCP", "ai-panel-debug.log");

    private static void DiagLog(string message)
    {
        try
        {
            var dir = Path.GetDirectoryName(DiagLogPath)!;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.AppendAllText(DiagLogPath,
                $"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}");
        }
        catch { /* best-effort */ }
    }

    public void SetupDockablePane(DockablePaneProviderData data)
    {
        DiagLog("SetupDockablePane START");
        _hostGrid = new Grid();
        _webView = new WebView2
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        _hostGrid.Children.Add(_webView);
        _webView.Loaded += OnWebViewLoaded;

        data.FrameworkElement = _hostGrid;
        data.InitialState = new DockablePaneState
        {
            DockPosition = DockPosition.Right,
            TabBehind = DockablePanes.BuiltInDockablePanes.ProjectBrowser
        };

        DiagLog("SetupDockablePane - waiting for WebView Loaded");
    }

    private void OnWebViewLoaded(object sender, RoutedEventArgs e)
    {
        if (_webView == null) return;

        _webView.Loaded -= OnWebViewLoaded;
        DiagLog("WebView Loaded - scheduling InitializeWebViewAsync at ApplicationIdle");
        _webView.Dispatcher.BeginInvoke(
            new Action(() => _ = InitializeWebViewAsync()),
            DispatcherPriority.ApplicationIdle);
    }

    private async Task InitializeWebViewAsync()
    {
        DiagLog("InitializeWebViewAsync START");
        if (_initialized) { DiagLog("Already initialized, returning"); return; }
        if (_webView == null) { DiagLog("_webView is null, returning"); return; }
        try
        {
            DiagLog("Step 1: GetAvailableBrowserVersionString");
            string? runtimeVersion = null;
            try
            {
                runtimeVersion = CoreWebView2Environment.GetAvailableBrowserVersionString();
                DiagLog($"Runtime version: {runtimeVersion}");
            }
            catch (Exception verEx)
            {
                DiagLog($"Cannot detect WebView2 Runtime: {verEx.Message}");
            }

            var sdkAssembly = typeof(CoreWebView2Environment).Assembly;
            var sdkVersion = sdkAssembly.GetName().Version?.ToString() ?? "unknown";
            DiagLog($"SDK version: {sdkVersion}, Runtime: {runtimeVersion ?? "NOT INSTALLED"}");
            DiagLog($"SDK assembly path: {sdkAssembly.Location}");
            // Dump ALL WebView2 assemblies loaded in AppDomain
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.FullName?.Contains("WebView2") == true)
                    DiagLog($"  AppDomain WebView2 asm: {asm.FullName} @ {asm.Location}");
            }
            if (runtimeVersion == null)
            {
                DiagLog("Runtime NOT found - showing diagnostic");
                ShowDiagnosticText(
                    "WebView2 Runtime not found",
                    "Microsoft Edge WebView2 Runtime is not installed on this machine.",
                    "Download and install from: https://developer.microsoft.com/en-us/microsoft-edge/webview2/\nThen restart Revit.");
                return;
            }

            DiagLog("Step 2: EnsureCoreWebView2Async via explicit environment");
            DiagLog($"Current thread: {System.Threading.Thread.CurrentThread.ManagedThreadId}, " +
                    $"IsBackground={System.Threading.Thread.CurrentThread.IsBackground}, " +
                    $"ApartmentState={System.Threading.Thread.CurrentThread.GetApartmentState()}, " +
                    $"Dispatcher={_webView.Dispatcher.Thread.ManagedThreadId}");
            string userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RevitMCP", "WebView2");
            DiagLog($"UserDataFolder: {userDataFolder}");

            var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
            DiagLog("Step 3: CoreWebView2Environment.CreateAsync OK");
            await _webView.EnsureCoreWebView2Async(env);
            DiagLog("Step 4: WebView2 initialized OK");
            _webView.CoreWebView2.NavigationCompleted += OnNavigationCompleted;
            _webView.CoreWebView2.ProcessFailed += OnProcessFailed;

            _bridge = new WebView2Bridge(_webView);
            _bridge.Initialize();
            RegisterBridgeHandlers();

            _config = _configService.Load();

            string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
            string wwwrootDir = Path.Combine(assemblyDir, "wwwroot");
            string indexPath = Path.Combine(wwwrootDir, "index.html");

            if (File.Exists(indexPath))
            {
                _webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    "revit-mcp.local", wwwrootDir,
                    CoreWebView2HostResourceAccessKind.Allow);
                if (string.Equals(Environment.GetEnvironmentVariable("REVIT_MCP_AI_DEBUG_WEBVIEW"), "1", StringComparison.Ordinal))
                    _webView.CoreWebView2.OpenDevToolsWindow();
                _webView.CoreWebView2.Navigate("https://revit-mcp.local/index.html");
            }
            else
            {
                _webView.CoreWebView2.NavigateToString(BuildDiagnosticHtml(
                    "AI Chat frontend not found",
                    $"Expected index.html at: {indexPath}",
                    "Run .\\.scripts\\deploy-phase1.ps1 for the Revit version you are launching."));
            }

            _initialized = true;
        }
        catch (Exception ex)
        {
            DiagLog($"INIT FAILED: {ex}");
            Debug.WriteLine($"[AiPanelProvider] Init failed: {ex.Message}");

            string sdkVer;
            try { sdkVer = typeof(CoreWebView2Environment).Assembly.GetName().Version?.ToString() ?? "?"; }
            catch { sdkVer = "?"; }

            string rtVer;
            try { rtVer = CoreWebView2Environment.GetAvailableBrowserVersionString() ?? "?"; }
            catch { rtVer = "?"; }

            ShowDiagnosticText(
                "WebView2 initialization failed",
                $"{ex.Message}\n\nSDK: {sdkVer} | Runtime: {rtVer}",
                "Ensure Microsoft Edge WebView2 Runtime is installed, then restart Revit.\n"
                + "If this persists, the SDK and Runtime versions may be incompatible.");
        }
    }

    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (e.IsSuccess) return;

        string message = $"WebView2 navigation failed: {e.WebErrorStatus}";
        Debug.WriteLine($"[AiPanelProvider] {message}");
        _webView?.CoreWebView2.NavigateToString(BuildDiagnosticHtml(
            "AI Chat page failed to load",
            message,
            "Check that wwwroot/index.html and wwwroot/assets are deployed next to RevitMcpPlugin.dll."));
    }

    private void OnProcessFailed(object? sender, CoreWebView2ProcessFailedEventArgs e)
    {
        string message = $"WebView2 process failed: {e.ProcessFailedKind}";
        Debug.WriteLine($"[AiPanelProvider] {message}");
        ShowDiagnosticText("AI Chat WebView crashed", message, "Restart Revit and reopen the AI Chat panel.");
    }

    private void ShowDiagnosticText(string title, string detail, string action)
    {
        if (_hostGrid == null) return;

        void Update()
        {
            _hostGrid.Children.Clear();
            _hostGrid.Background = new SolidColorBrush(Color.FromRgb(15, 17, 23));
            _hostGrid.Children.Add(new TextBlock
            {
                Text = $"{title}\n\n{detail}\n\n{action}",
                Foreground = new SolidColorBrush(Color.FromRgb(228, 228, 231)),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(16),
                VerticalAlignment = VerticalAlignment.Center
            });
        }

        if (_hostGrid.Dispatcher.CheckAccess()) Update();
        else _hostGrid.Dispatcher.Invoke(Update);
    }

    private static string BuildDiagnosticHtml(string title, string detail, string action)
    {
        string safeTitle = System.Net.WebUtility.HtmlEncode(title);
        string safeDetail = System.Net.WebUtility.HtmlEncode(detail);
        string safeAction = System.Net.WebUtility.HtmlEncode(action);
        return $@"<!doctype html>
<html>
<head>
<meta charset='utf-8'>
<style>
body {{ margin: 0; background: #0f1117; color: #e4e4e7; font-family: Segoe UI, sans-serif; }}
main {{ padding: 16px; line-height: 1.5; }}
h2 {{ font-size: 16px; margin: 0 0 12px; }}
pre {{ white-space: pre-wrap; background: #1a1b26; padding: 12px; border-radius: 4px; }}
</style>
</head>
<body>
<main>
<h2>{safeTitle}</h2>
<pre>{safeDetail}</pre>
<p>{safeAction}</p>
</main>
</body>
</html>";
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
                        usage = new { inputTokens = 0, outputTokens = 0 }
                    });
                    return;
                }

                // RAG auto-injection
                string requestSystemPrompt = SystemPrompt;
                try
                {
                    var ragResult = await LocalRevitRagService.FetchAsync(text, "2024");
                    if (ragResult.HasContext)
                    {
                        requestSystemPrompt += $"\n\n## Relevant Revit API Documentation\n{ragResult.ContextText}";
                    }
                }
                catch (Exception ragEx)
                {
                    Debug.WriteLine($"[AiPanelProvider] RAG lookup skipped: {ragEx.Message}");
                }

                _llmService.OnStreamingDelta -= OnStreamingDelta;
                _llmService.OnStreamingDelta += OnStreamingDelta;
                _llmService.OnStatusUpdate -= OnStatusUpdate;
                _llmService.OnStatusUpdate += OnStatusUpdate;

                Post("status_update", "Thinking...");

                // History sliding window
                const int SlidingWindowSize = 10;
                List<(string role, string text)> historyForRequest;
                lock (_chatHistoryLock)
                {
                    if (_chatHistory.Count > SlidingWindowSize)
                    {
                        var dropped = _chatHistory.Take(_chatHistory.Count - SlidingWindowSize).ToList();
                        var summary = HistorySummariser.Summarise(dropped);

                        historyForRequest = new List<(string role, string text)>
                        {
                            ("user", summary)
                        };
                        historyForRequest.AddRange(_chatHistory.Skip(_chatHistory.Count - SlidingWindowSize));
                    }
                    else
                    {
                        historyForRequest = _chatHistory.ToList();
                    }
                }

                var messages = LlmOrchestrationService.BuildMessagesArray(historyForRequest);
                var commandJsonPaths = FindCommandJsonPaths();
                var toolDefinitions = new JArray();
                Func<string, string, CancellationToken, Task<string>>? toolExecutor = null;

                if (commandJsonPaths.Count > 0)
                {
                    var builder = new ToolDefinitionBuilder();
                    foreach (var commandJsonPath in commandJsonPaths)
                    {
                        foreach (var tool in builder.BuildFromCommandJson(commandJsonPath))
                            toolDefinitions.Add(tool);
                    }
                }

                var executor = GetCommandExecutor();
                if (toolDefinitions.Count > 0 && executor != null)
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
                if (toolExecutor != null && toolDefinitions.Count > 0)
                {
                    response = await _llmService.GenerateWithToolsAsync(
                        messages, requestSystemPrompt, toolDefinitions,
                        toolExecutor, ct: _streamingCts.Token);
                }
                else
                {
                    response = await _llmService.SendMessageAsync(
                        messages, requestSystemPrompt, ct: _streamingCts.Token);
                }

                string responseText = response.Success
                    ? response.Text
                    : $"Error: {response.ErrorMessage}";

                Post("streaming_end", new
                {
                    text = responseText,
                    usage = new
                    {
                        inputTokens = response.InputTokens,
                        outputTokens = response.OutputTokens,
                        cachedInputTokens = response.CachedInputTokens,
                        cacheCreationInputTokens = response.CacheCreationInputTokens,
                        sessionInputTokens = TokenTracker.SessionInputTokens,
                        sessionOutputTokens = TokenTracker.SessionOutputTokens,
                        sessionCachedInputTokens = TokenTracker.SessionCachedInputTokens,
                        sessionCacheHitRatio = TokenTracker.SessionCacheHitRatio
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
                Post("streaming_end", new { text = $"Error: {ex.Message}", usage = new { inputTokens = 0, outputTokens = 0 } });
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

    private static List<string> FindCommandJsonPaths()
    {
        var result = new List<string>();
        string commandsDir = PathManager.GetCommandsDirectoryPath();
        if (!Directory.Exists(commandsDir))
            return result;

        foreach (var dir in Directory.GetDirectories(commandsDir))
        {
            if (Path.GetFileName(dir).StartsWith(".", StringComparison.Ordinal))
                continue;

            string commandJsonPath = Path.Combine(dir, "command.json");
            if (File.Exists(commandJsonPath))
                result.Add(commandJsonPath);
        }

        return result;
    }

    public void Dispose()
    {
        _streamingCts?.Cancel();
        _streamingCts?.Dispose();
        try
        {
            var coreWebView = _webView?.CoreWebView2;
            if (coreWebView != null)
            {
                coreWebView.NavigationCompleted -= OnNavigationCompleted;
                coreWebView.ProcessFailed -= OnProcessFailed;
            }
        }
        catch { /* WebView2 may already be disposed */ }

        _bridge?.Dispose();
        _webView?.Dispose();
    }
}
