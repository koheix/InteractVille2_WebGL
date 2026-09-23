using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace InteractVille2.LLM.Tests
{
    [TestFixture, Description("会話の失敗時の巻き戻しと、送信前の切り詰め")]
    public class ConversationRulesTests
    {
        private static Message M(string role, string content) => new Message { role = role, content = content };

        [Test, Description("失敗した会話の発言だけを取り除き、後から追加された別の会話の発言は残す")]
        public void RemoveExact_RemovesOnlyThatMessage()
        {
            Message u1 = M("user", "u1"), a1 = M("assistant", "a1"), u2 = M("user", "同じ"), u3 = M("user", "同じ");
            var history = new List<Message> { u1, a1, u2, u3 };

            Assert.IsTrue(ConversationRules.RemoveExact(history, u2));

            CollectionAssert.AreEqual(new[] { u1, a1, u3 }, history);
        }

        [Test, Description("内容が同じでも別のメッセージは取り除かず、2 回目の取り除きは何もしない")]
        public void RemoveExact_IsByReferenceAndIdempotent()
        {
            Message u1 = M("user", "やあ");
            var history = new List<Message> { M("user", "やあ") };

            Assert.IsFalse(ConversationRules.RemoveExact(history, u1));
            Assert.AreEqual(1, history.Count);

            history.Add(u1);
            Assert.IsTrue(ConversationRules.RemoveExact(history, u1));
            Assert.IsFalse(ConversationRules.RemoveExact(history, u1));
            Assert.AreEqual(1, history.Count);
            Assert.IsFalse(ConversationRules.RemoveExact(history, null));
        }

        [Test, Description("件数と合計文字数が上限ちょうど（100 件・60,000 文字）なら 1 件も削らない")]
        public void Trim_AtLimit_KeepsAll()
        {
            var history = Enumerable.Range(0, 100)
                .Select(i => M(i % 2 == 0 ? "user" : "assistant", new string('あ', 600)))
                .ToList();

            List<Message> trimmed = ConversationRules.TrimForRequest(history);

            Assert.AreEqual(100, trimmed.Count);
            Assert.AreEqual(60000, trimmed.Sum(m => m.content.Length));
        }

        [Test, Description("101 件の履歴は古いものから削り、先頭が user になるようにする（最新の発言は必ず残る）")]
        public void Trim_OverCount_DropsOldest_StartsWithUser()
        {
            // 0 番目が user、奇数番目が assistant。101 件なので最後（100 番目）は user。
            var history = Enumerable.Range(0, 101).Select(i => M(i % 2 == 0 ? "user" : "assistant", "m" + i)).ToList();

            List<Message> trimmed = ConversationRules.TrimForRequest(history);

            // 100 件に収めると先頭は 1 番目（assistant）になるので、それも落として 99 件。
            Assert.AreEqual(99, trimmed.Count);
            Assert.AreEqual("m2", trimmed[0].content);
            Assert.AreEqual("user", trimmed[0].role);
            Assert.AreEqual("m100", trimmed[trimmed.Count - 1].content);
        }

        [Test, Description("合計が 60,000 文字を超える分は古いものから削り、上限に収める")]
        public void Trim_OverChars_DropsOldest()
        {
            var history = new List<Message>
            {
                M("user", new string('a', 30000)),
                M("assistant", new string('b', 20000)),
                M("user", new string('c', 20000)),
                M("assistant", new string('d', 10000)),
                M("user", new string('e', 10000)),
            };

            List<Message> trimmed = ConversationRules.TrimForRequest(history);

            // 新しい順に e(10,000) d(10,000) c(20,000) b(20,000) で 60,000。a を足すと超える。
            // 先頭の b は assistant なので落とし、c から始まる。
            CollectionAssert.AreEqual(new[] { 'c', 'd', 'e' }, trimmed.Select(m => m.content[0]).ToArray());
            Assert.LessOrEqual(trimmed.Sum(m => m.content.Length), LlmLimits.MessagesMaxTotalChars);
        }

        [Test, Description("最新の発言 1 件だけで上限を超えても、送るものが無くならないよう残す")]
        public void Trim_SingleHugeMessage_IsKept()
        {
            var history = new List<Message> { M("user", "古い"), M("user", new string('x', 70000)) };

            List<Message> trimmed = ConversationRules.TrimForRequest(history);

            Assert.AreEqual(1, trimmed.Count);
            Assert.AreEqual(70000, trimmed[0].content.Length);
        }

        [Test, Description("切り詰めは元の履歴の件数・順序・各要素の role と content を変更しない（先頭を user にするために要素を書き換えない）")]
        public void Trim_DoesNotModifyOriginal()
        {
            // 101 件で、100 件に収めると先頭が assistant になる（先頭を落とす処理が走る）形。
            var history = Enumerable.Range(0, 101).Select(i => M(i % 2 == 0 ? "user" : "assistant", "m" + i)).ToList();
            var before = history.Select(m => (m, m.role, m.content)).ToList();

            ConversationRules.TrimForRequest(history);

            CollectionAssert.AreEqual(before, history.Select(m => (m, m.role, m.content)).ToList());
        }

        [Test, Description("プロンプト用の会話ログは \"role: content\" を改行でつなぎ、除外に指定したメッセージ（要約の指示）だけを参照で除く")]
        public void FormatForPrompt_ExcludesByReference()
        {
            Message instruction = M("user", "要約して");
            var history = new List<Message> { M("user", "やあ"), M("assistant", "こんにちは"), instruction, M("user", "要約して") };

            string text = ConversationRules.FormatForPrompt(history, instruction, 1000);

            Assert.AreEqual("user: やあ\nassistant: こんにちは\nuser: 要約して", text);
        }

        [Test, Description("除外するものが無ければ、最後の発言も落とさずに含める（以前の MessagesToString は末尾を落としていた）")]
        public void FormatForPrompt_KeepsLastMessage()
        {
            var history = new List<Message> { M("user", "やあ"), M("assistant", "こんにちは") };

            Assert.AreEqual("user: やあ\nassistant: こんにちは", ConversationRules.FormatForPrompt(history, null, 1000));
        }

        [Test, Description("会話ログが上限ちょうどなら全部残し、1 文字でも超えるなら古い発言から落とす")]
        public void FormatForPrompt_BoundaryDropsOldest()
        {
            var history = new List<Message> { M("user", "aaaa"), M("assistant", "bb") };
            // "user: aaaa"（10）+ 改行（1）+ "assistant: bb"（13）= 24 文字
            Assert.AreEqual("user: aaaa\nassistant: bb", ConversationRules.FormatForPrompt(history, null, 24));
            Assert.AreEqual("assistant: bb", ConversationRules.FormatForPrompt(history, null, 23));
        }

        [Test, Description("最新の 1 行だけで上限を超えるなら、その行の末尾（新しい部分）を上限の長さだけ残す")]
        public void FormatForPrompt_SingleLongLine_KeepsTail()
        {
            var history = new List<Message> { M("user", "0123456789") };

            Assert.AreEqual("456789", ConversationRules.FormatForPrompt(history, null, 6));
        }

        [Test, Description("末尾を残すとき、絵文字（サロゲートペア）の途中からは始めない")]
        public void FormatForPrompt_SingleLongLine_DoesNotSplitSurrogatePair()
        {
            // 行は "user: a😀b"（UTF-16 で 10 単位）。末尾 2 単位は 😀 の下位サロゲート + "b" なので、1 つ進めて "b" だけにする。
            // 末尾 3 単位なら 😀 をまるごと含められる。
            var history = new List<Message> { M("user", "a😀b") };

            string text = ConversationRules.FormatForPrompt(history, null, 2);

            Assert.AreEqual("b", text);
            Assert.AreEqual("😀b", ConversationRules.FormatForPrompt(history, null, 3));
        }

        [Test, Description("疑似ストリーミングは絵文字と濁点の結合文字を割らずに 1 文字ずつ伸ばし、最後は元の文字列になる")]
        public void StreamingPrefixes_KeepGraphemesIntact()
        {
            const string text = "や😀か\u3099ね";

            List<string> prefixes = ConversationRules.StreamingPrefixes(text);

            CollectionAssert.AreEqual(new[] { "や", "や😀", "や😀か\u3099", "や😀か\u3099ね" }, prefixes);
            foreach (string prefix in prefixes)
            {
                Assert.IsFalse(char.IsHighSurrogate(prefix[prefix.Length - 1]), $"途中で割れたサロゲート: {prefix}");
            }
        }

        [Test, Description("空の文字列と null では何も表示しない")]
        public void StreamingPrefixes_Empty()
        {
            CollectionAssert.IsEmpty(ConversationRules.StreamingPrefixes(""));
            CollectionAssert.IsEmpty(ConversationRules.StreamingPrefixes(null));
        }
    }

    [TestFixture, Description("会話 1 回の結果を履歴と会話回数に反映する規則")]
    public class ChatTurnRulesTests
    {
        private static readonly DateTime Before = new DateTime(2026, 9, 23, 9, 0, 0, DateTimeKind.Local);
        private static readonly DateTime Token = new DateTime(2026, 9, 23, 10, 0, 0, DateTimeKind.Local);
        private static readonly DateTime After = new DateTime(2026, 9, 23, 10, 1, 0, DateTimeKind.Local);

        private static Message M(string role, string content) => new Message { role = role, content = content };

        [Test, Description("成功なら返事をアシスタントの発言として履歴に 1 件だけ追加し、会話回数は返却しない")]
        public void Success_AddsOneAssistantMessage()
        {
            Message previous = M("assistant", "前の返事"), user = M("user", "やあ");
            var history = new List<Message> { previous, user };
            var turns = new List<DateTime> { Before, Token };

            ChatTurnOutcome outcome = ChatTurnRules.CompleteTurn(history, user, turns, Token, LlmResult.FromText("こんにちは！"));

            Assert.IsTrue(outcome.Succeeded);
            Assert.IsFalse(outcome.Refunded);
            Assert.AreEqual("こんにちは！", outcome.DisplayText);
            Assert.AreEqual(3, history.Count);
            Assert.AreSame(user, history[1]);
            Assert.AreEqual("assistant", history[2].role);
            Assert.AreEqual("こんにちは！", history[2].content);
            CollectionAssert.AreEqual(new[] { Before, Token }, turns);
        }

        [Test, Description("どの種別の失敗でも、今回の発言だけを履歴から取り除き、エラーの文言を履歴に入れず、今回の会話回数だけを返却する")]
        public void Failure_RollsBackOnlyThisTurn()
        {
            foreach (LlmErrorKind kind in Enum.GetValues(typeof(LlmErrorKind)))
            {
                Message earlier = M("user", "前の発言"), user = M("user", "やあ"), later = M("user", "やあ");
                var history = new List<Message> { earlier, user, later };
                var turns = new List<DateTime> { Before, Token, After };

                ChatTurnOutcome outcome = ChatTurnRules.CompleteTurn(history, user, turns, Token, LlmResult.Fail(kind));

                Assert.IsFalse(outcome.Succeeded, kind.ToString());
                Assert.IsTrue(outcome.Refunded, kind.ToString());
                Assert.AreEqual(LlmErrorMessages.ForChat(kind), outcome.DisplayText, kind.ToString());
                CollectionAssert.AreEqual(new[] { earlier, later }, history, kind.ToString());
                CollectionAssert.AreEqual(new[] { Before, After }, turns, kind.ToString());
                Assert.IsFalse(history.Any(m => m.content == outcome.DisplayText), kind.ToString());
            }
        }

        [Test, Description("結果が無い（コールバックが呼ばれなかった）ときは Upstream の失敗として扱う")]
        public void NullResult_IsUpstreamFailure()
        {
            Message user = M("user", "やあ");
            var history = new List<Message> { user };
            var turns = new List<DateTime> { Token };

            ChatTurnOutcome outcome = ChatTurnRules.CompleteTurn(history, user, turns, Token, null);

            Assert.IsFalse(outcome.Succeeded);
            Assert.AreEqual(LlmErrorMessages.ForChat(LlmErrorKind.Upstream), outcome.DisplayText);
            CollectionAssert.IsEmpty(history);
            CollectionAssert.IsEmpty(turns);
        }

        [Test, Description("返却する記録が見つからないときは Refunded を false にする（保存し直さない）")]
        public void Failure_WithoutRecord_IsNotRefunded()
        {
            Message user = M("user", "やあ");
            var turns = new List<DateTime> { Before };

            ChatTurnOutcome outcome = ChatTurnRules.CompleteTurn(new List<Message> { user }, user, turns, Token, LlmResult.Fail(LlmErrorKind.Busy));

            Assert.IsFalse(outcome.Refunded);
            CollectionAssert.AreEqual(new[] { Before }, turns);
        }
    }

    [TestFixture, Description("会話回数の返却")]
    public class SpeakTurnsTests
    {
        private static readonly DateTime T1 = new DateTime(2026, 9, 23, 10, 0, 0, DateTimeKind.Local);
        private static readonly DateTime T2 = new DateTime(2026, 9, 23, 10, 5, 0, DateTimeKind.Local);
        private static readonly DateTime T3 = new DateTime(2026, 9, 23, 10, 6, 0, DateTimeKind.Local);

        [Test, Description("失敗した会話の時刻だけを取り除き、後から記録された別の会話の時刻は残す（末尾を消さない）")]
        public void Refund_RemovesThatTimeNotTheLast()
        {
            var history = new List<DateTime> { T1, T2, T3 };

            Assert.IsTrue(SpeakTurns.Refund(history, T2));

            CollectionAssert.AreEqual(new[] { T1, T3 }, history);
        }

        [Test, Description("同じ時刻の記録が 2 件あっても 1 件だけ返却し、記録の無い時刻の返却は何もしない")]
        public void Refund_RemovesOneAndIsSafeWhenMissing()
        {
            var history = new List<DateTime> { T1, T1 };

            Assert.IsTrue(SpeakTurns.Refund(history, T1));
            Assert.AreEqual(1, history.Count);
            Assert.IsFalse(SpeakTurns.Refund(history, T3));
            Assert.AreEqual(1, history.Count);
            Assert.IsFalse(SpeakTurns.Refund(null, T1));
        }

        [Test, Description("保存形式（ToBinary）から読み戻した時刻の記録でも、元の時刻で返却できる")]
        public void Refund_WorksAfterBinaryRoundTrip()
        {
            var history = new List<DateTime> { DateTime.FromBinary(T1.ToBinary()), DateTime.FromBinary(T2.ToBinary()) };

            Assert.IsTrue(SpeakTurns.Refund(history, T2));

            Assert.AreEqual(1, history.Count);
            Assert.AreEqual(T1.Ticks, history[0].Ticks);
        }
    }
}
