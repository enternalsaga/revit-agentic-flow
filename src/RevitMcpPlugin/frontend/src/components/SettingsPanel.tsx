import { useState } from 'react';
import { AiSettings } from '../types';

interface Props {
  settings: AiSettings | null;
  onSave: (settings: AiSettings) => void;
}

const PROVIDER_LABELS: Record<string, string> = {
  anthropic: 'Anthropic',
  openai: 'OpenAI',
  gemini: 'Google Gemini',
  deepseek: 'DeepSeek',
  glm: 'GLM (Z.AI)',
  openrouter: 'OpenRouter',
};

export default function SettingsPanel({ settings, onSave }: Props) {
  const [localSettings, setLocalSettings] = useState<AiSettings>(
    settings || {
      activeModel: 'claude-sonnet-4-6',
      providers: {},
    }
  );

  const updateApiKey = (providerId: string, key: string) => {
    setLocalSettings(prev => ({
      ...prev,
      providers: {
        ...prev.providers,
        [providerId]: { ...prev.providers[providerId], apiKey: key } as any,
      },
    }));
  };

  const selectModel = (modelId: string) => {
    setLocalSettings(prev => ({ ...prev, activeModel: modelId }));
  };

  const allModels = Object.entries(localSettings.providers).flatMap(([id, p]) =>
    p.models.map(m => ({ ...m, providerId: id, hasKey: !!p.apiKey }))
  );

  return (
    <div style={{ flex: 1, overflow: 'auto', padding: 'var(--space-lg)' }}>
      <h3 style={{ fontSize: 'var(--text-lg)', marginBottom: 'var(--space-lg)', color: 'var(--color-text-primary)' }}>
        AI Provider Settings
      </h3>

      {/* Model selector */}
      <div style={{ marginBottom: 'var(--space-xl)' }}>
        <label style={{ display: 'block', fontSize: 'var(--text-sm)', color: 'var(--color-text-secondary)', marginBottom: 'var(--space-xs)' }}>
          Active Model
        </label>
        <select
          value={localSettings.activeModel}
          onChange={(e) => selectModel(e.target.value)}
          style={{
            width: '100%', padding: 'var(--space-sm)', background: 'var(--color-bg-input)',
            color: 'var(--color-text-primary)', border: '1px solid var(--color-border)',
            borderRadius: 'var(--radius-sm)', fontSize: 'var(--text-sm)',
          }}
        >
          {allModels.map(m => (
            <option key={m.id} value={m.id} disabled={!m.hasKey}>
              {m.label} ({PROVIDER_LABELS[m.providerId] || m.providerId})
              {!m.hasKey ? ' — no API key' : ''}
            </option>
          ))}
        </select>
      </div>

      {/* Provider API keys */}
      <div style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-lg)' }}>
        {Object.entries(localSettings.providers).map(([id, provider]) => (
          <div key={id}>
            <label style={{ display: 'block', fontSize: 'var(--text-sm)', color: 'var(--color-text-secondary)', marginBottom: 'var(--space-xs)' }}>
              {PROVIDER_LABELS[id] || id} API Key
            </label>
            <input
              type="password"
              value={provider.apiKey}
              onChange={(e) => updateApiKey(id, e.target.value)}
              placeholder="Enter API key..."
              style={{
                width: '100%', padding: 'var(--space-sm)', background: 'var(--color-bg-input)',
                color: 'var(--color-text-primary)', border: '1px solid var(--color-border)',
                borderRadius: 'var(--radius-sm)', fontSize: 'var(--text-sm)',
              }}
            />
          </div>
        ))}
      </div>

      <div style={{ marginTop: 'var(--space-xl)', paddingTop: 'var(--space-lg)', borderTop: '1px solid var(--color-border)' }}>
        <button
          onClick={() => onSave(localSettings)}
          style={{
            background: 'var(--color-accent)', color: '#fff', border: 'none',
            borderRadius: 'var(--radius-sm)', padding: 'var(--space-sm) var(--space-xl)',
            cursor: 'pointer', fontWeight: 600, fontSize: 'var(--text-sm)',
          }}
        >
          Save & Return to Chat
        </button>
      </div>
    </div>
  );
}
