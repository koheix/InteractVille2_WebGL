using System;

//GPTに送るメッセージの構造体
[Serializable]
public struct ChatMessage
{
	public string role; //user, assistant
	public string content;
}

//GPTに送るPOSTリクエストに含めるJSONボディの構造体
[Serializable]
public struct ChatBody
{
	public string model;
	public ChatMessage[] messages;
	public int max_tokens;
	public float temperature;
	public float top_p;
	public float frequency_penalty;
	public float presence_penalty;
}

//GPTからのレスポンスの構造体
[Serializable]
public struct ChatResponse
{
	public string id;
	public string obj;
	public int created;
	public string model;
	public ChatUsage usage;
	public ChatChoice[] choices;
}

//GPTからのレスポンスの構造体の中のusageの構造体
[Serializable]
public struct ChatUsage
{
	public int prompt_tokens;
	public int completion_tokens;
	public int total_tokens;
}

//GPTからのレスポンスの構造体の中のchoicesの構造体
[Serializable]
public struct ChatChoice
{
	public ChatMessage message;
	public int index;
	public string finish_reason;
}