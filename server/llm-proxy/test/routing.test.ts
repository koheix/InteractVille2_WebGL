import { describe, expect, it } from 'vitest';
import examples from '../contract/examples.json';
import { call, chatBody, claudeResponse, makeEnv, ORIGIN, responsesOutput } from './helpers';

describe('CORS', () => {
  it('許可された Origin のプリフライトには、許可ヘッダと Vary: Origin を返す', async () => {
    const res = await call(makeEnv(), '/chat', undefined, { method: 'OPTIONS' });
    expect(res.status).toBe(204);
    expect(res.headers.get('Access-Control-Allow-Origin')).toBe(ORIGIN);
    expect(res.headers.get('Access-Control-Allow-Methods')).toContain('POST');
    expect(res.headers.get('Access-Control-Allow-Headers')).toContain('Content-Type');
    expect(res.headers.get('Vary')).toBe('Origin');
  });

  it.each([
    ['無料枠の超過', async () => Promise.reject(new Error('3036: daily')), chatBody(), 429],
    ['不正な本文', async () => ({}), 'not json', 400],
    ['AI の想定外の例外', async () => Promise.reject(new TypeError('boom')), chatBody(), 502],
  ])('エラー応答（%s）にも Allow-Origin を付ける（無いとブラウザが応答を捨てる）', async (_label, run, body, status) => {
    const res = await call(makeEnv(run), '/chat', body);
    expect(res.status).toBe(status);
    expect(res.headers.get('Access-Control-Allow-Origin')).toBe(ORIGIN);
  });

  it('処理中の想定外の例外（プロバイダ以外）は 500 upstream を返し、例外の文言を出さず、Allow-Origin を付ける', async () => {
    const env = makeEnv(async () => responsesOutput('ok'));
    Object.defineProperty(env, 'LLM_PROVIDER', {
      get() {
        throw new TypeError('secret detail');
      },
    });
    const res = await call(env, '/chat', chatBody());
    expect(res.status).toBe(500);
    expect(res.text).toBe('{"error":"upstream"}');
    expect(res.headers.get('Access-Control-Allow-Origin')).toBe(ORIGIN);
  });

  it.each([
    'https://koheix.github.io.evil.com',
    'http://koheix.github.io',
    'https://KOHEIX.github.io',
    'null',
    'https://evil.com',
  ])('許可されていない Origin（%s）は POST も OPTIONS も 403 で、Allow-Origin を付けず、AI を呼ばない', async (origin) => {
    const env = makeEnv(async () => responsesOutput('ok'));
    const post = await call(env, '/chat', chatBody(), { origin });
    const options = await call(env, '/chat', undefined, { origin, method: 'OPTIONS' });
    expect(post.status).toBe(403);
    expect(options.status).toBe(403);
    expect(post.headers.get('Access-Control-Allow-Origin')).toBeNull();
    expect(env.AI.run).not.toHaveBeenCalled();
  });

  it('Origin ヘッダが無い呼び出し（Unity エディタ）は通し、Allow-Origin は付けない', async () => {
    const res = await call(makeEnv(async () => responsesOutput('ok')), '/chat', chatBody(), { origin: null });
    expect(res.status).toBe(200);
    expect(res.headers.get('Access-Control-Allow-Origin')).toBeNull();
  });
});

describe('ルーティング', () => {
  it.each([
    ['GET', '/chat', 405],
    ['PUT', '/score', 405],
    ['POST', '/chat/', 404],
    ['POST', '/unknown', 404],
  ])('%s %s は %i で、AI を呼ばない', async (method, path, status) => {
    const env = makeEnv(async () => responsesOutput('ok'));
    const res = await call(env, path, method === 'GET' ? undefined : chatBody(), { method });
    expect(res.status).toBe(status);
    expect(env.AI.run).not.toHaveBeenCalled();
  });

  it('クエリ文字列は無視して /chat として扱う', async () => {
    const res = await call(makeEnv(async () => responsesOutput('ok')), '/chat?x=1', chatBody());
    expect(res.status).toBe(200);
  });
});

describe('旧 API（公開中の古いビルド用）', () => {
  const legacyBody = { model: 'claude-sonnet-4-20250514', max_tokens: 1024, messages: [{ role: 'user', content: 'a' }], tools: [], stream: true };

  it('有効なときは本文を stream=false にしてそのまま Claude に中継し、応答をそのまま返す', async () => {
    const upstream = { id: 'msg_1', content: [{ type: 'text', text: 'やあ' }] };
    const res = await call(makeEnv(), '/', legacyBody, { fetch: async () => claudeResponse(upstream, 200) });
    expect(res.status).toBe(200);
    expect(res.json).toEqual(upstream);
    expect(res.headers.get('Access-Control-Allow-Origin')).toBe(ORIGIN);
    const [url, init] = res.fetch.mock.calls[0] as [string, RequestInit];
    expect(url).toBe('https://api.anthropic.com/v1/messages');
    expect(init.headers).toMatchObject({ 'x-api-key': 'sk-test-key' });
    expect(JSON.parse(init.body as string)).toEqual({ ...legacyBody, stream: false });
  });

  it('有効なときのプリフライトは anthropic-version ヘッダを許可する（古いビルドが付けて送るため）', async () => {
    const res = await call(makeEnv(), '/', undefined, { method: 'OPTIONS' });
    expect(res.status).toBe(204);
    expect(res.headers.get('Access-Control-Allow-Headers')).toContain('anthropic-version');
  });

  it.each(['false', 'TRUE', '1', undefined])('LEGACY_CLAUDE_PASSTHROUGH=%j なら 404 で、Claude を呼ばず、CORS ヘッダは付ける', async (value) => {
    const res = await call(makeEnv(undefined, { LEGACY_CLAUDE_PASSTHROUGH: value }), '/', legacyBody);
    expect(res.status).toBe(404);
    expect(res.fetch).not.toHaveBeenCalled();
    expect(res.headers.get('Access-Control-Allow-Origin')).toBe(ORIGIN);
  });
});

describe('Unity との契約（contract/examples.json）', () => {
  it('契約の会話リクエストを受け付け、契約どおりの形（text）で返す', async () => {
    const env = makeEnv(async () => responsesOutput(examples.chatSuccess.body.text));
    const res = await call(env, '/chat', examples.chatRequest);
    expect(res.status).toBe(examples.chatSuccess.status);
    expect(res.json).toEqual(examples.chatSuccess.body);
  });

  it('契約のスコアリクエストを受け付け、契約どおりの形（result）で返す', async () => {
    const env = makeEnv(async () => ({ response: { result: examples.scoreSuccess.body.result } }));
    const res = await call(env, '/score', examples.scoreRequest);
    expect(res.status).toBe(examples.scoreSuccess.status);
    expect(res.json).toEqual(examples.scoreSuccess.body);
  });

  it('契約のエラー例は、Worker が実際に返すステータスと種別の組み合わせと一致する', async () => {
    const produce: Record<string, () => Promise<{ status: number; json: unknown }>> = {
      daily_quota: () => call(makeEnv(async () => Promise.reject(new Error('3036: x'))), '/chat', chatBody()),
      busy: () => call(makeEnv(async () => Promise.reject(new Error('3040: x'))), '/chat', chatBody()),
      invalid_score: () => call(makeEnv(async () => ({ response: {} })), '/score', { prompt: 'p' }),
      upstream: () => call(makeEnv(async () => ({ status: 'completed', output: [] })), '/chat', chatBody()),
      bad_request: () => call(makeEnv(), '/chat', 'x'),
      config: () => call(makeEnv(undefined, { LLM_PROVIDER: 'x' }), '/chat', chatBody()),
    };
    for (const example of examples.errors) {
      const res = await produce[example.body.error]!();
      expect({ status: res.status, body: res.json }).toEqual(example);
    }
  });
});
