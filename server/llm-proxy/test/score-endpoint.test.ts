import { describe, expect, it } from 'vitest';
import { call, claudeResponse, makeEnv } from './helpers';

const scoreBody = { prompt: '以下の会話データから感情価を算出してください。' };

// 呼ばれるたびに順に値を返す（最後の値を繰り返す）Workers AI のフェイク。
const sequence = (...results: Array<unknown | Error>) => {
  let index = 0;
  return async () => {
    const value = results[Math.min(index, results.length - 1)];
    index++;
    if (value instanceof Error) throw value;
    return value;
  };
};

describe('POST /score（Workers AI）', () => {
  it('JSON Mode のスキーマ・固定のトークン数・スコア用モデルで問い合わせ、整数を返す', async () => {
    const env = makeEnv(async () => ({ response: { result: 72 } }));
    const res = await call(env, '/score', { ...scoreBody, model: '@cf/other', max_tokens: 9999 });
    expect(res.status).toBe(200);
    expect(res.json).toEqual({ result: 72 });
    expect(env.AI.run).toHaveBeenCalledTimes(1);
    expect(env.AI.run).toHaveBeenCalledWith('@cf/meta/llama-3.3-70b-instruct-fp8-fast', {
      messages: [{ role: 'user', content: scoreBody.prompt }],
      response_format: {
        type: 'json_schema',
        json_schema: { type: 'object', properties: { result: { type: 'number', description: '0から100の数値' } }, required: ['result'] },
      },
      max_tokens: 64,
    });
  });

  it('response が JSON 文字列で返っても解釈する', async () => {
    const res = await call(makeEnv(async () => ({ response: ' {"result": 38} ' })), '/score', scoreBody);
    expect(res.json).toEqual({ result: 38 });
  });

  it('小数・範囲外は四捨五入して 0〜100 の整数で返す', async () => {
    const res = await call(makeEnv(async () => ({ response: { result: 100.5 } })), '/score', scoreBody);
    expect(res.json).toEqual({ result: 100 });
  });

  it('1 回目が解釈できず 2 回目が有効なら、ちょうど 2 回問い合わせてその値を返す', async () => {
    const env = makeEnv(sequence({ response: { Valence: 80 } }, { response: { result: 40 } }));
    const res = await call(env, '/score', scoreBody);
    expect(res.json).toEqual({ result: 40 });
    expect(env.AI.run).toHaveBeenCalledTimes(2);
  });

  it.each([
    ['result が null', { response: { result: null } }],
    ['result が真偽値', { response: { result: true } }],
    ['result が空文字', { response: { result: '' } }],
    ['result が無い', { response: {} }],
    ['response が無い', {}],
  ])('2 回とも解釈できない（%s）なら 502 invalid_score を返し、0 や 1 を返さない', async (_label, output) => {
    const env = makeEnv(async () => output);
    const res = await call(env, '/score', scoreBody);
    expect(res.status).toBe(502);
    expect(res.json).toEqual({ error: 'invalid_score' });
    expect(env.AI.run).toHaveBeenCalledTimes(2);
  });

  it('無料枠の超過（3036）では再試行せず、1 回だけ呼んで 429 daily_quota', async () => {
    const env = makeEnv(sequence(new Error('3036: daily free allocation')));
    const res = await call(env, '/score', scoreBody);
    expect(res.status).toBe(429);
    expect(res.json).toEqual({ error: 'daily_quota' });
    expect(env.AI.run).toHaveBeenCalledTimes(1);
  });

  it('1 回目が解釈できず、再試行が 3036 になったら invalid_score ではなく daily_quota', async () => {
    const env = makeEnv(sequence({ response: {} }, new Error('3036: daily free allocation')));
    const res = await call(env, '/score', scoreBody);
    expect(res.status).toBe(429);
    expect(res.json).toEqual({ error: 'daily_quota' });
  });

  it('1 回目が解釈できず、再試行が 3040 になったら 503 busy', async () => {
    const env = makeEnv(sequence({ response: {} }, new Error('3040: Capacity temporarily exceeded')));
    const res = await call(env, '/score', scoreBody);
    expect(res.status).toBe(503);
    expect(res.json).toEqual({ error: 'busy' });
  });
});

describe('POST /score（Claude に切り戻したとき）', () => {
  const claudeEnv = () => makeEnv(async () => ({ response: { result: 1 } }), { LLM_PROVIDER: 'claude' });
  const toolUse = (input: unknown) => claudeResponse({ content: [{ type: 'tool_use', name: 'return_score', input }] });

  it('return_score ツールを強制して呼び、input.result を整数で返す', async () => {
    const res = await call(claudeEnv(), '/score', scoreBody, { fetch: async () => toolUse({ result: '72' }) });
    expect(res.json).toEqual({ result: 72 });
    const body = JSON.parse((res.fetch.mock.calls[0] as [string, RequestInit])[1].body as string);
    expect(body.tool_choice).toEqual({ type: 'tool', name: 'return_score' });
    expect(body.tools[0].name).toBe('return_score');
    expect(body.model).toBe('claude-sonnet-5');
  });

  it('SCORE_MODEL を変えれば、そのモデルで問い合わせる', async () => {
    const env = makeEnv(async () => ({ response: { result: 10 } }), { SCORE_MODEL: '@cf/test/score' });
    await call(env, '/score', scoreBody);
    expect(env.AI.run.mock.calls[0]?.[0]).toBe('@cf/test/score');
  });

  it('Claude が 429 を返したら再試行せず、1 回だけ呼んで 503 busy', async () => {
    const res = await call(claudeEnv(), '/score', scoreBody, { fetch: async () => claudeResponse({ error: {} }, 429) });
    expect(res.status).toBe(503);
    expect(res.json).toEqual({ error: 'busy' });
    expect(res.fetch).toHaveBeenCalledTimes(1);
  });

  it('return_score 以外の名前の tool_use は読まず、無効として扱う', async () => {
    const res = await call(claudeEnv(), '/score', scoreBody, {
      fetch: async () => claudeResponse({ content: [{ type: 'tool_use', name: 'other_tool', input: { result: 90 } }] }),
    });
    expect(res.status).toBe(502);
    expect(res.json).toEqual({ error: 'invalid_score' });
  });

  it('tool_use が無い応答は 1 回だけ再試行し、それでも無ければ 502 invalid_score', async () => {
    const res = await call(claudeEnv(), '/score', scoreBody, {
      fetch: async () => claudeResponse({ content: [{ type: 'text', text: '72です' }] }),
    });
    expect(res.status).toBe(502);
    expect(res.json).toEqual({ error: 'invalid_score' });
    expect(res.fetch).toHaveBeenCalledTimes(2);
  });
});
