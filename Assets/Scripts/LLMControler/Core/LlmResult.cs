namespace InteractVille2.LLM
{
    /// <summary>LLM 呼び出しが失敗した理由。</summary>
    public enum LlmErrorKind
    {
        /// <summary>Workers AI の 1 日の無料枠を使い切った（日本時間 9:00 に回復する）。</summary>
        DailyQuota,
        /// <summary>一時的な混雑。少し待てば回復する。</summary>
        Busy,
        /// <summary>スコアとして解釈できる値が返らなかった。</summary>
        InvalidScore,
        /// <summary>リクエストが上限を超えている、形式が不正。</summary>
        BadRequest,
        /// <summary>上流（LLM・プロキシ）の失敗、応答が解釈できない。</summary>
        Upstream,
        /// <summary>通信できなかった（オフライン、タイムアウト）。</summary>
        Network,
    }

    /// <summary>LLM 呼び出しの結果。成功なら Text（会話）か Score（スコア）、失敗なら Error。</summary>
    public sealed class LlmResult
    {
        public bool IsSuccess { get; }
        public string Text { get; }
        public int Score { get; }
        public LlmErrorKind Error { get; }

        private LlmResult(bool isSuccess, string text, int score, LlmErrorKind error)
        {
            IsSuccess = isSuccess;
            Text = text;
            Score = score;
            Error = error;
        }

        public static LlmResult FromText(string text) => new LlmResult(true, text, 0, default);
        public static LlmResult FromScore(int score) => new LlmResult(true, null, score, default);
        public static LlmResult Fail(LlmErrorKind error) => new LlmResult(false, null, 0, error);

        public override string ToString() => IsSuccess ? $"Success(text={Text}, score={Score})" : $"Fail({Error})";
    }
}
