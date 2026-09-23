// リクエスト本文の検証。上限値は contract/limits.json を Unity と共有する。
//
// クライアントが余分な項目（model、max_tokens、stream など）を付けてきても、
// ここで必要な項目だけを取り出すので、プロバイダには渡らない。

import limits from '../contract/limits.json';
import { type ChatMessage, type ChatRequest, ProxyError, type ScoreRequest } from './types';

export const LIMITS = {
  systemMaxChars: limits.systemMaxChars,
  messagesMaxCount: limits.messagesMaxCount,
  messagesMaxTotalChars: limits.messagesMaxTotalChars,
  promptMaxChars: limits.promptMaxChars,
  bodyMaxBytes: limits.bodyMaxBytes,
} as const;

function isPlainObject(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

export function parseChatRequest(body: unknown): ChatRequest {
  if (!isPlainObject(body)) throw new ProxyError('bad_request');

  const { system, messages } = body;
  if (typeof system !== 'string' || system.length > LIMITS.systemMaxChars) throw new ProxyError('bad_request');
  if (!Array.isArray(messages) || messages.length === 0 || messages.length > LIMITS.messagesMaxCount) {
    throw new ProxyError('bad_request');
  }

  let total = 0;
  const parsed: ChatMessage[] = messages.map((message) => {
    if (!isPlainObject(message)) throw new ProxyError('bad_request');
    const { role, content } = message;
    // system ロールは受け付けない（system を上書きされないように）。
    if (role !== 'user' && role !== 'assistant') throw new ProxyError('bad_request');
    if (typeof content !== 'string') throw new ProxyError('bad_request');
    total += content.length;
    return { role, content };
  });
  if (total > LIMITS.messagesMaxTotalChars) throw new ProxyError('bad_request');

  return { system, messages: parsed };
}

export function parseScoreRequest(body: unknown): ScoreRequest {
  if (!isPlainObject(body)) throw new ProxyError('bad_request');
  const { prompt } = body;
  if (typeof prompt !== 'string' || prompt.trim() === '' || prompt.length > LIMITS.promptMaxChars) {
    throw new ProxyError('bad_request');
  }
  return { prompt };
}

// 本文を JSON として読む。大きすぎる本文と JSON でない本文は bad_request。
export async function readJsonBody(request: Request): Promise<unknown> {
  const declared = request.headers.get('Content-Length');
  if (declared !== null && Number(declared) > LIMITS.bodyMaxBytes) throw new ProxyError('bad_request');

  const text = await request.text();
  if (new TextEncoder().encode(text).length > LIMITS.bodyMaxBytes) throw new ProxyError('bad_request');
  try {
    return JSON.parse(text);
  } catch {
    throw new ProxyError('bad_request');
  }
}
