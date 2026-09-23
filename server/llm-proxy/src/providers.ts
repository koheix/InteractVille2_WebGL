// LLM プロバイダ（Workers AI / Claude）の呼び出し。
//
// どちらも ChatRequest / ScoreRequest を受け取り、会話は文字列、スコアは生の値を返す。
// 失敗は ProxyError（種別つき）で投げる。モデル名やトークン数は設定から決め、
// クライアントからは受け取らない。

import { CLAUDE_MAX_TOKENS, type Config, SCORE_MAX_TOKENS } from './config';
import { classifyClaudeStatus, classifyWorkersAiError } from './errors';
import { extractResult } from './score';
import { type AiBinding, type ChatRequest, type FetchLike, type Provider, ProxyError, type ScoreRequest } from './types';

export const CLAUDE_URL = 'https://api.anthropic.com/v1/messages';
export const CLAUDE_VERSION = '2023-06-01';

const SCORE_SCHEMA = {
  type: 'object',
  properties: { result: { type: 'number', description: '0から100の数値' } },
  required: ['result'],
} as const;

function asRecord(value: unknown): Record<string, unknown> | null {
  return typeof value === 'object' && value !== null && !Array.isArray(value) ? (value as Record<string, unknown>) : null;
}

// gpt-oss（Responses API 形式）の応答から、ともハムの台詞になる本文だけを取り出す。
//
// output には推論（type: "reasoning"、英語の思考過程）と本文（type: "message"）が混ざる。
// message の content のうち output_text だけを出現順に連結し、refusal などは含めない。
// 推論の途中で打ち切られた（status: "incomplete"）応答は、本文があっても使わない。
export function extractResponsesText(response: unknown): string | null {
  const record = asRecord(response);
  if (record === null) return null;
  if (record.status !== undefined && record.status !== 'completed') return null;
  if (!Array.isArray(record.output)) return null;

  const parts: string[] = [];
  for (const item of record.output) {
    const itemRecord = asRecord(item);
    if (itemRecord?.type !== 'message' || !Array.isArray(itemRecord.content)) continue;
    for (const content of itemRecord.content) {
      const contentRecord = asRecord(content);
      if (contentRecord?.type === 'output_text' && typeof contentRecord.text === 'string') {
        parts.push(contentRecord.text);
      }
    }
  }
  const text = parts.join('');
  return text.trim() === '' ? null : text;
}

export function createWorkersAiProvider(ai: AiBinding, config: Config): Provider {
  const run = async (model: string, input: unknown): Promise<unknown> => {
    try {
      return await ai.run(model, input);
    } catch (error) {
      console.error('Workers AI error:', error instanceof Error ? error.message : String(error));
      throw new ProxyError(classifyWorkersAiError(error));
    }
  };

  return {
    async chat(request: ChatRequest): Promise<string> {
      const response = await run(config.chatModel, {
        instructions: request.system,
        input: request.messages.map(({ role, content }) => ({ role, content })),
        reasoning: { effort: config.chatReasoningEffort },
        max_output_tokens: config.chatMaxOutputTokens,
      });
      const text = extractResponsesText(response);
      if (text === null) {
        console.error('Workers AI returned no usable text:', JSON.stringify(asRecord(response)?.status ?? null));
        throw new ProxyError('upstream');
      }
      return text;
    },

    async scoreOnce(request: ScoreRequest): Promise<unknown> {
      const response = await run(config.scoreModel, {
        messages: [{ role: 'user', content: request.prompt }],
        response_format: { type: 'json_schema', json_schema: SCORE_SCHEMA },
        max_tokens: SCORE_MAX_TOKENS,
      });
      return extractResult(asRecord(response)?.response);
    },
  };
}

export function createClaudeProvider(fetchFn: FetchLike, config: Config): Provider {
  const call = async (body: Record<string, unknown>): Promise<Record<string, unknown>> => {
    if (config.claudeApiKey === undefined || config.claudeApiKey === '') throw new ProxyError('config');

    let response: Response;
    try {
      response = await fetchFn(CLAUDE_URL, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'x-api-key': config.claudeApiKey,
          'anthropic-version': CLAUDE_VERSION,
        },
        body: JSON.stringify(body),
      });
    } catch (error) {
      console.error('Claude fetch failed:', error instanceof Error ? error.message : String(error));
      throw new ProxyError('upstream');
    }

    if (!response.ok) {
      console.error('Claude API status:', response.status);
      throw new ProxyError(classifyClaudeStatus(response.status));
    }
    try {
      const data = asRecord(await response.json());
      if (data === null) throw new Error('not an object');
      return data;
    } catch {
      throw new ProxyError('upstream');
    }
  };

  const blocks = (data: Record<string, unknown>): Record<string, unknown>[] =>
    Array.isArray(data.content) ? data.content.map(asRecord).filter((b): b is Record<string, unknown> => b !== null) : [];

  return {
    async chat(request: ChatRequest): Promise<string> {
      const data = await call({
        model: config.claudeModel,
        max_tokens: CLAUDE_MAX_TOKENS,
        system: request.system,
        messages: request.messages.map(({ role, content }) => ({ role, content })),
      });
      const text = blocks(data)
        .filter((b) => b.type === 'text' && typeof b.text === 'string')
        .map((b) => b.text as string)
        .join('');
      if (text.trim() === '') throw new ProxyError('upstream');
      return text;
    },

    async scoreOnce(request: ScoreRequest): Promise<unknown> {
      const data = await call({
        model: config.claudeModel,
        max_tokens: CLAUDE_MAX_TOKENS,
        messages: [{ role: 'user', content: request.prompt }],
        tools: [{ name: 'return_score', description: '算出したスコアを返す', input_schema: SCORE_SCHEMA }],
        tool_choice: { type: 'tool', name: 'return_score' },
      });
      const toolUse = blocks(data).find((b) => b.type === 'tool_use' && b.name === 'return_score');
      return extractResult(toolUse?.input);
    },
  };
}
