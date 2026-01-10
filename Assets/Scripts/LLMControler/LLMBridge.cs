using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using System.Collections.Generic;
using System.Linq;


public class LLMBridge : MonoBehaviour
{
    private const string CLAUDE_API_URL = "https://api.anthropic.com/v1/messages";
    private const string CLAUDE_VERSION = "2023-06-01";

    // Structured Output用のクラス
    [Serializable]
    public class ClaudeTool
    {
        public string name;
        public string description;
        public ToolInputSchema input_schema;
    }

    [Serializable]
    public class ToolInputSchema
    {
        public string type = "object";
        public ToolProperties properties;
        public string[] required;
    }

    [Serializable]
    public class ToolProperties
    {
        public ToolProperty result;
    }

    [Serializable]
    public class ToolProperty
    {
        public string type;
        public string description;
    }

    // レスポンスのJSONデータ構造
    [System.Serializable]
    private class ClaudeRequest
    {
        public string model = "claude-sonnet-4-20250514";
        public int max_tokens = 1024;
        // ロールを演じさせるためのシステムメッセージ
        public string system = null;
        public Message[] messages;
        public bool stream = false; // ストリーミングオプション
        public ClaudeTool[] tools = null;
    }

    [System.Serializable]
    private class ClaudeResponse
    {
        public string id;
        public string type;
        public string role;
        public Content[] content;
        public string model;
        public string stop_reason;
    }

    [System.Serializable]
    private class Content
    {
        public string type;
        public string text;
        public string id;
        public string name;
        public ToolInput input;
    }

    [Serializable]
    public class ToolInput
    {
        public int result;
    }

    /// <summary>
    /// Claude APIからレスポンスを取得する
    /// </summary>
    /// <param name="message">送信するメッセージ</param>
    /// <param name="APIKey">Claude APIキー</param>
    /// <returns>Claude APIからのレスポンステキスト</returns>
    // ストリーミング対応版
    public IEnumerator GetLLMResponse(string systemMessage, Message[] messages, System.Action<string> onPartialResponse = null, bool stream = true)
    {
        ClaudeRequest requestData = new ClaudeRequest
        {
            system = systemMessage,
            messages = messages,  // 履歴全体を送信
            stream = true
        };


        string jsonData = JsonUtility.ToJson(requestData);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
        // リクエストボディの表示（デバッグ用）
        Debug.Log($"Request Body: {jsonData}");

        using (UnityWebRequest request = new UnityWebRequest(CLAUDE_API_URL, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new StreamingDownloadHandler(onPartialResponse);

            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("x-api-key", PlayerPrefs.GetString("APIKey"));
            request.SetRequestHeader("anthropic-version", CLAUDE_VERSION);

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Claude API Error: {request.error}");
                yield return $"Error: {request.error}";
                yield break;
            }

            yield return ((StreamingDownloadHandler)request.downloadHandler).GetFullText();
        }
    }

    // カスタムDownloadHandler
    public class StreamingDownloadHandler : DownloadHandlerScript
    {
        private System.Action<string> onPartialResponse;
        private string fullText = "";

        public StreamingDownloadHandler(System.Action<string> callback) : base()
        {
            onPartialResponse = callback;
        }

        protected override bool ReceiveData(byte[] data, int dataLength)
        {
            if (data == null || dataLength == 0) return false;

            string chunk = Encoding.UTF8.GetString(data, 0, dataLength);

            // Server-Sent Events (SSE)形式をパース
            string[] lines = chunk.Split('\n');
            foreach (string line in lines)
            {
                if (line.StartsWith("data: "))
                {
                    string jsonData = line.Substring(6);
                    if (jsonData == "[DONE]") continue;

                    try
                    {
                        // Claude streaming responseのパース
                        var streamEvent = JsonUtility.FromJson<ClaudeStreamEvent>(jsonData);

                        if (streamEvent.type == "content_block_delta" &&
                            streamEvent.delta != null &&
                            !string.IsNullOrEmpty(streamEvent.delta.text))
                        {
                            fullText += streamEvent.delta.text;
                            onPartialResponse?.Invoke(fullText);
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"Parse error: {e.Message}");
                    }
                }
            }

            return true;
        }

        public string GetFullText()
        {
            return fullText;
        }
    }

    // Streaming用のデータクラス
    [System.Serializable]
    public class ClaudeStreamEvent
    {
        public string type;
        public StreamDelta delta;
    }

    [System.Serializable]
    public class StreamDelta
    {
        public string type;
        public string text;
    }

    // テスト
    // private void Start()
    // {
    //     // StartCoroutine(ExampleUsage());
    // }
    // void Start()
    // {
    //     // 使用例
    //     Debug.Log("Testing GetLLMStructuredOutputResponse...");
    //     StartCoroutine(
    //     GetLLMStructuredOutputResponse(
    //         name: "return_calculation",
    //         description: "Returns the result of a calculation",
    //         "以下の記憶データから、ハムスターのValenceを算出してください。(0~100の範囲で数値を返してください）\n" +
    //         "記憶データ:\n" +
    //         "• こうへいくんという名前のユーザーで、親しみやすい関係性を築いている\n• 家族でアウトレットに出かけ、紺色のニットのトップスを購入した\n• 新しい服の購入を喜んでおり、ファッションに関心がある様子" +
    //         "• こうへいくんとの楽しい会話の時間を共有し、お互いに幸せな気持ちになれる関係性を築いている\n• こうへいくんは「たのしいね」という素直で前向きな表現をする人柄である\n• 私たちの会話は温かく親しみやすい雰囲気で進行している" +
    //         "申し訳ないのですが、今回が私たちの最初の会話です。\n\nこれまでの会話履歴は：\n1. あなたが「はなせる？」と質問\n2. 私が「話せるよ！こうへいくん、元気だった？」と返答\n\nまだ会話が始まったばかりで、要約できる重要な思い出や情報は蓄積されていません。もう少し会話を続けてから、改めて要約をお願いしていただけますか？" +
    //         "• ユーザーは「こうへいくん」という名前で呼ばれることを好む\n• 日常的な挨拶として「こんにちは」を使用する\n• カジュアルで親しみやすいコミュニケーションスタイルを好む",
    //         onComplete: result => Debug.Log(result),
    //         onError: error => Debug.LogError(error)
    //     ));
    // }

    // メッセージ履歴を管理するクラス
    [System.Serializable]
    public class ConversationHistory
    {
        public List<Message> messages = new List<Message>();

        public void AddUserMessage(string content)
        {
            messages.Add(new Message { role = "user", content = content });
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

        public string MessagesToString()
        {
            if (messages.Count == 0) return string.Empty;
            return string.Join("\n", messages.Take(messages.Count - 1).Select(m => $"{m.role}: {m.content}"));
        }
    }

    // private IEnumerator ExampleUsage()
    // {
    //     Debug.Log("Sending request to Claude API...");

    //     IEnumerator responseCoroutine = GetLLMResponse("元気ですか？");
    //     yield return StartCoroutine(responseCoroutine);

    //     // レスポンスの取得
    //     string response = responseCoroutine.Current as string;
    //     Debug.Log($"Claude Response: {response}");
    // }

    // Structured Outputを使ったLLM応答を取得するmethod
    // とりあえず数値を返すものに対応
    public IEnumerator GetLLMStructuredOutputResponse(string resultType, string name, string description, string question, Action<float> onComplete, Action<string> onError)
    {
        // ツールの定義
        ClaudeTool[] tools = new ClaudeTool[]
        {
            new ClaudeTool
            {
                // name = "return_calculation",
                // description = "Returns the result of a calculation",
                name = name,
                description = description,
                input_schema = new ToolInputSchema
                {
                    type = "object",
                    properties = new ToolProperties
                    {
                        result = new ToolProperty
                        {
                            type = resultType,
                            description = "The " + resultType + " result"
                        }
                    },
                    required = new string[] { "result" }
                }
            }
        };

        // リクエストの作成
        ClaudeRequest request = new ClaudeRequest
        {
            model = "claude-sonnet-4-20250514",
            max_tokens = 1024,
            messages = new Message[]
            {
                new Message
                {
                    role = "user",
                    // content = question + " Use the return_calculation tool to return the result."
                    content = question
                }
            },
            tools = tools
        };

        string jsonRequest = JsonUtility.ToJson(request);
        // リクエストボディの表示（デバッグ用）
        Debug.Log($"Request Body(Structured output): {jsonRequest}");

        // UnityWebRequestの作成
        UnityWebRequest webRequest = new UnityWebRequest(CLAUDE_API_URL, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonRequest);
        webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
        webRequest.downloadHandler = new DownloadHandlerBuffer();

        // ヘッダーの設定
        webRequest.SetRequestHeader("Content-Type", "application/json");
        webRequest.SetRequestHeader("x-api-key", PlayerPrefs.GetString("APIKey"));
        webRequest.SetRequestHeader("anthropic-version", CLAUDE_VERSION);

        // リクエスト送信
        yield return webRequest.SendWebRequest();

        if (webRequest.result == UnityWebRequest.Result.Success)
        {
            string responseText = webRequest.downloadHandler.text;
            ClaudeResponse response = JsonUtility.FromJson<ClaudeResponse>(responseText);

            // ツール使用のブロックを探す
            bool found = false;
            foreach (var block in response.content)
            {
                if (block.type == "tool_use" && block.name == "return_calculation")
                {
                    Debug.Log($"Structured Output Result: {block.input.result}");
                    int result = block.input.result;
                    onComplete?.Invoke(result);
                    found = true;
                    break;
                }
                else if (block.type == "tool_use" && block.name == "return_mood")
                {
                    Debug.Log($"Structured Output Result: {block.input.result}");
                    string result = block.input.result.ToString();
                    onComplete?.Invoke(float.Parse(result));
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                onError?.Invoke("Tool use not found in response");
            }
        }
        else
        {
            string errorMessage = $"Error: {webRequest.error}\nResponse: {webRequest.downloadHandler.text}";
            Debug.LogError(errorMessage);
            onError?.Invoke(errorMessage);
        }

        webRequest.Dispose();
    }

    // // Structured Outputを使ったLLM応答を取得するmethod
    // // stringを返すものに対応
    // public IEnumerator GetLLMStructuredOutputStringResponse(string name, string description, string question, Action<string> onComplete, Action<string> onError)
    // {
    //     // ツールの定義
    //     ClaudeTool[] tools = new ClaudeTool[]
    //     {
    //         new ClaudeTool
    //         {
    //             name = name,
    //             description = description,
    //             input_schema = new ToolInputSchema
    //             {
    //                 type = "object",
    //                 properties = new ToolProperties
    //                 {
    //                     result = new ToolProperty
    //                     {
    //                         type = "string",
    //                         description = "The string result"
    //                     }
    //                 },
    //                 required = new string[] { "result" }
    //             }
    //         }
    //     };

    //     // リクエストの作成
    //     ClaudeRequest request = new ClaudeRequest
    //     {
    //         model = "claude-sonnet-4-20250514",
    //         max_tokens = 1024,
    //         messages = new Message[]
    //         {
    //             new Message
    //             {
    //                 role = "user",
    //                 content = question
    //             }
    //         },
    //         tools = tools
    //     };

    //     string jsonRequest = JsonUtility.ToJson(request);
    //     // リクエストボディの表示（デバッグ用）
    //     Debug.Log($"Request Body(Structured output): {jsonRequest}");

    //     // UnityWebRequestの作成
    //     UnityWebRequest webRequest = new UnityWebRequest(CLAUDE_API_URL, "POST");
    //     byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonRequest);
    //     webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
    //     webRequest.downloadHandler = new DownloadHandlerBuffer();

    //     // ヘッダーの設定
    //     webRequest.SetRequestHeader("Content-Type", "application/json");
    //     webRequest.SetRequestHeader("x-api-key", PlayerPrefs.GetString("APIKey"));
    //     webRequest.SetRequestHeader("anthropic-version", CLAUDE_VERSION);
    //     // リクエスト送信
    //     yield return webRequest.SendWebRequest();
    //     if (webRequest.result == UnityWebRequest.Result.Success)
    //     {
    //         string responseText = webRequest.downloadHandler.text;
    //         ClaudeResponse response = JsonUtility.FromJson<ClaudeResponse>(responseText);

    //         // ツール使用のブロックを探す
    //         bool found = false;
    //         foreach (var block in response.content)
    //         {
    //             if (block.type == "tool_use" && block.name == name)
    //             {
    //                 Debug.Log($"Structured Output Result: {block.input.result}");
    //                 string result = block.input.result.ToString();
    //                 onComplete?.Invoke(result);
    //                 found = true;
    //                 break;
    //             }
    //         }

    //         if (!found)
    //         {
    //             onError?.Invoke("Tool use not found in response");
    //         }
    //     }
    //     else
    //     {
    //         string errorMessage = $"Error: {webRequest.error}\nResponse: {webRequest.downloadHandler.text}";
    //         Debug.LogError(errorMessage);
    //         onError?.Invoke(errorMessage);
    //     }
    //     webRequest.Dispose();
    // }

}