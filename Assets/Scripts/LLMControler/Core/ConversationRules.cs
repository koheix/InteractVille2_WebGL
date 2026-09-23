using System;
using System.Collections.Generic;
using System.Globalization;

namespace InteractVille2.LLM
{
    /// <summary>
    /// 会話の失敗時の巻き戻しと、送信前の切り詰め。
    ///
    /// 送信ボタンは通信中も押せるので、「最後の 1 件を消す」ような位置に頼った処理にすると、
    /// 別の会話の記録を消してしまう。消すときは「今回追加したもの」そのものを指定する。
    /// </summary>
    public static class ConversationRules
    {
        /// <summary>
        /// 会話履歴から、指定したメッセージ（参照が同じもの）を 1 件だけ取り除く。
        /// 内容が同じ別のメッセージは取り除かない。取り除いたら true。
        /// </summary>
        public static bool RemoveExact(List<Message> history, Message message)
        {
            if (history == null || message == null) return false;
            for (int i = 0; i < history.Count; i++)
            {
                if (ReferenceEquals(history[i], message))
                {
                    history.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 送信用に、会話履歴をプロキシの上限（件数・合計文字数）に収める。
        /// 新しいメッセージから残し、先頭が user になるようにする（Claude は先頭が user でないと拒否する）。
        /// 元のリストは変更しない。最新のメッセージは上限を超えていても残す（1 件も送らないよりよい）。
        /// </summary>
        public static List<Message> TrimForRequest(IReadOnlyList<Message> history)
        {
            var kept = new List<Message>();
            if (history == null) return kept;

            int total = 0;
            for (int i = history.Count - 1; i >= 0; i--)
            {
                var message = history[i];
                int length = message?.content?.Length ?? 0;
                bool isNewest = kept.Count == 0;
                if (!isNewest && (kept.Count >= LlmLimits.MessagesMaxCount || total + length > LlmLimits.MessagesMaxTotalChars)) break;
                kept.Add(message);
                total += length;
            }
            kept.Reverse();

            while (kept.Count > 1 && kept[0]?.role != "user") kept.RemoveAt(0);
            return kept;
        }

        /// <summary>
        /// スコアや要約のプロンプトに埋め込む会話ログ（"role: content" を改行でつないだもの）を作る。
        /// exclude に指定したメッセージ（要約の指示など、会話ではないもの）は参照で除く。
        /// 全体が maxChars 文字（改行を含む）を超えるなら、古い発言から落として収める。
        /// 最新の 1 行だけで超えるなら、その行の末尾 maxChars 文字を残す。
        /// </summary>
        public static string FormatForPrompt(IReadOnlyList<Message> history, Message exclude, int maxChars)
        {
            if (history == null || maxChars <= 0) return string.Empty;

            var lines = new List<string>();
            int total = 0;
            for (int i = history.Count - 1; i >= 0; i--)
            {
                var message = history[i];
                if (message == null || ReferenceEquals(message, exclude)) continue;

                string line = $"{message.role}: {message.content}";
                int added = line.Length + (lines.Count > 0 ? 1 : 0);
                if (total + added > maxChars)
                {
                    if (lines.Count == 0)
                    {
                        int start = line.Length - maxChars;
                        // サロゲートペアの途中から始めない（先頭が孤立した下位サロゲートにならないようにする）
                        if (char.IsLowSurrogate(line[start])) start++;
                        lines.Add(line.Substring(start));
                    }
                    break;
                }
                lines.Add(line);
                total += added;
            }
            lines.Reverse();
            return string.Join("\n", lines);
        }

        /// <summary>
        /// 疑似ストリーミングで表示する途中経過を、見た目の 1 文字（書記素）ずつ伸ばして返す。
        /// char 単位で切ると、絵文字（サロゲートペア）や濁点の結合文字が途中で割れて表示が崩れる。
        /// </summary>
        public static List<string> StreamingPrefixes(string text)
        {
            var prefixes = new List<string>();
            if (string.IsNullOrEmpty(text)) return prefixes;

            int[] starts = StringInfo.ParseCombiningCharacters(text);
            for (int i = 0; i < starts.Length; i++)
            {
                int end = i + 1 < starts.Length ? starts[i + 1] : text.Length;
                prefixes.Add(text.Substring(0, end));
            }
            return prefixes;
        }
    }

    /// <summary>会話 1 回分の結果をどう反映したか。</summary>
    public sealed class ChatTurnOutcome
    {
        /// <summary>ともハムの台詞として表示する文（成功なら返事、失敗ならエラーの文言）。</summary>
        public string DisplayText { get; }
        public bool Succeeded { get; }
        /// <summary>会話回数を返却したか（true なら保存し直して UI を更新する）。</summary>
        public bool Refunded { get; }

        public ChatTurnOutcome(string displayText, bool succeeded, bool refunded)
        {
            DisplayText = displayText;
            Succeeded = succeeded;
            Refunded = refunded;
        }
    }

    /// <summary>会話 1 回（ユーザーの発言 → ともハムの返事）の結果を、履歴と会話回数に反映する規則。</summary>
    public static class ChatTurnRules
    {
        /// <summary>
        /// 成功なら返事をアシスタントの発言として履歴に 1 件だけ追加する。
        /// 失敗（result が null の場合も含む）なら、今回の発言（userMessage）を履歴から取り除き、
        /// 今回消費した会話回数（speakTime の記録）を返却する。エラーの文言は表示するだけで、履歴には入れない
        /// （入れると次の会話で LLM に送られ、会話ログとしても保存されてしまう）。
        /// </summary>
        public static ChatTurnOutcome CompleteTurn(List<Message> history, Message userMessage, List<DateTime> speakHistory, DateTime speakTime, LlmResult result)
        {
            if (result != null && result.IsSuccess)
            {
                history.Add(new Message { role = "assistant", content = result.Text });
                return new ChatTurnOutcome(result.Text, true, false);
            }

            LlmErrorKind error = result?.Error ?? LlmErrorKind.Upstream;
            ConversationRules.RemoveExact(history, userMessage);
            bool refunded = SpeakTurns.Refund(speakHistory, speakTime);
            return new ChatTurnOutcome(LlmErrorMessages.ForChat(error), false, refunded);
        }
    }

    /// <summary>1 時間あたりの会話回数の記録に対する操作。</summary>
    public static class SpeakTurns
    {
        /// <summary>
        /// LLM の呼び出しが失敗したときに、今回消費した回数を返す。
        /// 今回記録した時刻（token）と同じ値の記録を 1 件だけ取り除く。末尾ではなく値で探すので、
        /// 通信中に別の会話が記録されていても、その記録は消さない。取り除いたら true。
        /// </summary>
        public static bool Refund(List<DateTime> history, DateTime token)
        {
            if (history == null) return false;
            int index = history.IndexOf(token);
            if (index < 0) return false;
            history.RemoveAt(index);
            return true;
        }
    }

    /// <summary>LLM の結果をともハムのステータスに反映する規則。失敗したときは今の値を保つ。</summary>
    public static class StatusRules
    {
        /// <summary>スコア（親密度・感情価・覚醒度）。失敗なら今の値のまま。</summary>
        public static int ApplyScore(int current, LlmResult result)
        {
            return result != null && result.IsSuccess ? result.Score : current;
        }

        /// <summary>
        /// 要約を記憶に追加する。失敗や空の要約は追加しない（時刻だけの記憶が増えると、
        /// 「前回の会話時刻」が失敗した時刻になり、上限を超えて本物の古い記憶も押し出される）。
        /// 追加して上限を超えたら、古いものから消す。追加したら true。
        /// </summary>
        public static bool TryAppendMemory(List<string> memory, LlmResult result, string timestamp, int maxSize)
        {
            if (memory == null || result == null || !result.IsSuccess || string.IsNullOrWhiteSpace(result.Text)) return false;

            memory.Add(result.Text + $"\n ({timestamp})");
            while (memory.Count > maxSize) memory.RemoveAt(0);
            return true;
        }

        /// <summary>気分。失敗や空の応答なら今の気分のまま。前後の空白は落とす。</summary>
        public static string ApplyMood(string current, LlmResult result)
        {
            if (result == null || !result.IsSuccess || string.IsNullOrWhiteSpace(result.Text)) return current;
            return result.Text.Trim();
        }
    }

    /// <summary>会話で失敗したときに、ともハムの台詞として表示する文言。</summary>
    public static class LlmErrorMessages
    {
        public static string ForChat(LlmErrorKind kind)
        {
            switch (kind)
            {
                // Workers AI の 1 日の枠は 00:00 UTC（日本時間 9:00）に回復する。
                case LlmErrorKind.DailyQuota:
                    return "今日はもう話せないみたい…！朝9時をすぎたら、またお話ししようね。";
                case LlmErrorKind.Busy:
                    return "いまちょっと混み合ってるみたい…少し待ってから、もう一度話しかけてね！";
                case LlmErrorKind.Network:
                    return "うまく聞こえなかったみたい…通信を確かめて、もう一度話しかけてね！";
                case LlmErrorKind.InvalidScore:
                case LlmErrorKind.BadRequest:
                case LlmErrorKind.Upstream:
                    return "うまくお返事できなかったみたい…もう一度話しかけてね！";
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "文言が定義されていないエラー種別です。");
            }
        }
    }
}
