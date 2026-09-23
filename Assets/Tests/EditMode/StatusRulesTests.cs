using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace InteractVille2.LLM.Tests
{
    [TestFixture, Description("LLM の結果をともハムのステータスに反映する規則")]
    public class StatusRulesTests
    {
        private static IEnumerable<LlmErrorKind> AllErrors => Enum.GetValues(typeof(LlmErrorKind)).Cast<LlmErrorKind>();

        [Test, Description("スコアの取得に失敗したら（どの種別でも、結果が無くても）会話前の値を保つ")]
        public void ApplyScore_Failure_KeepsCurrent()
        {
            foreach (LlmErrorKind kind in AllErrors)
            {
                Assert.AreEqual(55, StatusRules.ApplyScore(55, LlmResult.Fail(kind)), kind.ToString());
            }
            Assert.AreEqual(55, StatusRules.ApplyScore(55, null));
        }

        [Test, Description("スコアの取得に成功したら、その値（0 や 100 も）に置き換える")]
        public void ApplyScore_Success_Replaces()
        {
            Assert.AreEqual(0, StatusRules.ApplyScore(55, LlmResult.FromScore(0)));
            Assert.AreEqual(100, StatusRules.ApplyScore(55, LlmResult.FromScore(100)));
            Assert.AreEqual(72, StatusRules.ApplyScore(55, LlmResult.FromScore(72)));
        }

        [Test, Description("要約に失敗・空・空白だけなら記憶を追加せず、上限ちょうどの記憶も押し出さない")]
        public void TryAppendMemory_Failure_DoesNotChangeMemory()
        {
            var memory = Enumerable.Range(0, 20).Select(i => "記憶" + i).ToList();
            var before = memory.ToList();

            Assert.IsFalse(StatusRules.TryAppendMemory(memory, LlmResult.Fail(LlmErrorKind.DailyQuota), "t", 20));
            Assert.IsFalse(StatusRules.TryAppendMemory(memory, LlmResult.FromText(""), "t", 20));
            Assert.IsFalse(StatusRules.TryAppendMemory(memory, LlmResult.FromText("  \n"), "t", 20));
            Assert.IsFalse(StatusRules.TryAppendMemory(memory, null, "t", 20));

            CollectionAssert.AreEqual(before, memory);
        }

        [Test, Description("要約に成功したら「要約\\n (時刻)」の形で末尾に追加し、上限を超えたら最も古い記憶を消す")]
        public void TryAppendMemory_Success_AppendsWithTimestamp()
        {
            var memory = Enumerable.Range(0, 20).Select(i => "記憶" + i).ToList();

            Assert.IsTrue(StatusRules.TryAppendMemory(memory, LlmResult.FromText("- りんごの話をした"), "2026年09月23日 10時00分00秒", 20));

            Assert.AreEqual(20, memory.Count);
            Assert.AreEqual("記憶1", memory[0]);
            Assert.AreEqual("- りんごの話をした\n (2026年09月23日 10時00分00秒)", memory[19]);
        }

        [Test, Description("気分の取得に失敗・空・空白だけなら今の気分を保ち、成功なら前後の空白を落として置き換える")]
        public void ApplyMood()
        {
            Assert.AreEqual("普通", StatusRules.ApplyMood("普通", LlmResult.Fail(LlmErrorKind.Upstream)));
            Assert.AreEqual("普通", StatusRules.ApplyMood("普通", LlmResult.FromText("")));
            Assert.AreEqual("普通", StatusRules.ApplyMood("普通", LlmResult.FromText(" \n")));
            Assert.AreEqual("普通", StatusRules.ApplyMood("普通", null));
            Assert.AreEqual("喜び", StatusRules.ApplyMood("普通", LlmResult.FromText(" 喜び\n")));
        }
    }

    [TestFixture, Description("会話で失敗したときにともハムが話す文言")]
    public class LlmErrorMessagesTests
    {
        [Test, Description("すべてのエラー種別に空でない文言がある（種別を足して文言を足し忘れると失敗する）")]
        public void EveryKind_HasMessage()
        {
            foreach (LlmErrorKind kind in Enum.GetValues(typeof(LlmErrorKind)))
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(LlmErrorMessages.ForChat(kind)), kind.ToString());
            }
        }

        [Test, Description("無料枠の超過では「今日はもう話せない」ことと、回復する日本時間 9 時を伝える")]
        public void DailyQuota_SaysCannotTalkToday()
        {
            string message = LlmErrorMessages.ForChat(LlmErrorKind.DailyQuota);
            StringAssert.Contains("今日はもう話せない", message);
            StringAssert.Contains("9時", message);
        }

        [Test, Description("混雑と通信失敗では、もう一度話しかけるよう促す（今日はもう話せない、とは言わない）")]
        public void TransientErrors_AskToRetry()
        {
            foreach (LlmErrorKind kind in new[] { LlmErrorKind.Busy, LlmErrorKind.Network })
            {
                string message = LlmErrorMessages.ForChat(kind);
                StringAssert.Contains("もう一度", message, kind.ToString());
                StringAssert.DoesNotContain("今日はもう", message, kind.ToString());
            }
        }
    }
}
