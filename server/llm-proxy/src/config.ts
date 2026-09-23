// wrangler の vars を読み、検証済みの設定にする。
//
// vars はすべて文字列で渡ってくる。数値への変換漏れや、プロバイダ名の書き損じで
// 黙って既定動作に落ちるのを防ぐため、不正な値は ProxyError('config') にする。

import { type Env, ProxyError } from './types';

export type ProviderName = 'workers-ai' | 'claude';

export interface Config {
  provider: ProviderName;
  chatModel: string;
  chatReasoningEffort: 'low' | 'medium' | 'high';
  chatMaxOutputTokens: number;
  scoreModel: string;
  claudeModel: string;
  claudeApiKey: string | undefined;
}

export const SCORE_MAX_TOKENS = 64;
export const CLAUDE_MAX_TOKENS = 1024;

function required(value: string | undefined): string {
  if (value === undefined || value.trim() === '') throw new ProxyError('config');
  return value.trim();
}

export function readConfig(env: Env): Config {
  const provider = env.LLM_PROVIDER === undefined ? 'workers-ai' : env.LLM_PROVIDER;
  if (provider !== 'workers-ai' && provider !== 'claude') throw new ProxyError('config');

  const effort = env.CHAT_REASONING_EFFORT ?? 'low';
  if (effort !== 'low' && effort !== 'medium' && effort !== 'high') throw new ProxyError('config');

  const tokensText = env.CHAT_MAX_OUTPUT_TOKENS ?? '1024';
  if (!/^\d+$/.test(tokensText)) throw new ProxyError('config');
  const chatMaxOutputTokens = Number(tokensText);
  if (chatMaxOutputTokens <= 0) throw new ProxyError('config');

  return {
    provider,
    chatModel: provider === 'workers-ai' ? required(env.CHAT_MODEL) : env.CHAT_MODEL ?? '',
    chatReasoningEffort: effort,
    chatMaxOutputTokens,
    scoreModel: provider === 'workers-ai' ? required(env.SCORE_MODEL) : env.SCORE_MODEL ?? '',
    claudeModel: provider === 'claude' ? required(env.CLAUDE_MODEL) : env.CLAUDE_MODEL ?? '',
    claudeApiKey: env.CLAUDE_API_KEY,
  };
}

// ALLOWED_ORIGINS（カンマ区切り）を配列にする。
export function readAllowedOrigins(env: Env): string[] {
  return (env.ALLOWED_ORIGINS ?? '')
    .split(',')
    .map((origin) => origin.trim())
    .filter((origin) => origin !== '');
}

// 旧 API は "true" と完全一致のときだけ有効。
export function isLegacyEnabled(env: Env): boolean {
  return env.LEGACY_CLAUDE_PASSTHROUGH === 'true';
}
