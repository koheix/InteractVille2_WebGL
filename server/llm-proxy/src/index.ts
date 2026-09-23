// ともハムの会話用 LLM プロキシ。
//
//   POST /chat   { system, messages } → { text }       会話・記憶の要約・気分
//   POST /score  { prompt }           → { result }     親密度などのスコア（0〜100 の整数）
//
// 以前あった旧 API（POST / で Claude Messages API をそのまま中継）は、古いビルドが指定していた
// Claude のモデルが廃止されて使われなくなったので削除した。Claude の API キーを誰でも使える中継を残さないため。
//
// 失敗は { error: 種別 } で返す（種別と HTTP ステータスは errors.ts の ERROR_STATUS）。
// どの応答にも CORS ヘッダを付ける。付け忘れるとブラウザが応答を捨て、Unity からは
// 通信エラーにしか見えなくなる（「今日はもう話せない」が表示できない）。

import { readAllowedOrigins, readConfig } from './config';
import { ERROR_STATUS } from './errors';
import { createClaudeProvider, createWorkersAiProvider } from './providers';
import { normalizeScore } from './score';
import { type Env, type ErrorKind, type FetchLike, type Provider, ProxyError, type ScoreRequest } from './types';
import { parseChatRequest, parseScoreRequest, readJsonBody } from './validate';

export interface Deps {
  fetch: FetchLike;
}

type CorsHeaders = Record<string, string>;

// Origin の判定。完全一致だけを許可する（前方一致にすると https://koheix.github.io.evil.com が通る）。
// Origin ヘッダが無い呼び出し（Unity エディタ、curl）は許可し、CORS ヘッダは付けない。
// "null"（サンドボックス化された iframe や file://）はヘッダ無しとは扱わず、拒否する。
function resolveCors(request: Request, env: Env): { allowed: boolean; headers: CorsHeaders } {
  const origin = request.headers.get('Origin');
  if (origin === null) return { allowed: true, headers: {} };
  if (readAllowedOrigins(env).includes(origin)) {
    return { allowed: true, headers: { 'Access-Control-Allow-Origin': origin, Vary: 'Origin' } };
  }
  return { allowed: false, headers: { Vary: 'Origin' } };
}

function json(body: unknown, status: number, cors: CorsHeaders): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json', ...cors },
  });
}

function errorResponse(kind: ErrorKind, cors: CorsHeaders, status = ERROR_STATUS[kind]): Response {
  return json({ error: kind }, status, cors);
}

// スコアを求める。解釈できない値が返ったときだけ 1 回再試行する。
// 上流の失敗（無料枠の超過など）では再試行しない（枠を無駄に使うため）。
export async function score(provider: Provider, request: ScoreRequest): Promise<number> {
  for (let attempt = 0; attempt < 2; attempt++) {
    const value = normalizeScore(await provider.scoreOnce(request));
    if (value !== null) return value;
  }
  throw new ProxyError('invalid_score');
}

function createProvider(env: Env, deps: Deps): Provider {
  const config = readConfig(env);
  return config.provider === 'claude' ? createClaudeProvider(deps.fetch, config) : createWorkersAiProvider(env.AI, config);
}

const ROUTES = new Set(['/chat', '/score']);

export function createHandler(deps: Deps) {
  return async function handle(request: Request, env: Env): Promise<Response> {
    const cors = resolveCors(request, env);
    const path = new URL(request.url).pathname;

    if (!cors.allowed) return errorResponse('forbidden', cors.headers);
    if (!ROUTES.has(path)) return errorResponse('not_found', cors.headers);

    if (request.method === 'OPTIONS') {
      return new Response(null, {
        status: 204,
        headers: {
          ...cors.headers,
          'Access-Control-Allow-Methods': 'POST, OPTIONS',
          'Access-Control-Allow-Headers': 'Content-Type',
          'Access-Control-Max-Age': '86400',
        },
      });
    }
    if (request.method !== 'POST') return errorResponse('method_not_allowed', cors.headers);

    try {
      const body = await readJsonBody(request);
      if (path === '/chat') {
        const chatRequest = parseChatRequest(body);
        const text = await createProvider(env, deps).chat(chatRequest);
        return json({ text }, 200, cors.headers);
      }
      const scoreRequest = parseScoreRequest(body);
      const result = await score(createProvider(env, deps), scoreRequest);
      return json({ result }, 200, cors.headers);
    } catch (error) {
      if (error instanceof ProxyError) return errorResponse(error.kind, cors.headers);
      console.error('Unexpected error:', error instanceof Error ? error.message : String(error));
      return errorResponse('upstream', cors.headers, 500);
    }
  };
}

const handle = createHandler({ fetch: (input, init) => fetch(input, init) });

export default {
  fetch: (request: Request, env: Env): Promise<Response> => handle(request, env),
};
