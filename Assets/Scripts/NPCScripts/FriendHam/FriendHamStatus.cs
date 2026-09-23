/* ともハムのステータスを管理するスクリプト
 * 機嫌や満腹度の管理など
 * ステータスはLLMの応答生成、行動選択に影響を与えるかつLLMの出力で変化する
 * よって外からも読み書きできるようにする
 * 発話メソッドなども担う
 */

using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;
using System.Text.RegularExpressions;
using System.Linq;
using InteractVille2.LLM;


public class FriendHamStatus : MonoBehaviour
{
    // LLMBridgeの参照
    [Header("LLM Bridge Reference")]
    public LLMBridge llmBridge;

    [Header("Friend Ham Status Reference")]
    [SerializeField] private TextMeshProUGUI moodText;
    [SerializeField] private Image closenessGauge;
    [SerializeField] private TextMeshProUGUI closenessGaugeText;
    [SerializeField] private Transform remainingTurnsLayout;
    [SerializeField] private TextMeshProUGUI remainingTurnsNumText;
    [SerializeField] private TextMeshProUGUI maxTurnsNumText;

    
    
    [Header("Remaining Turns Image Prefab Reference")]
    [SerializeField] private GameObject remainingTurnsImagePrefab;

    // 会話回数が回復する時間
    private float recoveryDuration = 3600f; // 一時間

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

    // ともハムと1時間に喋れる回数
    private int MAX_SPEAKS_PER_HOUR = 5;
    // ともハムと会話した履歴
    public List<DateTime> speakHistory = new List<DateTime>();
    public List<long> speakHistoryLong;

    private bool canSpeak = false;
    // checkSpeak でサーバー時刻を確かめられなかった（PlayFab の失敗・タイムアウト）なら true
    private bool speakCheckFailed = false;
    // checkSpeak が今回の会話として記録した時刻。LLM が失敗したら、この記録だけを取り消して回数を返す。
    private DateTime lastSpeakTime;

    // 会話回数の判定に使うサーバー時刻の取得を待つ上限（秒）
    private const float ServerTimeTimeoutSeconds = 15f;

    // ともハムが返事を考えている間は true（通信中に次の発言を受け付けない）
    public bool IsSpeaking { get; private set; }

    // 記憶の要約を頼むために会話履歴へ追加した指示。会話ログを保存するときにこれだけを取り除く。
    private Message summaryInstruction;

    // memory(ともハムの記憶を保存するための文字列リスト)
    // ゲームが終了するときに保存する(SaveDaoを使う)
    // public List<string> memory = new List<string>();
    // memory = SaveDao.LoadData(PlayerPrefs.GetString("userName", "default"), data => data.friendHamMemory);
    public List<string> memory;
    private const int MaxMemorySize = 20; // 最大メモリ数
    // スコアのプロンプトに埋め込む会話ログの上限。前後の説明文の分を空けて、プロキシの上限（30,000 文字）に収める。
    private const int ConversationPromptBudget = LlmLimits.PromptMaxChars - 2000;
    private LLMBridge.ConversationHistory conversationHistory = new LLMBridge.ConversationHistory();
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    // void Start()
    // {
    //     memory = SaveDao.LoadData(PlayerPrefs.GetString("userName", "default"), data => data.friendHamMemory);
    //     valence = SaveDao.LoadData(PlayerPrefs.GetString("userName", "default"), data => data.friendHamValence);
    //     arousal = SaveDao.LoadData(PlayerPrefs.GetString("userName", "default"), data => data.friendHamArousal);
    //     hunger = SaveDao.LoadData(PlayerPrefs.GetString("userName", "default"), data => data.friendHamHunger);
    //     closeness = SaveDao.LoadData(PlayerPrefs.GetString("userName", "default"), data => data.friendHamCloseness);
    //     currentMood = SaveDao.LoadData(PlayerPrefs.GetString("userName", "default"), data => data.friendHamCurrentMood);

    //     // 話せる回数は親密度によって増える
    //     MAX_SPEAKS_PER_HOUR += closeness / 10;
        
    //     speakHistoryLong = SaveDao.LoadData(PlayerPrefs.GetString("userName", "default"), data => data.speakHistoryLong);
    //     foreach (var tick in speakHistoryLong)
    //     {
    //         speakHistory.Add(DateTime.FromBinary(tick));
    //     }
    //     // 一時間以上前の発話データを削除
    //     TimeUtil.GetSafeDateTime(
    //         serverTime =>
    //         {
    //             // 現在時刻を取得
    //             DateTime now = serverTime;
    //             Debug.Log(serverTime);
    //             // 1時間以上前の発話時間データを削除
    //             if(speakHistory != null)
    //             {
    //                 speakHistory.RemoveAll(t => (now - t).TotalMinutes >= 60);
    //             }
    //         },
    //         error =>
    //         {
    //             Debug.LogError("playfab error:" + error.GenerateErrorReport());
    //         }
    //     );




    //     UpdateMoodUI();
    //     UpdateClosenessUI();
    //     UpdateRemainingTurnsUI();
    //     // Debug.Log($"FriendHamStatus: Loaded memory count = {memory.Count}, Valence={Valence}, Arousal={Arousal}, Hunger={Hunger}");
    // }

    public IEnumerator Start() 
    {
        string userName = PlayerPrefs.GetString("userName", "default");

        // 各データのロード
        memory = SaveDao.LoadData(userName, data => data.friendHamMemory);
        valence = SaveDao.LoadData(userName, data => data.friendHamValence);
        arousal = SaveDao.LoadData(userName, data => data.friendHamArousal);
        hunger = SaveDao.LoadData(userName, data => data.friendHamHunger);
        closeness = SaveDao.LoadData(userName, data => data.friendHamCloseness);
        currentMood = SaveDao.LoadData(userName, data => data.friendHamCurrentMood);

        // 親密度に基づく計算
        MAX_SPEAKS_PER_HOUR += closeness / 10;

        speakHistoryLong = SaveDao.LoadData(userName, data => data.speakHistoryLong);
        speakHistory.Clear(); // 二重登録防止
        foreach (var tick in speakHistoryLong)
        {
            speakHistory.Add(DateTime.FromBinary(tick));
        }

        // サーバー時刻の取得を「待機」する
        bool isTimeProcessingDone = false;

        TimeUtil.GetSafeDateTime(
            serverTime =>
            {
                DateTime now = serverTime;
                if (speakHistory != null)
                {
                    speakHistory.RemoveAll(t => (now - t).TotalMinutes >= 60);
                }
                isTimeProcessingDone = true; 
            },
            error =>
            {
                Debug.LogError("playfab error:" + error.GenerateErrorReport());
                isTimeProcessingDone = true; // エラー時も進行を止めない
            }
        );

        // 完了フラグが true になるまでループで待機（ここがポイント）
        while (!isTimeProcessingDone)
        {
            yield return null; 
        }

        // 全てのデータ処理が終わってからUIを更新
        UpdateMoodUI();
        UpdateClosenessUI();
        UpdateRemainingTurnsUI();
        
        Debug.Log("すべての初期化とUI更新が完了しました");
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
        // 非アクティブ化で止まったコルーチンは後処理（CoroutineFlow.Guard の finally）が走らないので、ここで戻す。
        // 戻さないと、再びアクティブになったときに以後の送信がすべて弾かれる。
        IsSpeaking = false;

        // 会話をしていれば会話履歴を更新する
        if (conversationHistory.messages.Count > 1){
            // 会話履歴を保存しておく
            Debug.Log("FriendHamStatus: 会話履歴の保存");
            List<Message> conversationLog = new List<Message>();
            conversationLog = SaveDao.LoadData(PlayerPrefs.GetString("userName", "default"), data => data.conversationHistory);
            // 要約の指示は会話ではないので保存しない。以前は「最後の 1 件」を消していたが、
            // 要約をしなかった場合に本物の発言を消してしまうので、指示そのものを取り除く。
            ConversationRules.RemoveExact(conversationHistory.messages, summaryInstruction);
            summaryInstruction = null;
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

    // 残りの発話回数のUIを更新するメソッド
    private void UpdateRemainingTurnsUI()
    {
        // 残り回数と最大回数をセット
        maxTurnsNumText.text = MAX_SPEAKS_PER_HOUR.ToString();
        remainingTurnsNumText.text = (MAX_SPEAKS_PER_HOUR - speakHistory.Count).ToString();

        // layoutの子要素を一つずつループで処理
        foreach (Transform child in remainingTurnsLayout)
        {
            // オブジェクトを削除
            Destroy(child.gameObject);
        }


        Image lastImage;
        int generateNum = MAX_SPEAKS_PER_HOUR - speakHistory.Count;

        // リストの数だけスロットを生成する
        for(int i = 0;i < generateNum;i++)
        {
            Instantiate(remainingTurnsImagePrefab, remainingTurnsLayout);
            // lastImage = obj.GetComponent<Image>(); 
        }
        GameObject obj = Instantiate(remainingTurnsImagePrefab, remainingTurnsLayout);
        lastImage = obj.GetComponent<Image>(); 

        // 現在時刻を取得
        TimeUtil.GetSafeDateTime(
            serverTime =>
            {
                // 現在時刻を取得
                DateTime now = serverTime;
                Debug.Log(serverTime);
                // TimeSpan remaining = now - speakHistory[0];
                // Debug.Log("remaining:" + remaining);
                // float remainingSeconds = (float)remaining.TotalSeconds;

                // 1時間以内の会話履歴があれば最後のspeak turns UIの割合を計算して反映する
                if(speakHistory.Count > 0)
                {
                    float remainingSeconds = (float)(now - speakHistory[0]).TotalSeconds;

                    if (remainingSeconds > 0)
                    {
                        // 割合を計算 (1.0 - 残り時間/トータル時間)
                        // 1時間で満タンになる計算
                        float progress = remainingSeconds / 3600f; // 一時間をfullとして計算
                        Debug.Log("progress:" + progress);
                        if(progress >= 0.0f)
                        {
                            lastImage.fillAmount = Mathf.Clamp01(progress);                        
                        }
                        else
                        {
                            lastImage.fillAmount = Mathf.Clamp01(1.0f);
                        }
                    }
                }
                
            },
            error =>
            {
                Debug.LogError("playfab error:" + error.GenerateErrorReport());
            }
        );



    }

    public IEnumerator Speak(string message, System.Action<string> onUpdate, System.Action<string> onComplete = null)
    {
        if (IsSpeaking)
        {
            // 通信中の二重送信は受け付けない（呼び出し側でも止めているが念のため）
            yield break;
        }
        IsSpeaking = true;
        // 途中で例外が起きても IsSpeaking を戻す（戻さないと以後の送信がすべて弾かれ、会話 UI が固まる）。
        // Unity に入れ子のコルーチンを渡すと、その中の例外は外側の finally を通らない。CoroutineFlow.Guard は
        // 入れ子も自分で進めるので、SpeakCore の中では StartCoroutine を使わず、入れ子は yield return で渡す。
        yield return CoroutineFlow.Guard(SpeakCore(message, onUpdate, onComplete), () => IsSpeaking = false);
    }

    private IEnumerator SpeakCore(string message, System.Action<string> onUpdate, System.Action<string> onComplete)
    {
        string finalResponse = "";
        // ともハムと喋れるかを判定(1時間にMAX_SPEAKS_PER_HOUR回しか発話できない)
        yield return checkSpeak();

        // 会話残り回数を反映する
        UpdateRemainingTurnsUI();

        // 会話時間リスト保存
        SaveSpeakHistory();
        // 話せないならbreakする
        if (!canSpeak)
        {
            Debug.Log("yield break");
            // 時刻を確かめられなかったときは、上限に達したのとは別の理由を伝える
            finalResponse = speakCheckFailed
                ? "いまちょっと時計が読めないみたい…少し待ってから、もう一度話しかけてね！"
                : "ぼくとは1時間に"+ MAX_SPEAKS_PER_HOUR + "回しか話せないみたい...!";
            onUpdate?.Invoke(finalResponse);
            onComplete?.Invoke(finalResponse);
            yield break;
        }
        DateTime speakTime = lastSpeakTime;
        Debug.Log("[Friend Ham]LLM プロキシに会話をリクエストします...");


        // ユーザーメッセージを履歴に追加（失敗したらこのメッセージだけを取り除く）
        Message userMessage = conversationHistory.AddUserMessage(message);

        // アクティビティの記録が無いセーブデータでも落ちないようにする
        List<string> activityMemory = SaveDao.LoadData(PlayerPrefs.GetString("userName", "default"), data => data.friendHamActivityMemory) ?? new List<string>();

        LlmResult result = null;
        IEnumerator responseCoroutine = llmBridge.Chat(
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
            string.Join("\n", activityMemory.TakeLast(3).ToList()) +
            "これらの情報を元に、以下のユーザーメッセージに返答してください。",  // システムメッセージ
            conversationHistory.messages,  // 履歴全体を送信（上限を超える分は古いものから LLMBridge が切り詰める）
            r => result = r
        );

        yield return responseCoroutine;

        // 成功なら返事を履歴に追加する。失敗なら今回の発言を履歴から取り除いて会話回数を返し、
        // エラーの文言を表示する（文言は履歴に入れない）。規則は Core の ChatTurnRules にある。
        ChatTurnOutcome outcome = ChatTurnRules.CompleteTurn(conversationHistory.messages, userMessage, speakHistory, speakTime, result);
        finalResponse = outcome.DisplayText;
        if (outcome.Refunded)
        {
            SaveSpeakHistory();
            UpdateRemainingTurnsUI();
        }

        if (outcome.Succeeded)
        {
            yield return llmBridge.PseudoStreaming(outcome.DisplayText, partialText => onUpdate?.Invoke(partialText));
        }
        else
        {
            onUpdate?.Invoke(outcome.DisplayText);
        }

        DebugPrintConversation();

        onComplete?.Invoke(finalResponse);
    }

    // 会話した時刻の記録を保存する
    private void SaveSpeakHistory()
    {
        speakHistoryLong.Clear();
        foreach (var dt in speakHistory)
        {
            speakHistoryLong.Add(dt.ToBinary());
        }
        SaveDao.UpdateData(PlayerPrefs.GetString("userName", "default"), data => data.speakHistoryLong = speakHistoryLong);
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

    private IEnumerator checkSpeak()
    {

        // 発話時間のリストを取得
        // 記録の無い古いセーブデータでも落ちないようにする
        speakHistoryLong = SaveDao.LoadData(PlayerPrefs.GetString("userName", "default"), data => data.speakHistoryLong) ?? new List<long>();
        speakHistory.Clear();
        foreach (var tick in speakHistoryLong)
        {
            speakHistory.Add(DateTime.FromBinary(tick));
        }
        if(speakHistory == null) speakHistory = new List<DateTime>();

        canSpeak = false;
        speakCheckFailed = false;
        bool isWaiting = true;
        // 時刻の取得が返ってこないと会話がずっと始まらないので、一定時間で諦めて「話せない」扱いにする。
        // 諦めた後に届いた応答は無視する（記録だけが増えて会話回数が減るのを防ぐ）。
        bool abandoned = false;

        TimeUtil.GetSafeDateTime(
            serverTime =>
            {
                if (abandoned) return;
                // 現在時刻を取得
                DateTime now = serverTime;
                Debug.Log(serverTime);
                // 1時間以上前の発話時間データを削除
                if(speakHistory != null)
                {
                    speakHistory.RemoveAll(t => (now - t).TotalMinutes >= 60);
                }

                // 話せる
                if(speakHistory.Count < MAX_SPEAKS_PER_HOUR)
                {
                    // 今回の会話の時間を追加
                    speakHistory.Add(now);
                    lastSpeakTime = now;
                    canSpeak = true;
                    Debug.Log("話せる");
                }
                else
                {
                    Debug.Log("会話回数上限を超えています。");
                    canSpeak = false;
                }
                isWaiting = false;
            },
            error =>
            {
                if (abandoned) return;
                Debug.LogError("playfab error:" + error.GenerateErrorReport());
                canSpeak = false;
                speakCheckFailed = true;
                isWaiting = false;
            }
        );

        float deadline = Time.realtimeSinceStartup + ServerTimeTimeoutSeconds;
        while (isWaiting)
        {
            if (Time.realtimeSinceStartup > deadline)
            {
                Debug.LogError("サーバー時刻の取得がタイムアウトしました。");
                abandoned = true;
                canSpeak = false;
                speakCheckFailed = true;
                break;
            }
            yield return null;
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
        // ClosenessをLLMに計算させる（失敗したら会話前の値のまま）
        LlmResult closenessResult = null;
        yield return StartCoroutine(
        llmBridge.Score(
            "以下の会話データから、ハムスターの親密度(Closeness)を算出してください。(0~100の範囲で数値を返してください）\n" +
            "最後に会話をしてから時間が経過していたら、経過している時間が長いほど親密度を下げてください。" +
            "会話データ:" +
            ConversationRules.FormatForPrompt(conversationHistory.messages, summaryInstruction, ConversationPromptBudget) +
            "\n最後に会話をした時間:" + ExtractDateTime(memory) +
            "\n現在の時間:" + TimeUtil.GetCurrentTimeString() +
            "\n会話前のCloseness:" + Closeness.ToString(),
            r => closenessResult = r
        ));
        Closeness = StatusRules.ApplyScore(Closeness, closenessResult);
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

        // 要約支持を履歴に追加（会話ログを保存するときに、この指示だけを取り除く）
        string message = "これまでの会話履歴から、あなたとの重要な思い出や情報を3つ程度要約してメモリとして保存してください。" +
                         "それぞれは短い文章で表現してください。" +
                         "また、箇条書き形式で、その内容だけを出力してください。";
        summaryInstruction = conversationHistory.AddUserMessage(message);

        LlmResult summaryResult = null;
        IEnumerator responseCoroutine = llmBridge.Chat(
            "最近の会話履歴から重要な情報を整理し、要約してください。",  // システムメッセージ
            conversationHistory.messages,  // 履歴全体を送信
            r => summaryResult = r
        );
        yield return StartCoroutine(responseCoroutine);
        Debug.Log($"[Friend Ham]生成されたメモリ: {summaryResult}");
        // memoryに保存（失敗・空の要約は追加しない。MaxMemorySize 個を超えたら古いものから削除）
        if (StatusRules.TryAppendMemory(memory, summaryResult, TimeUtil.GetCurrentTimeString(), MaxMemorySize))
        {
            // ここで履歴を保存する
            SaveDao.UpdateData(PlayerPrefs.GetString("userName", "default"), data => data.friendHamMemory = memory);
        }

        // 会話時間のリストも保存
        // SaveDao.UpdateData(PlayerPrefs.GetString("userName", "default"), data => data.speakHistory = speakHistory);
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
        // ValenceをLLMに計算させる（失敗したら会話前の値のまま）
        LlmResult valenceResult = null;
        yield return StartCoroutine(
        llmBridge.Score(
            "以下の会話データから、ハムスターの感情価(Valence)を算出してください。(0~100の範囲で数値を返してください）\n" +
            "会話データ:" +
            ConversationRules.FormatForPrompt(conversationHistory.messages, summaryInstruction, ConversationPromptBudget) +
            "\n会話前のValence:" + Valence.ToString(),
            r => valenceResult = r
        ));
        Valence = StatusRules.ApplyScore(Valence, valenceResult);
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
        // ArousalをLLMに計算させる（失敗したら会話前の値のまま）
        LlmResult arousalResult = null;
        yield return StartCoroutine(
        llmBridge.Score(
            "以下の会話データから、ハムスターの覚醒度(Arousal)を算出してください。(0~100の範囲で数値を返してください）\n" +
            "会話データ:" +
            ConversationRules.FormatForPrompt(conversationHistory.messages, summaryInstruction, ConversationPromptBudget) +
            "\n会話前のArousal:" + Arousal.ToString(),
            r => arousalResult = r
        ));
        Arousal = StatusRules.ApplyScore(Arousal, arousalResult);
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
    //         ConversationRules.FormatForPrompt(conversationHistory.messages, summaryInstruction, ConversationPromptBudget) +
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
        LlmResult moodResult = null;
        yield return StartCoroutine(
            llmBridge.Chat(
                "以下のValenceとArousalデータから、ハムスターの現在の感情を一言で表現してください。(例: '喜び', '悲しみ', '怒り'など）\n" +
                "会話後のValence:" + Valence.ToString() +
                "\n会話後のArousal:" + Arousal.ToString(),  // システムメッセージ
                new Message[] {
                    new Message {role = "user", content = "必ず4文字以内で、感情のみを回答してください。"}
                },
                r => moodResult = r
            )
        );
        // 失敗・空の応答なら今の気分のまま
        CurrentMood = StatusRules.ApplyMood(CurrentMood, moodResult);
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