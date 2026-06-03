using System;
using System.Collections.Concurrent;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RevitMcpPlugin.AI;

public class WebView2Bridge : IDisposable
{
    private readonly WebView2 _webView;
    private readonly ConcurrentDictionary<string, Action<string?>> _handlers = new();
    public event EventHandler<BridgeMessage>? MessageReceived;

    public WebView2Bridge(WebView2 webView)
    {
        _webView = webView ?? throw new ArgumentNullException(nameof(webView));
    }

    public void Initialize()
    {
        _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
        _webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(@"
                window.revitMcp = window.revitMcp || {};
                window.revitMcp._handlers = {};
                window.revitMcp.on = function(type, handler) {
                    if (!window.revitMcp._handlers[type]) window.revitMcp._handlers[type] = [];
                    window.revitMcp._handlers[type].push(handler);
                };
                window.revitMcp.onMessage = function(type, payload) {
                    var hs = window.revitMcp._handlers[type];
                    if (hs) hs.forEach(function(h) { h(payload); });
                };
                window.revitMcp.send = function(type, payload) {
                    window.chrome.webview.postMessage({ type: type, payload: payload });
                };
            ");
    }

    public void PostMessage(string type, object? payload = null)
    {
        if (_isDisposed) return;
        try
        {
            string payloadJson = payload != null
                ? JsonConvert.SerializeObject(payload)
                : "null";
            string escapedType = type.Replace("\\", "\\\\").Replace("'", "\\'");
            string script = $"if(window.revitMcp && window.revitMcp.onMessage) window.revitMcp.onMessage('{escapedType}', {payloadJson});";
            if (_webView.Dispatcher.CheckAccess())
                _webView.CoreWebView2.ExecuteScriptAsync(script);
            else
                _webView.Dispatcher.Invoke(() => _webView.CoreWebView2.ExecuteScriptAsync(script));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WebView2Bridge] PostMessage error: {ex.Message}");
        }
    }

    public void On(string type, Action<string?> handler) => _handlers[type] = handler;

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            string raw = e.WebMessageAsJson;
            var msg = JsonConvert.DeserializeObject<BridgeMessage>(raw);
            if (msg == null) return;
            if (_handlers.TryGetValue(msg.Type, out var handler))
                handler(msg.Payload?.ToString());
            MessageReceived?.Invoke(this, msg);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WebView2Bridge] OnWebMessage error: {ex.Message}");
        }
    }

    private volatile bool _isDisposed;

    public void Dispose()
    {
        _isDisposed = true;
        if (_webView?.CoreWebView2 != null)
        {
            try { _webView.CoreWebView2.WebMessageReceived -= OnWebMessageReceived; }
            catch { /* WebView2 may already be disposed */ }
        }
    }
}

public class BridgeMessage
{
    public string Type { get; set; } = "";
    public JToken? Payload { get; set; }
}
