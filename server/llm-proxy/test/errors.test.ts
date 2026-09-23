import { describe, expect, it } from 'vitest';
import { classifyClaudeStatus, classifyWorkersAiError } from '../src/errors';

describe('Workers AI のエラー分類', () => {
  it('3036（1 日の無料枠の超過）は daily_quota', () => {
    expect(classifyWorkersAiError(new Error('3036: You have used up your daily free allocation of 10,000 neurons.'))).toBe('daily_quota');
  });

  it('3040（一時的な混雑）は busy', () => {
    expect(classifyWorkersAiError(new Error('3040: Capacity temporarily exceeded, please try again.'))).toBe('busy');
  });

  it.each([
    ['桁の多いコード', new Error('30360: something')],
    ['本文中に 3036 がある別コード', new Error('5028: model deprecated 3036')],
    ['接頭辞つき', new Error('AiError: 3036: daily')],
    ['コロンなし', new Error('3036')],
    ['文字列を throw', '3036: daily'],
    ['message の無いオブジェクト', { code: 3036 }],
    ['undefined', undefined],
  ])('先頭の数字が完全一致しない形（%s）は取り違えず upstream', (_label, error) => {
    expect(classifyWorkersAiError(error)).toBe('upstream');
  });

  it('message 文字列を持つ Error 以外のオブジェクトも先頭のコードで判定する', () => {
    expect(classifyWorkersAiError({ message: '3040: busy' })).toBe('busy');
  });
});

describe('Claude API のステータス分類', () => {
  it.each([
    [429, 'busy'],
    [529, 'busy'],
    [400, 'upstream'],
    [401, 'upstream'],
    [500, 'upstream'],
  ])('HTTP %i は %s（Claude には 1 日の無料枠が無いので daily_quota にしない）', (status, expected) => {
    expect(classifyClaudeStatus(status)).toBe(expected);
  });
});
