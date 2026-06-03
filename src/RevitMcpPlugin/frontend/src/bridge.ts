interface RevitMcpBridge {
  send: (type: string, payload?: unknown) => void;
  on: (type: string, handler: (payload: unknown) => void) => void;
  onMessage: (type: string, payload: unknown) => void;
  _handlers: Record<string, (payload: unknown) => void>;
}

declare global {
  interface Window {
    revitMcp: RevitMcpBridge;
    chrome: {
      webview: {
        postMessage: (msg: string) => void;
      };
    };
  }
}

export function sendToBackend(type: string, payload?: unknown): void {
  if (window.revitMcp?.send) {
    window.revitMcp.send(type, payload);
  } else {
    console.warn('[Bridge] Not in WebView2 context, message dropped:', type);
  }
}

export function onBackendMessage(type: string, handler: (payload: unknown) => void): void {
  if (window.revitMcp?.on) {
    window.revitMcp.on(type, handler);
  }
}

export function isWebView2(): boolean {
  return typeof window.chrome?.webview?.postMessage === 'function';
}
