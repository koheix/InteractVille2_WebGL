// スコア（0〜100 の整数）の正規化。
//
// Number() の暗黙の型変換は使わない。Number(null) や Number('') は 0、Number(true) は 1 になり、
// 「解釈できない応答」が親密度 0 として黙って保存されてしまうため。

// 値をスコアにする。解釈できなければ null（呼び出し側が再試行する）。
//
//   有効: 有限の数値、数字だけの文字列（"72"、"72.5"、"-5"）
//   無効: null、真偽値、空文字、配列、オブジェクト、"seventy"、NaN、Infinity
//   有効な値は四捨五入して 0〜100 に収める（-0 は 0 にする）
export function normalizeScore(value: unknown): number | null {
  let numeric: number;
  if (typeof value === 'number') {
    numeric = value;
  } else if (typeof value === 'string' && /^\s*-?\d+(\.\d+)?\s*$/.test(value)) {
    numeric = Number.parseFloat(value);
  } else {
    return null;
  }
  if (!Number.isFinite(numeric)) return null;

  const rounded = Math.round(numeric);
  const clamped = Math.min(100, Math.max(0, rounded));
  return clamped === 0 ? 0 : clamped;
}

// { result: ... } の形（オブジェクト、または JSON 文字列）から result を取り出す。
// 形が違えば undefined（normalizeScore が null にする）。
export function extractResult(response: unknown): unknown {
  let value = response;
  if (typeof value === 'string') {
    try {
      value = JSON.parse(value);
    } catch {
      return undefined;
    }
  }
  if (typeof value !== 'object' || value === null || Array.isArray(value)) return undefined;
  return (value as Record<string, unknown>).result;
}
