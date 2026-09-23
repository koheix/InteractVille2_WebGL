using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using InteractVille2.LLM;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// LLM プロキシ（server/llm-proxy、Cloudflare Workers）との通信。
///
/// プロキシが Workers AI / Claude のどちらを使うかは Worker 側の設定で決まるので、
/// ここではプロバイダを意識しない。リクエストの組み立てと応答の解釈は Core の LlmApi が行う。
/// どの呼び出しも、成功・失敗・タイムアウトのいずれでも onComplete をちょうど 1 回呼ぶ。
/// </summary>
public class LLMBridge : MonoBehaviour
{
    // API キーはプロキシ側にあり、クライアントは URL だけを知る。
    private const string PROXY_URL = "https://llm-proxy.grapeoxygen.workers.dev";

    // エディタでだけ、環境変数でプロキシの URL を差し替えられる（ローカルの wrangler dev で失敗経路を確かめるため）。
    // 例: $env:IV2_LLM_PROXY_URL = "http://localhost:8787" を設定してから Unity を起動する。ビルドには影響しない。
    private const string PROXY_URL_ENV = "IV2_LLM_PROXY_URL";

    private static string ProxyUrl
    {
        get
        {
#if UNITY_EDITOR
            string overridden = Environment.GetEnvironmentVariable(PROXY_URL_ENV);
            if (!string.IsNullOrWhiteSpace(overridden)) return overridden.TrimEnd('/');
#endif
            return PROXY_URL;
        }
    }

    // 応答が返らないとコルーチンが永久に待ち、タイトルへ戻る処理も終わらなくなるので必ず区切る。
    public const int ChatTimeoutSeconds = 60;
    public const int ScoreTimeoutSeconds = 30;

    // 疑似ストリーミングの 1 文字あたりの表示間隔（秒）
    private const float StreamingInterval = 0.03f;

    /// <summary>会話・要約・気分を生成する（POST /chat）。</summary>
    public IEnumerator Chat(string systemMessage, IReadOnlyList<Message> messages, Action<LlmResult> onComplete)
    {
        string json;
        try
        {
            json = LlmApi.BuildChatRequestJson(systemMessage, ConversationRules.TrimForRequest(messages));
        }
        catch (ArgumentException e)
        {
            Debug.LogError($"[LLM] 会話リクエストを組み立てられませんでした: {e.Message}");
            onComplete?.Invoke(LlmResult.Fail(LlmErrorKind.BadRequest));
            yield break;
        }
        yield return Post(LlmApi.ChatPath, json, ChatTimeoutSeconds, LlmApi.ParseChatResponse, onComplete);
    }

    /// <summary>0〜100 のスコア（親密度・感情価・覚醒度）を求める（POST /score）。</summary>
    public IEnumerator Score(string prompt, Action<LlmResult> onComplete)
    {
        string json;
        try
        {
            json = LlmApi.BuildScoreRequestJson(prompt);
        }
        catch (ArgumentException e)
        {
            Debug.LogError($"[LLM] スコアのリクエストを組み立てられませんでした: {e.Message}");
            onComplete?.Invoke(LlmResult.Fail(LlmErrorKind.BadRequest));
            yield break;
        }
        yield return Post(LlmApi.ScorePath, json, ScoreTimeoutSeconds, LlmApi.ParseScoreResponse, onComplete);
    }

    /// <summary>文章を 1 文字（書記素）ずつ伸ばして表示する（プロキシはストリーミングしないので疑似的に行う）。</summary>
    public IEnumerator PseudoStreaming(string text, Action<string> onPartialResponse)
    {
        foreach (string prefix in ConversationRules.StreamingPrefixes(text))
        {
            onPartialResponse?.Invoke(prefix);
            yield return new WaitForSeconds(StreamingInterval);
        }
    }

    private IEnumerator Post(string path, string json, int timeoutSeconds, Func<long, string, bool, LlmResult> parse, Action<LlmResult> onComplete)
    {
        using (UnityWebRequest request = new UnityWebRequest(ProxyUrl + path, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(LlmApi.ToRequestBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = timeoutSeconds;

            yield return request.SendWebRequest();

            // 接続失敗・タイムアウトは ConnectionError。4xx/5xx は ProtocolError で、本文にエラー種別が入っている。
            bool networkError = request.result == UnityWebRequest.Result.ConnectionError;
            LlmResult result;
            try
            {
                result = parse(request.responseCode, request.downloadHandler?.text, networkError);
            }
            catch (Exception e)
            {
                Debug.LogError($"[LLM] {path} の応答を解釈できませんでした: {e.Message}");
                result = LlmResult.Fail(LlmErrorKind.Upstream);
            }

            if (!result.IsSuccess)
            {
                Debug.LogWarning($"[LLM] {path} が失敗しました: {result.Error}（HTTP {request.responseCode}, {request.error}）");
            }
            onComplete?.Invoke(result);
        }
    }

    // メッセージ履歴を管理するクラス
    [System.Serializable]
    public class ConversationHistory
    {
        public List<Message> messages = new List<Message>();

        /// <summary>ユーザーの発言を追加する。失敗時に取り除けるよう、追加したメッセージを返す。</summary>
        public Message AddUserMessage(string content)
        {
            var message = new Message { role = "user", content = content };
            messages.Add(message);
            return message;
        }

        public void AddAssistantMessage(string content)
        {
            messages.Add(new Message { role = "assistant", content = content });
        }

        // 指定した数の最新メッセージを配列で取得
        public Message[] ToArray(int count = 100)
        {
            if (count <= 0)
                return new Message[0];

            if (count >= messages.Count)
                return messages.ToArray();

            return messages.Skip(messages.Count - count).ToArray();
        }

        public void Clear()
        {
            messages.Clear();
        }

        // プロンプト用の会話ログは ConversationRules.FormatForPrompt で作る。以前の MessagesToString は
        // 「最後の 1 件は要約の指示」という前提で末尾を落としていたため、要約の前に呼ぶと本物の発言が消えていた。
    }
}
