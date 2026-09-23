import { describe, expect, it } from 'vitest';
import examples from '../contract/examples.json';
import { call, chatBody, makeEnv, ORIGIN, responsesOutput } from './helpers';

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
    expect(post.json).toEqual({ error: 'forbidden' });
    expect(options.json).toEqual({ error: 'forbidden' });
    expect(post.headers.get('Access-Control-Allow-Origin')).toBeNull();
    expect(options.headers.get('Access-Control-Allow-Origin')).toBeNull();
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

describe('削除した旧 API（Claude への素通し）', () => {
  // 以前の古いビルドが送っていた Claude Messages API 形式の本文
  const legacyBody = { model: 'claude-sonnet-5', max_tokens: 1024, messages: [{ role: 'user', content: 'a' }], stream: false };

  // 以前の設定値（LEGACY_CLAUDE_PASSTHROUGH: "true"）がダッシュボードなどに残っていても効かないことを確かめる
  const envWithOldSetting = () => ({ ...makeEnv(async () => responsesOutput('ok')), LEGACY_CLAUDE_PASSTHROUGH: 'true' });

  it.each([
    ['ブラウザ（許可された Origin）', ORIGIN],
    ['curl など（Origin ヘッダなし）', null],
  ])('POST / は %s からでも 404 で、Claude も Workers AI も呼ばない（Claude の API キーを使う中継を誰にも使わせない）', async (_label, origin) => {
    const env = envWithOldSetting();
    const res = await call(env, '/', legacyBody, { origin, headers: { 'anthropic-version': '2023-06-01' } });
    expect(res.status).toBe(404);
    expect(res.json).toEqual({ error: 'not_found' });
    expect(res.fetch).not.toHaveBeenCalled();
    expect(env.AI.run).not.toHaveBeenCalled();
  });

  it('/ へのプリフライトも 404 で、許可メソッド・許可ヘッダを返さず、CORS の Allow-Origin だけ付ける', async () => {
    const res = await call(envWithOldSetting(), '/', undefined, { method: 'OPTIONS' });
    expect(res.status).toBe(404);
    expect(res.json).toEqual({ error: 'not_found' });
    expect(res.headers.get('Access-Control-Allow-Methods')).toBeNull();
    expect(res.headers.get('Access-Control-Allow-Headers')).toBeNull();
    expect(res.headers.get('Access-Control-Allow-Origin')).toBe(ORIGIN);
  });

  it.each(['/chat', '/score'])('%s のプリフライトは Content-Type だけを許可する（以前の設定値が残っていても anthropic-version は許可しない）', async (path) => {
    const res = await call(envWithOldSetting(), path, undefined, { method: 'OPTIONS' });
    expect(res.status).toBe(204);
    expect(res.headers.get('Access-Control-Allow-Headers')).toBe('Content-Type');
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
