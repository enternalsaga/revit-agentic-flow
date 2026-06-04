import { useState, useEffect, useRef } from 'react';
import { sendToBackend, onBackendMessage } from './bridge';
import { ChatMessage, AiSettings, TokenUsage, ViewMode } from './types';
import ChatPanel from './components/ChatPanel';
import SettingsPanel from './components/SettingsPanel';

type BackendAiSettings = Partial<AiSettings> & {
  ActiveModel?: string;
  Providers?: Record<string, any>;
};

const normalizeSettings = (payload: unknown): AiSettings => {
  const data = (payload || {}) as BackendAiSettings;
  const rawProviders = data.providers || data.Providers || {};
  const providers = Object.fromEntries(
    Object.entries(rawProviders).map(([id, provider]) => [
      id,
      {
        id,
        protocol: provider.protocol || provider.Protocol || '',
        baseUrl: provider.baseUrl || provider.BaseUrl || '',
        apiKey: provider.apiKey || provider.ApiKey || '',
        models: (provider.models || provider.Models || []).map((model: any) => ({
          id: model.id || model.Id || '',
          label: model.label || model.Label || '',
          speed: model.speed || model.Speed || 'medium',
        })),
        headers: provider.headers || provider.Headers,
      },
    ])
  );

  return {
    activeModel: data.activeModel || data.ActiveModel || 'claude-sonnet-4-6',
    providers,
  };
};

export default function App() {
  const [view, setView] = useState<ViewMode>('chat');
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [isStreaming, setIsStreaming] = useState(false);
  const [streamingText, setStreamingText] = useState('');
  const [statusText, setStatusText] = useState<string | null>(null);
  const [settings, setSettings] = useState<AiSettings | null>(null);
  const [lastUsage, setLastUsage] = useState<TokenUsage | null>(null);
  const streamingTextRef = useRef('');

  // Register backend message handlers once
  useEffect(() => {
    onBackendMessage('streaming_delta', (payload) => {
      const data = payload as { delta?: string } | null;
      const text = data?.delta || '';
      streamingTextRef.current += text;
      setStreamingText(streamingTextRef.current);
    });

    onBackendMessage('streaming_end', (payload) => {
      const data = payload as { text?: string; usage?: TokenUsage } | null;
      const finalText = data?.text || streamingTextRef.current;
      const usage = data?.usage || undefined;
      setLastUsage(usage || null);
      setMessages(prev => {
        const updated = [...prev];
        if (updated.length > 0 && !updated[updated.length - 1].isUser) {
          const last = updated[updated.length - 1];
          updated[updated.length - 1] = { ...last, text: finalText || last.text, tokenUsage: usage };
        }
        return updated;
      });
      streamingTextRef.current = '';
      setStreamingText('');
      setIsStreaming(false);
      setStatusText(null);
    });

    onBackendMessage('status_update', (payload) => {
      setStatusText(payload as string || null);
    });

    onBackendMessage('settings_loaded', (payload) => {
      setSettings(normalizeSettings(payload));
    });
  }, []);

  const handleSend = (text: string) => {
    if (!text.trim()) return;
    const userMsg: ChatMessage = { id: Date.now().toString(), text, isUser: true };
    setMessages(prev => [...prev, userMsg]);
    // Add placeholder assistant message
    setMessages(prev => [...prev, { id: (Date.now() + 1).toString(), text: '', isUser: false }]);
    streamingTextRef.current = '';
    setStreamingText('');
    setIsStreaming(true);
    sendToBackend('send_message', { text });
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', height: '100%', overflow: 'hidden' }}>
      <header style={{
        display: 'flex', alignItems: 'center',
        padding: 'var(--space-sm) var(--space-lg)',
        borderBottom: '1px solid var(--color-border)',
        flexShrink: 0, gap: 'var(--space-sm)',
      }}>
        <span style={{ fontSize: 'var(--text-base)', fontWeight: 600, color: 'var(--color-accent)' }}>
          Revit MCP AI
        </span>
        <div style={{ marginLeft: 'auto' }}>
          <button
            onClick={() => {
              setView(view === 'chat' ? 'settings' : 'chat');
              if (view !== 'settings') sendToBackend('load_settings');
            }}
            style={{
              background: 'none', border: '1px solid var(--color-border)',
              borderRadius: 'var(--radius-sm)', padding: '4px 10px',
              color: 'var(--color-text-secondary)', cursor: 'pointer',
              fontSize: 'var(--text-xs)',
            }}
          >
            {view === 'chat' ? 'Settings' : 'Chat'}
          </button>
        </div>
      </header>

      {view === 'chat' ? (
        <ChatPanel
          messages={messages}
          isStreaming={isStreaming}
          streamingText={streamingText}
          statusText={statusText}
          onSend={handleSend}
        />
      ) : (
        <SettingsPanel
          settings={settings}
          usage={lastUsage}
          onSave={(s) => {
            sendToBackend('save_settings', s);
            setSettings(s);
            setView('chat');
          }}
        />
      )}
    </div>
  );
}
