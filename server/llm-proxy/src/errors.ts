// 上流（Workers AI / Claude）の失敗を、クライアントに返すエラー種別に分類する。
//
// 応答本文には種別しか載せない。例外のメッセージや上流の本文には、内部の情報や
// キーの断片が含まれうるため、ログにだけ残す。

import { type ErrorKind, ProxyError } from './types';

// Workers AI の例外を分類する。例外メッセージの先頭が「数字: 」の形で、その数字が
// 完全一致したときだけ判定する（"30360: ..." や "5028: ... 3036 ..." を取り違えない）。
//
//   3036 … 1 日の無料枠（10,000 Neurons）を使い切った
//   3040 … 一時的な混雑
export function classifyWorkersAiError(error: unknown): ErrorKind {
  if (error instanceof ProxyError) return error.kind;

  let message = '';
  if (error instanceof Error) {
    message = error.message;
  } else if (typeof error === 'object' && error !== null && typeof (error as { message?: unknown }).message === 'string') {
    message = (error as { message: string }).message;
  }

  const match = /^(\d+):/.exec(message);
  if (match?.[1] === '3036') return 'daily_quota';
  if (match?.[1] === '3040') return 'busy';
  return 'upstream';
}

// Claude API の HTTP ステータスを分類する。429（レート制限）と 529（過負荷）は一時的な混雑。
// Claude には「1 日の無料枠」は無いので daily_quota にはしない。
export function classifyClaudeStatus(status: number): ErrorKind {
  if (status === 429 || status === 529) return 'busy';
  return 'upstream';
}

export const ERROR_STATUS: Record<ErrorKind, number> = {
  daily_quota: 429,
  busy: 503,
  invalid_score: 502,
  upstream: 502,
  bad_request: 400,
  config: 500,
  not_found: 404,
  forbidden: 403,
  method_not_allowed: 405,
};
