import Markdown from 'react-markdown';
import CodeBlock from './CodeBlock';
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
        wordBreak: 'break-word',
      }}>
        {isUser ? (
          <span style={{ whiteSpace: 'pre-wrap' }}>{message.text}</span>
        ) : (
          <Markdown
            components={{
              code({ className, children, ...props }) {
                const match = /language-(\w+)/.exec(className || '');
                const codeStr = String(children).replace(/\n$/, '');
                if (match) {
                  return <CodeBlock code={codeStr} language={match[1]} />;
                }
                return (
                  <code
                    style={{
                      background: 'var(--color-bg-code)',
                      padding: '1px 4px',
                      borderRadius: 'var(--radius-sm)',
                      fontSize: 'var(--text-xs)',
                    }}
                    {...props}
                  >
                    {children}
                  </code>
                );
              },
            }}
          >
            {message.text}
          </Markdown>
        )}
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
        {message.tokenUsage && !message.isUser && (
          <div style={{
            marginTop: 'var(--space-xs)', fontSize: 'var(--text-xs)', opacity: 0.5,
            display: 'flex', gap: 'var(--space-sm)',
          }}>
            <span>in {message.tokenUsage.inputTokens}</span>
            <span>out {message.tokenUsage.outputTokens}</span>
            {!!message.tokenUsage.cachedInputTokens && message.tokenUsage.cachedInputTokens > 0 && (
              <span>cached {message.tokenUsage.cachedInputTokens}</span>
            )}
          </div>
        )}
      </div>
    </div>
  );
}
