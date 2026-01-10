/* ともハムのステータスを管理するスクリプト
 * 機嫌や満腹度の管理など
 * ステータスはLLMの応答生成、行動選択に影響を与えるかつLLMの出力で変化する
 * よって外からも読み書きできるようにする
 * 発話メソッドなども担う
 */

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;
using System.Text.RegularExpressions;
using System.Linq;


public class FriendHamStatus : MonoBehaviour
{
    // LLMBridgeの参照
    [Header("LLM Bridge Reference")]
    public LLMBridge llmBridge;

    [Header("Friend Ham Status Reference")]
    [SerializeField] private TextMeshProUGUI moodText;
    [SerializeField] private Image closenessGauge;
    [SerializeField] private TextMeshProUGUI closenessGaugeText;

    // valence(0 ~ 100で表現し、getterとsetterで制御する)
    private int valence = 50;
    public int Valence
    {
        get { return valence; }
        set { valence = Mathf.Clamp(value, 0, 100); }
    }
    // arousal(0 ~ 100で表現し、getterとsetterで制御する)
    private int arousal = 50;
    public int Arousal
    {
        get { return arousal; }
        set { arousal = Mathf.Clamp(value, 0, 100); }
    }
    // hunger(0 ~ 100で表現し、getterとsetterで制御する)
    private int hunger = 50;
    public int Hunger
    {
        get { return hunger; }
        set { hunger = Mathf.Clamp(value, 0, 100); }
    }
    private int closeness = 50;
    public int Closeness
    {
        get { return closeness; }
        set { closeness = Mathf.Clamp(value, 0, 100); }
    }
    private string currentMood = "普通";
    public string CurrentMood
    {
        get { return currentMood; }
        set { currentMood = value; }
    }

    // memory(ともハムの記憶を保存するための文字列リスト)
    // ゲームが終了するときに保存する(SaveDaoを使う)
    // public List<string> memory = new List<string>();
    // memory = SaveDao.LoadData(PlayerPrefs.GetString("userName", "default"), data => data.friendHamMemory);
    public List<string> memory;
    private const int MaxMemorySize = 20; // 最大メモリ数
    private LLMBridge.ConversationHistory conversationHistory = new LLMBridge.ConversationHistory();
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        memory = SaveDao.LoadData(PlayerPrefs.GetString("userName", "default"), data => data.friendHamMemory);
        valence = SaveDao.LoadData(PlayerPrefs.GetString("userName", "default"), data => data.friendHamValence);
        arousal = SaveDao.LoadData(PlayerPrefs.GetString("userName", "default"), data => data.friendHamArousal);
        hunger = SaveDao.LoadData(PlayerPrefs.GetString("userName", "default"), data => data.friendHamHunger);
        closeness = SaveDao.LoadData(PlayerPrefs.GetString("userName", "default"), data => data.friendHamCloseness);
        currentMood = SaveDao.LoadData(PlayerPrefs.GetString("userName", "default"), data => data.friendHamCurrentMood);
        UpdateMoodUI();
        UpdateClosenessUI();
        // Debug.Log($"FriendHamStatus: Loaded memory count = {memory.Count}, Valence={Valence}, Arousal={Arousal}, Hunger={Hunger}");
    }
    void OnEnable()
    {
        Debug.Log("FriendHamStatus: Registering SaveMemory task to QuitManager");
        // QuitManager.Instance.AddReturn2TitleTask(SaveMemory());
        QuitManager.Instance.AddReturn2TitleTask(SaveMemoryAndCloseness());
        QuitManager.Instance.AddReturn2TitleTask(SaveValence());
        QuitManager.Instance.AddReturn2TitleTask(SaveArousal());
        // QuitManager.Instance.AddReturn2TitleTask(SaveCloseness());
        QuitManager.Instance.AddReturn2TitleTask(SaveCurrentMood());
    }

    void OnDisable()
    {
        // 会話をしていれば会話履歴を更新する
        if (conversationHistory.messages.Count > 1){
            // 会話履歴を保存しておく
            Debug.Log("FriendHamStatus: 会話履歴の保存");
            List<Message> conversationLog = new List<Message>();
            conversationLog = SaveDao.LoadData(PlayerPrefs.GetString("userName", "default"), data => data.conversationHistory);
            // if (conversationHistory.messages.Count > 1)
            // {
            conversationHistory.messages.RemoveAt(conversationHistory.messages.Count - 1);
            conversationLog.AddRange(conversationHistory.messages);
            // }
            SaveDao.UpdateData(PlayerPrefs.GetString("userName", "default"), data => data.conversationHistory = conversationLog);
            conversationHistory.Clear();
        }
        
    }

    // Update is called once per frame
    void Update()
    {

    }

    // UIの機嫌表示を更新
    public void UpdateMoodUI()
    {
        if (moodText != null)
        {
            moodText.text = CurrentMood;
        }
    }

    // closenessのUIを更新するメソッド
    private void UpdateClosenessUI()
    {
        closenessGauge.fillAmount = closeness / 100f;
        closenessGaugeText.text = closeness.ToString() + "％";
    }

    public IEnumerator Speak(string message, System.Action<string> onUpdate, System.Action<string> onComplete = null)
    {
        Debug.Log("[Friend Ham]Sending request to Claude API...");

        // ユーザーメッセージを履歴に追加
        conversationHistory.AddUserMessage(message);

        string finalResponse = "";

        IEnumerator responseCoroutine = llmBridge.GetLLMResponse(
            "あなたは親しみやすい友達のハムスターです。\n" +
            "ただし、メッセージは1から3文程度の短い文章で答えてください。\n" +
            "また、メッセージのみで、描写は含めないでください。\n" + 
            "現在時刻: " + TimeUtil.GetCurrentTimeString() + "\n" +
            "前回の会話をした時間:" + ExtractDateTime(memory) + "\n" +
            "以下はこのユーザーとの会話でのあなたの記憶です。" + 
            string.Join("\n", memory) +
            "前回の会話後のValence: " + Valence.ToString() + "\n" +
            "前回の会話後のArousal: " + Arousal.ToString() + "\n" +
            "前回の会話後のCloseness: " + Closeness.ToString() + "\n" +
            "以下はあなたのアクティビティ（家具を配置したことやプレイヤーにプレゼントされたものと時間）です。" +
            // string.Join("\n", SaveDao.LoadData(PlayerPrefs.GetString("userName", "default"), data => data.friendHamActivityMemory)) + // 最新の3つ程度に制限する
            string.Join("\n", SaveDao.LoadData(PlayerPrefs.GetString("userName", "default"), data => data.friendHamActivityMemory).TakeLast(3).ToList()) +
            "これらの情報を元に、以下のユーザーメッセージに返答してください。",  // システムメッセージ
            conversationHistory.ToArray(),  // 履歴全体を送信
            (partialText) =>
            {
                finalResponse = partialText;
                onUpdate?.Invoke(partialText);
            }
        );

        yield return StartCoroutine(responseCoroutine);

        // アシスタントの返答を履歴に追加
        conversationHistory.AddAssistantMessage(finalResponse);

        // // メモリにも保存
        // memory.Add($"User: {message}");
        // memory.Add($"Assistant: {finalResponse}");
        DebugPrintConversation();

        onComplete?.Invoke(finalResponse);
    }

    // 会話履歴をクリア
    public void ClearConversation()
    {
        conversationHistory.Clear();
        // memory.Clear();
    }

    public void DebugPrintConversation()
    {
        Debug.Log("=== Conversation History ===");
        foreach (var msg in conversationHistory.messages)
        {
            Debug.Log($"{msg.role}: {msg.content}");
        }
    }

    // ゲーム終了時に履歴をLLMに渡してメモリを保存する
    // public IEnumerator SaveMemory()
    // {
    //     // 会話履歴が空の場合はスキップ
    //     if(conversationHistory.messages.Count == 0)
    //     {
    //         Debug.Log("[Friend Ham]会話履歴が空のため、メモリ保存をスキップします。");
    //         yield break;
    //     }
    //     // LLMに履歴を渡してメモリを生成する
    //     Debug.Log("[Friend Ham]メモリを生成中...");

    //     string finalResponse = "";
        
    //     // 要約支持を履歴に追加
    //     string message = "これまでの会話履歴から、あなたとの重要な思い出や情報を3つ程度要約してメモリとして保存してください。" +
    //                      "それぞれは短い文章で表現してください。" +
    //                      "また、箇条書き形式で、その内容だけを出力してください。";
    //     conversationHistory.AddUserMessage(message);

    //     IEnumerator responseCoroutine = llmBridge.GetLLMResponse(
    //         "最近の会話履歴から重要な情報を整理し、要約してください。",  // システムメッセージ
    //         conversationHistory.ToArray(),  // 履歴全体を送信
    //         (partialText) =>
    //         {
    //             finalResponse = partialText;
    //         }
    //         // stream: false  // ストリーミングは不要
    //     );
    //     // StartCoroutine(responseCoroutine);
    //     yield return StartCoroutine(responseCoroutine);
    //     Debug.Log($"[Friend Ham]生成されたメモリ: {finalResponse}");
    //     // memoryに保存
    //     memory.Add(finalResponse + $"\n ({TimeUtil.GetCurrentTimeString()})");
    //     // MaxMemorySize 個を超えたら古いものから削除
    //     if (memory.Count > MaxMemorySize)
    //     {
    //         memory.RemoveAt(0);
    //     }

    //     // ここで履歴を保存する処理を追加
    //     // SaveDaoを使って保存
    //     SaveDao.UpdateData(PlayerPrefs.GetString("userName", "default"), data => data.friendHamMemory = memory);
    // }

    public IEnumerator SaveMemoryAndCloseness()
    {
        // 会話履歴が空の場合はスキップ
        if(conversationHistory.messages.Count == 0)
        {
            Debug.Log("[Friend Ham]会話履歴が空のため、Closeness保存をスキップします。");
            yield break;
        }
        // ---------------友ハムの親密度を更新して保存する
        Debug.Log("[Friend Ham]Closenessステータスを更新中...");
        // ClosenessをLLMに計算させる
        yield return StartCoroutine(
        llmBridge.GetLLMStructuredOutputResponse(
            resultType: "number",
            name: "return_calculation",
            description: "Returns the result of a calculation",
            "以下の会話データから、ハムスターの親密度(Closeness)を算出してください。(0~100の範囲で数値を返してください）\n" +
            "最後に会話をしてから時間が経過していたら、経過している時間が長いほど親密度を下げてください。" +
            "会話データ:" +
            conversationHistory.MessagesToString() +
            "\n最後に会話をした時間:" + ExtractDateTime(memory) + 
            "\n現在の時間:" + TimeUtil.GetCurrentTimeString() +
            "\n会話前のCloseness:" + Closeness.ToString(),
            onComplete: result => Closeness = Mathf.Clamp((int)result, 0, 100),
            onError: error => Debug.LogError(error)
        ));
        //
        Debug.Log("以下の情報をもとにClosenessを計算しました: " +
            "\n最後に会話をした時間:" + ExtractDateTime(memory) + 
            "\n現在の時間:" + TimeUtil.GetCurrentTimeString() +
            "\n会話前のCloseness:" + Closeness.ToString()
        );
        //
        Debug.Log($"[Friend Ham]ステータス更新完了: Closeness={Closeness}");
        // SaveDaoを使って保存
        SaveDao.UpdateData(PlayerPrefs.GetString("userName", "default"), data => data.friendHamCloseness = Closeness);
        // 親密度はこの会話より前のメモリを使用するので、メモリ保存は必ず後に処理する
        // ---------------LLMに履歴を渡してメモリを生成する
        Debug.Log("[Friend Ham]メモリを生成中...");

        string finalResponse = "";
        
        // 要約支持を履歴に追加
        string message = "これまでの会話履歴から、あなたとの重要な思い出や情報を3つ程度要約してメモリとして保存してください。" +
                         "それぞれは短い文章で表現してください。" +
                         "また、箇条書き形式で、その内容だけを出力してください。";
        conversationHistory.AddUserMessage(message);

        IEnumerator responseCoroutine = llmBridge.GetLLMResponse(
            "最近の会話履歴から重要な情報を整理し、要約してください。",  // システムメッセージ
            conversationHistory.ToArray(),  // 履歴全体を送信
            (partialText) =>
            {
                finalResponse = partialText;
            }
            // stream: false  // ストリーミングは不要
        );
        // StartCoroutine(responseCoroutine);
        yield return StartCoroutine(responseCoroutine);
        Debug.Log($"[Friend Ham]生成されたメモリ: {finalResponse}");
        // memoryに保存
        memory.Add(finalResponse + $"\n ({TimeUtil.GetCurrentTimeString()})");
        // MaxMemorySize 個を超えたら古いものから削除
        if (memory.Count > MaxMemorySize)
        {
            memory.RemoveAt(0);
        }

        // ここで履歴を保存する
        SaveDao.UpdateData(PlayerPrefs.GetString("userName", "default"), data => data.friendHamMemory = memory);
    }

    // ゲーム終了時にValenceを保存する
    public IEnumerator SaveValence()
    {
        // 会話履歴が空の場合はスキップ
        if(conversationHistory.messages.Count == 0)
        {
            Debug.Log("[Friend Ham]会話履歴が空のため、Valence保存をスキップします。");
            yield break;
        }
        // 友ハムの各種ステータスも更新して保存する
        Debug.Log("[Friend Ham]Valenceステータスを更新中...");
        // ValenceをLLMに計算させる
        yield return StartCoroutine(
        llmBridge.GetLLMStructuredOutputResponse(
            resultType: "number",
            name: "return_calculation",
            description: "Returns the result of a calculation",
            "以下の会話データから、ハムスターの感情価(Valence)を算出してください。(0~100の範囲で数値を返してください）\n" +
            "会話データ:" +
            conversationHistory.MessagesToString() +
            "\n会話前のValence:" + Valence.ToString(),
            onComplete: result => Valence = Mathf.Clamp((int)result, 0, 100),
            onError: error => Debug.LogError(error)
        ));
        Debug.Log($"[Friend Ham]ステータス更新完了: Valence={Valence}");
        // SaveDaoを使って保存
        SaveDao.UpdateData(PlayerPrefs.GetString("userName", "default"), data => data.friendHamValence = Valence);
    }

    // ゲーム終了時にArousalを保存する
    public IEnumerator SaveArousal()
    {
        // 会話履歴が空の場合はスキップ
        if(conversationHistory.messages.Count == 0)
        {
            Debug.Log("[Friend Ham]会話履歴が空のため、Arousal保存をスキップします。");
            yield break;
        }
        // 友ハムの各種ステータスも更新して保存する
        Debug.Log("[Friend Ham]Arousalステータスを更新中...");
        // ArousalをLLMに計算させる
        yield return StartCoroutine(
        llmBridge.GetLLMStructuredOutputResponse(
            resultType: "number",
            name: "return_calculation",
            description: "Returns the result of a calculation",
            "以下の会話データから、ハムスターの覚醒度(Arousal)を算出してください。(0~100の範囲で数値を返してください）\n" +
            "会話データ:" +
            conversationHistory.MessagesToString() +
            "\n会話前のArousal:" + Arousal.ToString(),
            onComplete: result => Arousal = Mathf.Clamp((int)result, 0, 100),
            onError: error => Debug.LogError(error)
        ));
        Debug.Log($"[Friend Ham]ステータス更新完了: Arousal={Arousal}");
        // SaveDaoを使って保存
        SaveDao.UpdateData(PlayerPrefs.GetString("userName", "default"), data => data.friendHamArousal = Arousal);
    }
    // ゲーム終了時にClosenessを保存する
    // public IEnumerator SaveCloseness()
    // {
    //     // 会話履歴が空の場合はスキップ
    //     if(conversationHistory.messages.Count == 0)
    //     {
    //         Debug.Log("[Friend Ham]会話履歴が空のため、Closeness保存をスキップします。");
    //         yield break;
    //     }
    //     // 友ハムの各種ステータスも更新して保存する
    //     Debug.Log("[Friend Ham]Closenessステータスを更新中...");
    //     // ClosenessをLLMに計算させる
    //     yield return StartCoroutine(
    //     llmBridge.GetLLMStructuredOutputResponse(
    //         resultType: "number",
    //         name: "return_calculation",
    //         description: "Returns the result of a calculation",
    //         "以下の会話データから、ハムスターの親密度(Closeness)を算出してください。(0~100の範囲で数値を返してください）\n" +
    //         "最後に会話をしてから時間が経過していたら、経過している時間が長いほど親密度を下げてください。" +
    //         "会話データ:" +
    //         conversationHistory.MessagesToString() +
    //         "\n最後に会話をした時間:" + ExtractDateTime(memory) + 
    //         "\n現在の時間:" + TimeUtil.GetCurrentTimeString() +
    //         "\n会話前のCloseness:" + Closeness.ToString(),
    //         onComplete: result => Closeness = Mathf.Clamp((int)result, 0, 100),
    //         onError: error => Debug.LogError(error)
    //     ));
    //     //
    //     Debug.Log("以下の情報をもとにClosenessを計算しました: " +
    //         "\n最後に会話をした時間:" + ExtractDateTime(memory) + 
    //         "\n現在の時間:" + TimeUtil.GetCurrentTimeString() +
    //         "\n会話前のCloseness:" + Closeness.ToString()
    //     );
    //     //
    //     Debug.Log($"[Friend Ham]ステータス更新完了: Closeness={Closeness}");
    //     // SaveDaoを使って保存
    //     SaveDao.UpdateData(PlayerPrefs.GetString("userName", "default"), data => data.friendHamCloseness = Closeness);
    // }

    // memoryから最後の日時情報を抽出するヘルパーメソッド
    private string ExtractDateTime(List<string> memory)
    {
        if (memory.Count == 0)
        {
            return "なし";
        }

        string lastMemory = memory[memory.Count - 1];
        
        // 括弧内の日時を抽出する正規表現
        // \(  - 開き括弧
        // (.+?) - 括弧内の内容（非貪欲マッチ）
        // \)  - 閉じ括弧
        Match match = Regex.Match(lastMemory, @"\((.+?)\)$");
        
        if (match.Success)
        {
            return match.Groups[1].Value;
        }
        
        return "なし(会話の記憶がありません)";
    }

    // ゲーム終了時にCurrentMoodを保存する
    public IEnumerator SaveCurrentMood()
    {
        // 会話履歴が空の場合はスキップ
        if(conversationHistory.messages.Count == 0)
        {
            Debug.Log("[Friend Ham]会話履歴が空のため、CurrentMood保存をスキップします。");
            yield break;
        }
        // 友ハムの各種ステータスも更新して保存する
        Debug.Log("[Friend Ham]CurrentMoodステータスを更新中...");
        // CurrentMoodをLLMに計算させる
        // yield return StartCoroutine(
        // llmBridge.GetLLMStructuredOutputResponse(
        //     resultType: "string",
        //     name: "return_mood",
        //     description: "Returns the current mood as a string",
        //     "以下の会話データから、ハムスターの現在の機嫌(CurrentMood)を一言で表現してください。(例: '喜び', '悲しみ', '怒り'など）\n" +
        //     "会話後のValence:" + Valence.ToString() +
        //     "\n会話後のArousal:" + Arousal.ToString(),
        //     onComplete: result => CurrentMood = result.ToString(),
        //     onError: error => Debug.LogError(error)
        // ));
        yield return StartCoroutine(
            llmBridge.GetLLMResponse(
                "以下のValenceとArousalデータから、ハムスターの現在の感情を一言で表現してください。(例: '喜び', '悲しみ', '怒り'など）\n" +
                "会話後のValence:" + Valence.ToString() +
                "\n会話後のArousal:" + Arousal.ToString(),  // システムメッセージ
                new Message[] {
                    new Message {role = "user", content = "必ず4文字以内で、感情のみを回答してください。"}
                },
                (partialText) =>
                {
                    CurrentMood = partialText;
                }
            )
        );
        Debug.Log($"[Friend Ham]ステータス更新完了: CurrentMood={CurrentMood}");
        // SaveDaoを使って保存
        SaveDao.UpdateData(PlayerPrefs.GetString("userName", "default"), data => data.friendHamCurrentMood = CurrentMood);
    }

    // // ともハムが食べ物を持っていて、満腹度が低い場合に食べさせるメソッド
    // public IEnumerator TryFeedFriendHam(System.Action onComplete = null)
    // {
    //     FriendHamItemManager itemManager = FindObjectOfType<FriendHamItemManager>();
    //     if (itemManager == null)
    //     {
    //         Debug.LogError("FriendHamItemManagerが見つかりません。");
    //         yield break;
    //     }

    //     // 満腹度が50未満の場合に食べ物を探す
    //     if (Hunger < 50)
    //     {
    //         foreach (var item in itemManager.friendHamItems)
    //         {
    //             string itemName = item.Key;
    //             int itemCount = item.Value;

    //             // foodItemsにitemNameが含まれているかチェック
    //             bool isFood = itemManager.foodItems.Exists(foodItem => foodItem.itemName == itemName);

    //             if (isFood && itemCount > 0)
    //             {
    //                 // 食べ物アイテムが見つかった場合、食べさせる
    //                 Debug.Log($"ともハムに{itemName}を食べさせます。");
    //                 // Hungerを20回復させる
    //                 Hunger += 20;
    //                 // 食べ物を1個減らす
    //                 itemManager.RemoveItem(itemName, 1);
    //                 // 食べたことを記録
    //                 AddActivityMemory($"{itemName}が食べられた（{DateTime.Now}）");
    //             }
    //         }
    //     }
    //     onComplete?.Invoke();
    // }
}