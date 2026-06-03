import { useState, useRef, useEffect } from 'react';
import { ChatMessage } from '../types';
import MessageBubble from './MessageBubble';

interface Props {
  messages: ChatMessage[];
  isStreaming: boolean;
  streamingText: string;
  statusText: string | null;
  onSend: (text: string) => void;
}

export default function ChatPanel({ messages, isStreaming, streamingText, statusText, onSend }: Props) {
  const [input, setInput] = useState('');
  const messagesEndRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [messages, streamingText]);

  const handleSend = () => {
    if (!input.trim() || isStreaming) return;
    onSend(input);
    setInput('');
  };

  return (
    <div style={{ flex: 1, display: 'flex', flexDirection: 'column', overflow: 'hidden' }}>
      <div style={{ flex: 1, overflow: 'auto', padding: 'var(--space-lg)' }}>
        {messages.length === 0 && (
          <div style={{
            display: 'flex', alignItems: 'center', justifyContent: 'center',
            height: '100%', color: 'var(--color-text-muted)', fontSize: 'var(--text-sm)',
          }}>
            Send a message to interact with Revit via AI.
          </div>
        )}
        {messages.map((msg) => (
          <MessageBubble key={msg.id} message={msg} />
        ))}
        {isStreaming && streamingText && (
          <MessageBubble message={{ id: 'streaming', text: streamingText, isUser: false }} />
        )}
        {statusText && (
          <div style={{ padding: 'var(--space-sm) var(--space-lg)', color: 'var(--color-text-muted)', fontSize: 'var(--text-xs)' }}>
            {statusText}
          </div>
        )}
        <div ref={messagesEndRef} />
      </div>

      <div style={{
        padding: 'var(--space-sm) var(--space-lg)', borderTop: '1px solid var(--color-border)',
        display: 'flex', gap: 'var(--space-sm)', flexShrink: 0,
      }}>
        <textarea
          value={input}
          onChange={(e) => setInput(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); handleSend(); }
          }}
          placeholder="Ask about your Revit model..."
          disabled={isStreaming}
          rows={2}
          style={{
            flex: 1, resize: 'none',
            background: 'var(--color-bg-input)', color: 'var(--color-text-primary)',
            border: '1px solid var(--color-border)', borderRadius: 'var(--radius-md)',
            padding: 'var(--space-sm)', fontSize: 'var(--text-sm)',
            fontFamily: 'var(--font-sans)', outline: 'none',
          }}
        />
        <button
          onClick={handleSend}
          disabled={isStreaming || !input.trim()}
          style={{
            background: 'var(--color-accent)', color: '#fff', border: 'none',
            borderRadius: 'var(--radius-md)', padding: 'var(--space-sm) var(--space-lg)',
            cursor: isStreaming || !input.trim() ? 'default' : 'pointer',
            fontWeight: 600, fontSize: 'var(--text-sm)', opacity: isStreaming ? 0.5 : 1,
          }}
        >
          Send
        </button>
      </div>
    </div>
  );
}
