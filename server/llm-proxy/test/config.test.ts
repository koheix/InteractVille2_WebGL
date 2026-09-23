import { describe, expect, it } from 'vitest';
import { isLegacyEnabled, readAllowedOrigins, readConfig } from '../src/config';
import { ProxyError } from '../src/types';
import { makeEnv } from './helpers';

const kindOf = (fn: () => unknown): string | undefined => {
  try {
    fn();
    return undefined;
  } catch (error) {
    return error instanceof ProxyError ? error.kind : 'other';
  }
};

describe('設定の読み込み', () => {
  it('vars の値（既定値と異なる値）を読み、トークン数は数値に変換する', () => {
    const config = readConfig(
      makeEnv(undefined, {
        CHAT_MAX_OUTPUT_TOKENS: '2048',
        CHAT_REASONING_EFFORT: 'medium',
        CHAT_MODEL: '@cf/test/chat',
        SCORE_MODEL: '@cf/test/score',
        CLAUDE_MODEL: 'claude-test',
      }),
    );
    expect(config).toMatchObject({
      chatMaxOutputTokens: 2048,
      chatReasoningEffort: 'medium',
      chatModel: '@cf/test/chat',
      scoreModel: '@cf/test/score',
      claudeModel: 'claude-test',
    });
  });

  it('トークン数と推論の強さが未設定なら 1024 と low', () => {
    const config = readConfig(makeEnv(undefined, { CHAT_MAX_OUTPUT_TOKENS: undefined, CHAT_REASONING_EFFORT: undefined }));
    expect(config.chatMaxOutputTokens).toBe(1024);
    expect(config.chatReasoningEffort).toBe('low');
  });

  it('LLM_PROVIDER が未設定なら workers-ai', () => {
    expect(readConfig(makeEnv(undefined, { LLM_PROVIDER: undefined })).provider).toBe('workers-ai');
  });

  it.each(['Claude', 'claude ', 'openai', ''])('LLM_PROVIDER の書き損じ（%j）は黙って workers-ai に落とさず config エラー', (value) => {
    expect(kindOf(() => readConfig(makeEnv(undefined, { LLM_PROVIDER: value })))).toBe('config');
  });

  it.each(['abc', '', '0', '-1', '10.5'])('CHAT_MAX_OUTPUT_TOKENS が正の整数でない（%j）なら config エラー', (value) => {
    expect(kindOf(() => readConfig(makeEnv(undefined, { CHAT_MAX_OUTPUT_TOKENS: value })))).toBe('config');
  });

  it('推論の強さが low / medium / high 以外なら config エラー', () => {
    expect(kindOf(() => readConfig(makeEnv(undefined, { CHAT_REASONING_EFFORT: 'minimal' })))).toBe('config');
  });

  it('workers-ai のときにモデル名が空なら config エラー', () => {
    expect(kindOf(() => readConfig(makeEnv(undefined, { SCORE_MODEL: ' ' })))).toBe('config');
  });

  it('claude のときは Workers AI のモデル名が無くてもよいが、Claude のモデル名は必須', () => {
    expect(kindOf(() => readConfig(makeEnv(undefined, { LLM_PROVIDER: 'claude', CHAT_MODEL: undefined, SCORE_MODEL: undefined })))).toBeUndefined();
    expect(kindOf(() => readConfig(makeEnv(undefined, { LLM_PROVIDER: 'claude', CLAUDE_MODEL: '' })))).toBe('config');
  });

  it('ALLOWED_ORIGINS はカンマで区切り、前後の空白と空要素を除く', () => {
    expect(readAllowedOrigins(makeEnv(undefined, { ALLOWED_ORIGINS: ' https://a.example , ,https://b.example ' }))).toEqual([
      'https://a.example',
      'https://b.example',
    ]);
  });

  it.each([
    ['true', true],
    ['TRUE', false],
    ['1', false],
    ['false', false],
    [undefined, false],
  ])('旧 API は LEGACY_CLAUDE_PASSTHROUGH が "true" と完全一致のときだけ有効（%j → %s）', (value, expected) => {
    expect(isLegacyEnabled(makeEnv(undefined, { LEGACY_CLAUDE_PASSTHROUGH: value }))).toBe(expected);
  });
});
