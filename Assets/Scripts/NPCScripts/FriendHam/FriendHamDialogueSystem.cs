using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class FriendHamDialogueSystem : DialogueSystem
{
    [Header("UI References")]
    public GameObject chattingBox;
    // public GameObject presentBox;
    public GameObject pettingBox;
    // ともハムとのインタラクション全体を終了するためのボタン
    public Button quitButton;
    public Button presentButton;
    public Button chatButton;
    public Button petButton;
    // メニューに戻るためのボタン
    public Button returnButton;


    [Header("Chatting Box UI References")]
    public Button sendButton;
    public TMP_InputField chatInputField;
    public TextMeshProUGUI chattingCharacterNameText;
    public TextMeshProUGUI chattingText;

    [Header("Present Box UI References")]
    public GameObject presentBox;
    public GameObject itemListPanel;
    public TextMeshProUGUI presentCharacterNameText;
    public TextMeshProUGUI presentDialogueText;
    public Button presentBoxYesButton;
    public Button presentBoxNoButton;
    public Button presentBoxReturnButton;

    [Header("FriendHam Status Reference")]
    public FriendHamStatus friendHamStatus;

    [Header("FriendHam Item Manager Reference")]
    public FriendHamItemManager friendHamItemManager;


    private FriendHamFSM friendHamFSM = new FriendHamFSM();


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public override void Start()
    {
        base.Start();
        if (chattingBox != null)
        {
            chattingBox.SetActive(false);
        }
        // if (pettingBox != null)
        // {
        //     pettingBox.SetActive(false);
        // }
        if (presentBox != null)
        {
            presentBox.SetActive(false); 
        }
        // quitButtonにクリックイベントを追加, 非表示にしておく
        if (quitButton != null)
        {
            quitButton.gameObject.SetActive(false);
            quitButton.onClick.AddListener(EndDialogue);
        }
        if (presentButton != null)
        {
            presentButton.onClick.AddListener(OpenPresentBox);
        }
        if (chatButton != null)
        {
            chatButton.onClick.AddListener(OpenChattingBox);
        }
        // chattingBoxのボタンイベントを設定
        // 送信ボタン
        if (sendButton != null)
        {
            sendButton.onClick.AddListener(OnSendButtonClicked);
        }
        // メニューに戻るボタン
        if (returnButton != null)
        {
            returnButton.onClick.AddListener(() =>
            {
                chattingBox.SetActive(false);
                presentBox.SetActive(false);
                dialogueBox.SetActive(true);
            });
        }

        // presentBoxのはいかいいえボタンにクリックイベントを追加
        if (presentBoxYesButton != null)
        {
            presentBoxYesButton.onClick.AddListener(() => OnClickPresentBoxYorNButton(presentBoxYesButton));
        }
        if (presentBoxNoButton != null)
        {
            presentBoxNoButton.onClick.AddListener(() => OnClickPresentBoxYorNButton(presentBoxNoButton));
        }

        // presentBoxの戻るボタンにクリックイベントを追加
        if (presentBoxReturnButton != null)
        {
            presentBoxReturnButton.onClick.AddListener(() =>
            {
                chattingBox.SetActive(false);
                presentBox.SetActive(false);
                dialogueBox.SetActive(true);
            });
        }


    }

    // Update is called once per frame
    void Update()
    {

    }

    public override void StartDialogue()
    {
        // friendHamFSM.changeState(true); // 初期状態へ遷移
        dialogueLines = friendHamFSM.EnterState(FriendHamState.Greeting);
        quitButton.gameObject.SetActive(true); // やめるボタンを表示
        base.StartDialogue();
    }


    // インタラクション全体を終了する
    protected override void EndDialogue()
    {
        base.EndDialogue();
        // pettingBox.SetActive(false);
        presentBox.SetActive(false);
        chattingBox.SetActive(false);
        quitButton.gameObject.SetActive(false); // やめるボタンを非表示
    }

    // 雑談ボックスを開く
    void OpenChattingBox()
    {
        chattingBox.SetActive(true);
        dialogueBox.SetActive(false);
        // pettingBox.SetActive(false);
        presentBox.SetActive(false);
    }

    // プレゼントボックスを開く
    void OpenPresentBox()
    {
        dialogueLines = friendHamFSM.EnterState(FriendHamState.Present);
        presentBox.SetActive(true);
        dialogueBox.SetActive(false);
        chattingBox.SetActive(false);
        // pettingBox.SetActive(false);
        dialogueLines = friendHamFSM.EnterState(FriendHamState.Present);
        presentDialogueText.text = dialogueLines[0].text;
        presentCharacterNameText.text = dialogueLines[0].characterName;
        // currentLineIndex = 0;
        // DisplayLine();
    }

    // chat送信ボタンのイベント
    void OnSendButtonClicked()
    {
        string playerMessage = chatInputField.text;
        // メッセージが空でない場合のみ処理
        if (!string.IsNullOrEmpty(playerMessage))
        {
            // // ともハムの応答を生成（いったん固定応答を使用）
            // string FriendHamResponse = "ともハム：それは面白いね！";
            // StartCoroutine(friendHamStatus.Speak(playerMessage));
            // StartCoroutine(friendHamStatus.Speak(playerMessage,
            //     res =>
            //     {
            //         Debug.Log(res);
            //         chattingText.text = res;
            //     }
            // ));
            
            StartCoroutine(friendHamStatus.Speak(playerMessage, 
                res => {
                    // ストリーミング中の更新
                    chattingText.text = res;
                },
                finalRes => {
                    // 完了時の処理
                    Debug.Log("Complete: " + finalRes);
                }
            ));


            // ともハムの応答を表示
            chattingCharacterNameText.text = "ともハム";
            // chattingText.text = res;

            // 入力フィールドをクリア
            chatInputField.text = "";
        }
    }

    // presentboxのはいかいいえボタンがクリックされたときに呼び出される
    void OnClickPresentBoxYorNButton(Button clickedButton)
    {
        Debug.Log("Clicked presentBox Button: " + clickedButton.name);
        bool isYes = (clickedButton == presentBoxYesButton);
        if (isYes)
        {
            Debug.Log("Player chose to present the item.");
            // ここでpresentManagerのpresentItemメソッドを呼び出すなどの処理を追加
            // presentManager.PresentItem();
            friendHamItemManager.PresentItem();
        }
        else
        {
            Debug.Log("Player chose not to present the item.");
            // プレゼントしない場合、とりあえずもとに戻る
        }
        // present状態が終了したら基本UIBOXを表示してpresentUIを非表示にする
        dialogueBox.SetActive(true);
        Debug.Log("OK");
        presentBox.SetActive(false);
        dialogueLines = friendHamFSM.EnterState(FriendHamState.Greeting);
        currentLineIndex = 0;
        DisplayLine();
    }

}
