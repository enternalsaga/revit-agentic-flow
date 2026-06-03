import { ChatMessage } from '../types';

interface Props {
  message: ChatMessage;
}

export default function MessageBubble({ message }: Props) {
  const isUser = message.isUser;

  return (
    <div style={{
      display: 'flex', justifyContent: isUser ? 'flex-end' : 'flex-start',
      marginBottom: 'var(--space-sm)',
    }}>
      <div style={{
        maxWidth: '85%',
        padding: 'var(--space-sm) var(--space-md)',
        borderRadius: 'var(--radius-md)',
        background: isUser ? 'var(--color-accent)' : 'var(--color-bg-secondary)',
        color: isUser ? '#fff' : 'var(--color-text-primary)',
        fontSize: 'var(--text-sm)',
        lineHeight: 'var(--leading-normal)',
        whiteSpace: 'pre-wrap',
        wordBreak: 'break-word',
      }}>
        {message.text}
        {message.toolCalls && message.toolCalls.length > 0 && (
          <div style={{ marginTop: 'var(--space-xs)', fontSize: 'var(--text-xs)', opacity: 0.7 }}>
            {message.toolCalls.map((tc, i) => (
              <div key={i}>
                Tool: {tc.name}
                {tc.result && <div style={{ opacity: 0.7 }}>{tc.result.substring(0, 100)}{tc.result.length > 100 ? '...' : ''}</div>}
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
