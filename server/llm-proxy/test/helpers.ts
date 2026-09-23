// テスト用のフェイクと呼び出しヘルパー。
import { type Mock, vi } from 'vitest';
import { createHandler } from '../src/index';
import type { Env, FetchLike } from '../src/types';

export const ORIGIN = 'https://koheix.github.io';

export type AiRun = (model: string, input: unknown) => Promise<unknown>;

export type FakeEnv = Omit<Env, 'AI'> & { AI: { run: Mock<AiRun> } };

export function makeEnv(run: AiRun = async () => ({}), overrides: Partial<Omit<Env, 'AI'>> = {}): FakeEnv {
  return {
    AI: { run: vi.fn<AiRun>(run) },
    LLM_PROVIDER: 'workers-ai',
    CHAT_MODEL: '@cf/openai/gpt-oss-120b',
    CHAT_REASONING_EFFORT: 'low',
    CHAT_MAX_OUTPUT_TOKENS: '1024',
    SCORE_MODEL: '@cf/meta/llama-3.3-70b-instruct-fp8-fast',
    CLAUDE_MODEL: 'claude-sonnet-5',
    CLAUDE_API_KEY: 'sk-test-key',
    ALLOWED_ORIGINS: ORIGIN,
    ...overrides,
  };
}

// gpt-oss の Responses API 形式の応答（推論 + 本文）。
export function responsesOutput(text: string, extra: Record<string, unknown> = {}) {
  return {
    status: 'completed',
    output: [
      { type: 'reasoning', content: [{ type: 'reasoning_text', text: 'Need friendly hamster reply.' }] },
      { type: 'message', role: 'assistant', content: [{ type: 'output_text', text }] },
    ],
    ...extra,
  };
}

export function claudeResponse(body: unknown, status = 200): Response {
  return new Response(typeof body === 'string' ? body : JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  });
}

export interface CallOptions {
  method?: string;
  origin?: string | null;
  headers?: Record<string, string>;
  fetch?: FetchLike;
}

// ハンドラを呼び、ステータス・ヘッダ・本文（JSON なら解析済み）を返す。
export async function call(env: Env, path: string, body?: unknown, options: CallOptions = {}) {
  const fetchFn = vi.fn(options.fetch ?? (async () => claudeResponse({ content: [{ type: 'text', text: 'ok' }] })));
  const headers: Record<string, string> = { ...options.headers };
  const origin = options.origin === undefined ? ORIGIN : options.origin;
  if (origin !== null) headers.Origin = origin;
  if (body !== undefined) headers['Content-Type'] = 'application/json';

  const request = new Request(`https://llm-proxy.example.workers.dev${path}`, {
    method: options.method ?? 'POST',
    headers,
    body: body === undefined ? undefined : typeof body === 'string' ? body : JSON.stringify(body),
  });
  const response = await createHandler({ fetch: fetchFn })(request, env);
  const text = await response.text();
  let json: unknown = null;
  try {
    json = JSON.parse(text);
  } catch {
    json = null;
  }
  return { status: response.status, headers: response.headers, text, json, fetch: fetchFn };
}

export const chatBody = (overrides: Record<string, unknown> = {}) => ({
  system: 'あなたは親しみやすい友達のハムスターです。',
  messages: [{ role: 'user', content: 'こんにちは！' }],
  ...overrides,
});
