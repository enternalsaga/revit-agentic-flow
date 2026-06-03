import { useState } from 'react';

interface Props {
  code: string;
  language?: string;
}

export default function CodeBlock({ code, language }: Props) {
  const [copied, setCopied] = useState(false);

  const handleCopy = () => {
    navigator.clipboard.writeText(code);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  return (
    <div style={{ position: 'relative', margin: 'var(--space-xs) 0' }}>
      <div style={{
        display: 'flex', justifyContent: 'space-between', alignItems: 'center',
        background: 'var(--color-bg-code)', padding: 'var(--space-xs) var(--space-sm)',
        borderRadius: 'var(--radius-sm) var(--radius-sm) 0 0',
        fontSize: 'var(--text-xs)', color: 'var(--color-text-muted)',
      }}>
        <span>{language || 'code'}</span>
        <button
          onClick={handleCopy}
          style={{
            background: 'none', border: 'none', color: 'var(--color-text-secondary)',
            cursor: 'pointer', fontSize: 'var(--text-xs)',
          }}
        >
          {copied ? 'Copied!' : 'Copy'}
        </button>
      </div>
      <pre style={{
        background: 'var(--color-bg-code)', padding: 'var(--space-sm)',
        borderRadius: '0 0 var(--radius-sm) var(--radius-sm)',
        overflow: 'auto', fontSize: 'var(--text-xs)', lineHeight: 'var(--leading-normal)',
        margin: 0,
      }}>
        <code>{code}</code>
      </pre>
    </div>
  );
}
