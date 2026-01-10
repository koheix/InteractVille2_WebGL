/*
 * Message.cs
 * 
 * LLMに送信するメッセージデータ構造
 */

[System.Serializable]
public class Message
{
    public string role;
    public string content;
}
