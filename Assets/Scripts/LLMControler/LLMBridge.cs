using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using System.Collections.Generic;
using System.Linq;

public class LLMBridge : MonoBehaviour
{
    // フロントでAPIキーを載せないようにプロキシサーバを利用
    // private const string CLAUDE_API_URL = "https://api.anthropic.com/v1/messages";
    private const string CLAUDE_API_URL = "https://llm-proxy.grapeoxygen.workers.dev";
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
    // public IEnumerator GetLLMResponse(string systemMessage, Message[] messages, System.Action<string> onPartialResponse = null, bool stream = true)
    // {
    //     ClaudeRequest requestData = new ClaudeRequest
    //     {
    //         system = systemMessage,
    //         messages = messages,  // 履歴全体を送信
    //         stream = true
    //     };


    //     string jsonData = JsonUtility.ToJson(requestData);
    //     byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
    //     // リクエストボディの表示（デバッグ用）
    //     Debug.Log($"Request Body: {jsonData}");

    //     using (UnityWebRequest request = new UnityWebRequest(CLAUDE_API_URL, "POST"))
    //     {
    //         request.uploadHandler = new UploadHandlerRaw(bodyRaw);
    //         request.downloadHandler = new StreamingDownloadHandler(onPartialResponse);

    //         request.SetRequestHeader("Content-Type", "application/json");
    //         // プロキシサーバに載せてもらうため不要
    //         // request.SetRequestHeader("x-api-key", PlayerPrefs.GetString("APIKey"));
    //         request.SetRequestHeader("anthropic-version", CLAUDE_VERSION);

    //         yield return request.SendWebRequest();

    //         if (request.result != UnityWebRequest.Result.Success)
    //         {
    //             Debug.LogError($"Claude API Error: {request.error}");
    //             yield return $"Error: {request.error}";
    //             yield break;
    //         }

    //         yield return ((StreamingDownloadHandler)request.downloadHandler).GetFullText();
    //     }
    // }

    // プロキシでストリーミングがうまくいかないので疑似ストリーミング
    public IEnumerator GetLLMResponse(
        string systemMessage,
        Message[] messages,
        System.Action<string> onPartialResponse = null
    )
    {
        ClaudeRequest requestData = new ClaudeRequest
        {
            system = systemMessage,
            messages = messages,
            stream = false   
        };

        string jsonData = JsonUtility.ToJson(requestData);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);

        using (UnityWebRequest request = new UnityWebRequest(CLAUDE_API_URL, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();

            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("anthropic-version", CLAUDE_VERSION);

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Claude API Error: {request.error}");
                yield break;
            }

            string responseText = request.downloadHandler.text;

            // Claudeの通常レスポンスをパース
            ClaudeResponse response =
                JsonUtility.FromJson<ClaudeResponse>(responseText);

            string fullText = response.content[0].text;

            // 疑似ストリーミング
            yield return StartCoroutine(
                PseudoStreaming(fullText, onPartialResponse)
            );
        }
    }

    private IEnumerator PseudoStreaming(
        string text,
        System.Action<string> onPartialResponse,
        float interval = 0.03f   // 表示速度（好みで調整）
    )
    {
        StringBuilder buffer = new StringBuilder();

        foreach (char c in text)
        {
            buffer.Append(c);
            onPartialResponse?.Invoke(buffer.ToString());
            yield return new WaitForSeconds(interval);
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
        // プロキシサーバに載せてもらうため不要
        // webRequest.SetRequestHeader("x-api-key", PlayerPrefs.GetString("APIKey"));
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
}