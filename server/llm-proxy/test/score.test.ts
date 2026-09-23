import { describe, expect, it } from 'vitest';
import { extractResult, normalizeScore } from '../src/score';

describe('スコアの正規化', () => {
  it.each([
    [72, 72],
    [72.4, 72],
    [72.5, 73],
    [0, 0],
    [100, 100],
    [-5, 0],
    [100.5, 100],
    [1e3, 100],
    ['72', 72],
    ['72.5', 73],
    [' 40 ', 40],
  ])('数値と数字だけの文字列は四捨五入して 0〜100 に収める（%j → %i）', (input, expected) => {
    expect(normalizeScore(input)).toBe(expected);
  });

  it('-0.5 は -0 ではなく 0 になる（JSON で "-0" を返さない）', () => {
    const result = normalizeScore(-0.5);
    expect(Object.is(result, 0)).toBe(true);
  });

  it.each([
    ['null', null],
    ['undefined', undefined],
    ['空文字', ''],
    ['空白のみ', '  '],
    ['true', true],
    ['false', false],
    ['空配列', []],
    ['数値 1 つの配列', [72]],
    ['空オブジェクト', {}],
    ['文章', 'seventy'],
    ['コードフェンス付き JSON', '```json{"result":72}```'],
    ['NaN', Number.NaN],
    ['Infinity', Number.POSITIVE_INFINITY],
    ['文字列の Infinity', 'Infinity'],
    ['単位付き', '72点'],
  ])('解釈できない値（%s）は 0 や 1 にせず null を返す', (_label, input) => {
    expect(normalizeScore(input)).toBeNull();
  });
});

describe('スコア応答から result を取り出す', () => {
  it('オブジェクトの result を返す', () => {
    expect(extractResult({ result: 72 })).toBe(72);
  });

  it('JSON 文字列（前後の空白つき）の result を返す', () => {
    expect(extractResult(' {"result": 72} ')).toBe(72);
  });

  it.each([
    ['別のキー名', { Valence: 80 }],
    ['壊れた JSON 文字列', '{"result":'],
    ['配列', [{ result: 72 }]],
    ['null', null],
    ['数値そのもの', 72],
  ])('%s からは値を取り出さない（undefined）', (_label, input) => {
    expect(extractResult(input)).toBeUndefined();
  });
});
