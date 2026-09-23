using System;
using System.Collections;
using System.Collections.Generic;

namespace InteractVille2.LLM
{
    /// <summary>コルーチンの後処理を保証するための道具。</summary>
    public static class CoroutineFlow
    {
        /// <summary>
        /// body を最後まで進め、正常終了・例外・途中での Dispose のどの場合も onFinally をちょうど 1 回呼ぶ。
        ///
        /// Unity に `yield return 入れ子の IEnumerator` を渡すと、Unity が入れ子を直接進めるので、
        /// 入れ子の中の例外は外側の try/finally を通らない。ここでは入れ子の IEnumerator も自分で MoveNext して
        /// 進め、例外が必ずこのメソッドの finally を通るようにする。IEnumerator 以外（WaitForSeconds、
        /// AsyncOperation など）は、そのまま Unity に渡して待ってもらう。
        /// </summary>
        public static IEnumerator Guard(IEnumerator body, Action onFinally)
        {
            if (body == null) throw new ArgumentNullException(nameof(body));

            var stack = new Stack<IEnumerator>();
            stack.Push(body);
            try
            {
                while (stack.Count > 0)
                {
                    IEnumerator top = stack.Peek();
                    if (!top.MoveNext())
                    {
                        stack.Pop();
                        (top as IDisposable)?.Dispose();
                        continue;
                    }
                    if (top.Current is IEnumerator nested)
                    {
                        stack.Push(nested);
                        continue;
                    }
                    yield return top.Current;
                }
            }
            finally
            {
                try
                {
                    // 例外や途中の Dispose で抜けたときは、進行中の入れ子の finally も実行させる
                    while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose();
                }
                finally
                {
                    onFinally?.Invoke();
                }
            }
        }
    }
}
