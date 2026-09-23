// Worker 全体で使う型。

// Workers AI バインディングのうち、このプロキシが使う部分だけ。
// テストではこの形のフェイクを env.AI に入れる。
export interface AiBinding {
  run(model: string, input: unknown): Promise<unknown>;
}

// wrangler.jsonc の vars と secret。vars はすべて文字列で渡ってくる。
export interface Env {
  AI: AiBinding;
  LLM_PROVIDER?: string;
  CHAT_MODEL?: string;
  CHAT_REASONING_EFFORT?: string;
  CHAT_MAX_OUTPUT_TOKENS?: string;
  SCORE_MODEL?: string;
  CLAUDE_MODEL?: string;
  CLAUDE_API_KEY?: string;
  ALLOWED_ORIGINS?: string;
}

export type Role = 'user' | 'assistant';

export interface ChatMessage {
  role: Role;
  content: string;
}

export interface ChatRequest {
  system: string;
  messages: ChatMessage[];
}

export interface ScoreRequest {
  prompt: string;
}

// クライアントに返すエラーの種別。応答本文は { error: 種別 } だけにする。
export type ErrorKind = 'daily_quota' | 'busy' | 'invalid_score' | 'upstream' | 'bad_request' | 'config' | 'not_found' | 'forbidden' | 'method_not_allowed';

// 処理の途中で投げ、ルーティングの層で応答に変換する。
export class ProxyError extends Error {
  constructor(public readonly kind: ErrorKind) {
    super(kind);
  }
}

// Claude への呼び出しに使う fetch。テストで差し替える。
export type FetchLike = (input: string, init: RequestInit) => Promise<Response>;

// プロバイダの共通インタフェース。
export interface Provider {
  chat(request: ChatRequest): Promise<string>;
  // 1 回分の問い合わせ。解釈できる値が返らなければ null（再試行は呼び出し側が決める）。
  scoreOnce(request: ScoreRequest): Promise<unknown>;
}
