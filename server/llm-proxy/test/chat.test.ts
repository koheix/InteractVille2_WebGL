import { describe, expect, it } from 'vitest';
import { call, chatBody, claudeResponse, makeEnv, responsesOutput } from './helpers';

describe('POST /chat（Workers AI）', () => {
  it('推論の文章を混ぜず、本文だけを text として返す', async () => {
    const env = makeEnv(async () => responsesOutput('こんにちは！'));
    const res = await call(env, '/chat', chatBody());
    expect(res.status).toBe(200);
    expect(res.json).toEqual({ text: 'こんにちは！' });
    expect(res.text).not.toContain('Need friendly');
  });

  it('本文が複数あれば output_text だけを出現順に連結し、refusal は含めない', async () => {
    const env = makeEnv(async () => ({
      status: 'completed',
      output: [
        { type: 'message', content: [{ type: 'output_text', text: 'やあ、' }, { type: 'refusal', refusal: '拒否文' }] },
        { type: 'reasoning', content: [{ type: 'reasoning_text', text: 'thinking' }] },
        { type: 'message', content: [{ type: 'output_text', text: '元気？' }] },
      ],
    }));
    const res = await call(env, '/chat', chatBody());
    expect(res.json).toEqual({ text: 'やあ、元気？' });
  });

  it('モデル・推論の強さ・トークン数は設定の値を数値型で渡し、クライアントの余分な項目は渡さない', async () => {
    const env = makeEnv(async () => responsesOutput('ok'));
    await call(env, '/chat', chatBody({ model: '@cf/other/expensive', max_output_tokens: 99999, stream: true }));
    expect(env.AI.run).toHaveBeenCalledTimes(1);
    expect(env.AI.run).toHaveBeenCalledWith('@cf/openai/gpt-oss-120b', {
      instructions: 'あなたは親しみやすい友達のハムスターです。',
      input: [{ role: 'user', content: 'こんにちは！' }],
      reasoning: { effort: 'low' },
      max_output_tokens: 1024,
    });
  });

  it('wrangler の vars を変えれば、Workers AI に渡すモデル・推論の強さ・トークン数（数値型）も変わる', async () => {
    const env = makeEnv(async () => responsesOutput('ok'), {
      CHAT_MODEL: '@cf/test/chat',
      CHAT_REASONING_EFFORT: 'medium',
      CHAT_MAX_OUTPUT_TOKENS: '2048',
    });
    await call(env, '/chat', chatBody());
    expect(env.AI.run).toHaveBeenCalledWith('@cf/test/chat', expect.objectContaining({ reasoning: { effort: 'medium' }, max_output_tokens: 2048 }));
  });

  it('messages に余分な項目があっても role と content だけを渡す', async () => {
    const env = makeEnv(async () => responsesOutput('ok'));
    await call(env, '/chat', chatBody({ messages: [{ role: 'user', content: 'a', name: 'x' }] }));
    expect(env.AI.run.mock.calls[0]?.[1]).toMatchObject({ input: [{ role: 'user', content: 'a' }] });
    expect((env.AI.run.mock.calls[0]?.[1] as { input: object[] }).input[0]).toEqual({ role: 'user', content: 'a' });
  });

  it.each([
    ['推論の途中で打ち切られ本文が無い', { status: 'incomplete', output: [{ type: 'reasoning', content: [] }] }],
    ['打ち切られたが部分的な本文がある', { ...responsesOutput('途中まで'), status: 'incomplete' }],
    ['本文が空文字', responsesOutput('')],
    ['本文が空白と改行だけ', responsesOutput('   \n')],
    ['output が空配列', { status: 'completed', output: [] }],
    ['output が無い', { status: 'completed' }],
    ['null', null],
  ])('使える本文が無い応答（%s）は 502 upstream（500 にならない）', async (_label, output) => {
    const res = await call(makeEnv(async () => output), '/chat', chatBody());
    expect(res.status).toBe(502);
    expect(res.json).toEqual({ error: 'upstream' });
  });

  it('無料枠の超過（3036）は 429 daily_quota', async () => {
    const env = makeEnv(async () => {
      throw new Error('3036: You have used up your daily free allocation of 10,000 neurons.');
    });
    const res = await call(env, '/chat', chatBody());
    expect(res.status).toBe(429);
    expect(res.json).toEqual({ error: 'daily_quota' });
  });

  it('一時的な混雑（3040）は 503 busy', async () => {
    const env = makeEnv(async () => {
      throw new Error('3040: Capacity temporarily exceeded, please try again.');
    });
    const res = await call(env, '/chat', chatBody());
    expect(res.status).toBe(503);
    expect(res.json).toEqual({ error: 'busy' });
  });

  it('例外の文言（キーなど）を応答本文に含めない', async () => {
    const env = makeEnv(async () => {
      throw new Error('9999: internal key=sk-secret-123');
    });
    const res = await call(env, '/chat', chatBody());
    expect(res.status).toBe(502);
    expect(res.text).toBe('{"error":"upstream"}');
  });

  it('設定が不正なら 500 config を返し、Workers AI を呼ばない', async () => {
    const env = makeEnv(async () => responsesOutput('ok'), { LLM_PROVIDER: 'Claude' });
    const res = await call(env, '/chat', chatBody());
    expect(res.status).toBe(500);
    expect(res.json).toEqual({ error: 'config' });
    expect(env.AI.run).not.toHaveBeenCalled();
  });
});

describe('POST /chat（Claude に切り戻したとき）', () => {
  const claudeEnv = () => makeEnv(async () => responsesOutput('workers-ai'), { LLM_PROVIDER: 'claude' });

  it('Claude の text ブロックを連結して返し、キー・バージョン・固定のモデルを付けて送る', async () => {
    const env = claudeEnv();
    const res = await call(env, '/chat', chatBody({ model: 'claude-opus-x', stream: true }), {
      fetch: async () => claudeResponse({ content: [{ type: 'text', text: 'あ' }, { type: 'tool_use' }, { type: 'text', text: 'い' }] }),
    });
    expect(res.status).toBe(200);
    expect(res.json).toEqual({ text: 'あい' });
    expect(env.AI.run).not.toHaveBeenCalled();

    const [url, init] = res.fetch.mock.calls[0] as [string, RequestInit];
    expect(url).toBe('https://api.anthropic.com/v1/messages');
    expect(init.headers).toMatchObject({ 'x-api-key': 'sk-test-key', 'anthropic-version': '2023-06-01' });
    expect(JSON.parse(init.body as string)).toEqual({
      model: 'claude-sonnet-4-20250514',
      max_tokens: 1024,
      system: 'あなたは親しみやすい友達のハムスターです。',
      messages: [{ role: 'user', content: 'こんにちは！' }],
    });
  });

  it.each([
    [429, 503, 'busy'],
    [529, 503, 'busy'],
    [401, 502, 'upstream'],
    [400, 502, 'upstream'],
    [500, 502, 'upstream'],
  ])('Claude が HTTP %i を返したら %i %s（上流の本文は返さない）', async (upstream, status, kind) => {
    const res = await call(claudeEnv(), '/chat', chatBody(), {
      fetch: async () => claudeResponse({ error: { message: 'invalid x-api-key sk-secret' } }, upstream),
    });
    expect(res.status).toBe(status);
    expect(res.text).toBe(`{"error":"${kind}"}`);
  });

  it.each([
    ['fetch が失敗した', async () => Promise.reject(new Error('network down'))],
    ['200 で HTML が返った', async () => claudeResponse('<html>error</html>')],
    ['200 で content が空だった', async () => claudeResponse({ content: [] })],
  ])('%s ときは 502 upstream', async (_label, fetchImpl) => {
    const res = await call(claudeEnv(), '/chat', chatBody(), { fetch: fetchImpl as never });
    expect(res.status).toBe(502);
    expect(res.json).toEqual({ error: 'upstream' });
  });

  it('CLAUDE_API_KEY が無ければ Claude を呼ばずに 500 config', async () => {
    const env = makeEnv(async () => ({}), { LLM_PROVIDER: 'claude', CLAUDE_API_KEY: undefined });
    const res = await call(env, '/chat', chatBody());
    expect(res.status).toBe(500);
    expect(res.json).toEqual({ error: 'config' });
    expect(res.fetch).not.toHaveBeenCalled();
  });
});
