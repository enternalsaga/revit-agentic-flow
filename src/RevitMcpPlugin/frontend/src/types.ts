export interface ChatMessage {
  id: string;
  text: string;
  isUser: boolean;
  toolCalls?: ToolCallInfo[];
  tokenUsage?: TokenUsage;
}

export interface ToolCallInfo {
  name: string;
  result?: string;
}

export interface TokenUsage {
  inputTokens: number;
  outputTokens: number;
  cachedInputTokens?: number;
  cacheCreationInputTokens?: number;
  sessionInputTokens?: number;
  sessionOutputTokens?: number;
  sessionCachedInputTokens?: number;
  sessionCacheHitRatio?: number;
}

export interface ProviderInfo {
  id: string;
  protocol: string;
  baseUrl: string;
  apiKey: string;
  models: ModelInfo[];
  headers?: Record<string, string>;
}

export interface ModelInfo {
  id: string;
  label: string;
  speed: string;
}

export interface AiSettings {
  activeModel: string;
  providers: Record<string, ProviderInfo>;
}

export type ViewMode = 'chat' | 'settings';
