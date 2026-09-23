namespace InteractVille2.LLM
{
    /// <summary>
    /// LLM プロキシ（server/llm-proxy）が受け付ける入力の上限のうち、Unity 側で守るもの。
    /// server/llm-proxy/contract/limits.json と同じ値にする（EditMode テストで一致を確認している）。
    /// 文字数は UTF-16 のコード単位（string.Length）で数える。
    ///
    ///   会話の履歴 … ConversationRules.TrimForRequest が件数と合計文字数に収める
    ///   スコアのプロンプト … ConversationRules.FormatForPrompt が会話部分を収める
    ///   system（上限 20,000 文字）… 記憶 20 件を入れても数千文字なので、Unity 側では切り詰めない
    /// </summary>
    public static class LlmLimits
    {
        public const int MessagesMaxCount = 100;
        public const int MessagesMaxTotalChars = 60000;
        public const int PromptMaxChars = 30000;
    }
}
