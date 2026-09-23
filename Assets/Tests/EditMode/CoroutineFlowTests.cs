using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;

namespace InteractVille2.LLM.Tests
{
    [TestFixture, Description("コルーチンの後処理の保証（会話中の例外で UI が固まらないようにするため）")]
    public class CoroutineFlowTests
    {
        private readonly List<string> log = new List<string>();

        [SetUp]
        public void SetUp() => log.Clear();

        private IEnumerator Inner(bool fail)
        {
            try
            {
                yield return "inner-1";
                if (fail) throw new InvalidOperationException("入れ子の中の失敗");
                yield return "inner-2";
            }
            finally
            {
                log.Add("inner-finally");
            }
        }

        private IEnumerator Middle(bool fail)
        {
            yield return "middle-1";
            yield return Inner(fail);
            yield return "middle-2";
        }

        private IEnumerator Body(bool fail)
        {
            yield return "body-1";
            yield return Middle(fail);
            yield return "body-2";
        }

        // Unity の代わりに MoveNext を回し、Unity に渡る値を集める。
        private static List<object> Drain(IEnumerator routine)
        {
            var yielded = new List<object>();
            while (routine.MoveNext()) yielded.Add(routine.Current);
            return yielded;
        }

        [Test, Description("入れ子の IEnumerator も自分で進め、Unity には入れ子以外の値だけを順番どおりに渡す")]
        public void Guard_FlattensNestedEnumerators()
        {
            int calls = 0;
            List<object> yielded = Drain(CoroutineFlow.Guard(Body(false), () => calls++));

            CollectionAssert.AreEqual(new object[] { "body-1", "middle-1", "inner-1", "inner-2", "middle-2", "body-2" }, yielded);
            Assert.AreEqual(1, calls);
        }

        [Test, Description("最後まで進むまでは後処理を呼ばず、正常に終わったら後処理をちょうど 1 回呼ぶ")]
        public void Guard_CallsFinallyOnceAtTheEnd()
        {
            int calls = 0;
            IEnumerator routine = CoroutineFlow.Guard(Body(false), () => calls++);

            routine.MoveNext();
            routine.MoveNext();
            Assert.AreEqual(0, calls);

            while (routine.MoveNext()) { }
            Assert.AreEqual(1, calls);
        }

        [Test, Description("入れ子の 2 段目で例外が起きても後処理をちょうど 1 回呼び、例外はそのまま投げ直す（入れ子の finally も実行される）")]
        public void Guard_CallsFinallyOnNestedException()
        {
            int calls = 0;
            IEnumerator routine = CoroutineFlow.Guard(Body(true), () => calls++);

            var error = Assert.Throws<InvalidOperationException>(() => Drain(routine));

            Assert.AreEqual("入れ子の中の失敗", error.Message);
            Assert.AreEqual(1, calls);
            CollectionAssert.Contains(log, "inner-finally");
        }

        [Test, Description("途中で Dispose されたときも後処理をちょうど 1 回呼び、進行中の入れ子の finally も実行させる")]
        public void Guard_CallsFinallyOnDispose()
        {
            int calls = 0;
            IEnumerator routine = CoroutineFlow.Guard(Body(false), () => calls++);
            routine.MoveNext(); // body-1
            routine.MoveNext(); // middle-1
            routine.MoveNext(); // inner-1（入れ子の try の中）

            ((IDisposable)routine).Dispose();

            Assert.AreEqual(1, calls);
            CollectionAssert.AreEqual(new[] { "inner-finally" }, log);
        }

        [Test, Description("本体が null なら ArgumentNullException を投げる")]
        public void Guard_NullBody_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => CoroutineFlow.Guard(null, () => { }).MoveNext());
        }
    }
}
