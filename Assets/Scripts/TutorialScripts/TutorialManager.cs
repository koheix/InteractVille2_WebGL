using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;

public class TutorialManager : MonoBehaviour
{
    [Header("Tutorial Canvas")]
    public GameObject tutorialCanvas;
    // textmeshpro
    public TextMeshProUGUI tutorialText;
    public Button nextButton;

    // チュートリアルのステップ(ともハムの説明は初めて家に入ったときに行う？)
    protected List<string> tutorialSteps = new List<string>()
    {
        "Interact Ville2へようこそ!\n簡単な説明をはじめます。",
        "この森には、あなたが好きな友達のハムスター、「ともハム」が住んでいます。\nともハムはあなたとの会話が大好きです。",
        "この森にはりんごが落ちています。りんごはこの世界のお金で、食べ物としても使えます。\n左上に集めたりんごの数が表示されます。",
        "りんごは、森のショップでアイテムを購入するために使えます。",
        "森のショップには、家具や飲み物が売られています。",
        "Eキーであなたのインベントリを開けます。購入したアイテムをここに保存できます。",
        // プレイヤーステータスの説明を追加
        "「WASD」か、矢印キーで移動できます。",
        "左下にはあなたのスタミナが表示されています。\nずっと動き回るとスタミナが減ってしまいます。\n休憩してスタミナを回復しましょう。",
        "以上です!\nInteract Ville2でのともハムとの生活をお楽しみください!"
    };


    // Start is called once before the first execution of Update after the MonoBehaviour is created
     void Start()
    {
        // チュートリアルが完了していない場合、チュートリアルを開始する
        // bool isTutorialCompleted = SaveDao.LoadData<bool>("Player1", data => data.isTutorialCompleted);
        
        bool isTutorialCompleted = SaveDao.LoadData<bool>(PlayerPrefs.GetString("userName", "default"), data => data.isTutorialCompleted);
        if (!isTutorialCompleted)
        {
            StartTutorial();
        }else
        {
            tutorialCanvas.SetActive(false);
            Debug.Log("Tutorial already completed.");
        }
        
    }
    void StartTutorial()
    {
        tutorialCanvas.SetActive(true);
        StartCoroutine(RunTutorial());
    }
    System.Collections.IEnumerator RunTutorial()
    {
        for (int i = 0; i < tutorialSteps.Count; i++)
        {
            tutorialText.text = tutorialSteps[i];
            bool nextClicked = false;
            nextButton.onClick.RemoveAllListeners();
            nextButton.onClick.AddListener(() => nextClicked = true);

            // 次のボタンがクリックされるまで待つ
            yield return new WaitUntil(() => nextClicked);
        }

        // チュートリアル完了後の処理
        tutorialCanvas.SetActive(false);
        SaveDao.UpdateData(PlayerPrefs.GetString("userName", "default"),  data => data.isTutorialCompleted = true);
        Debug.Log("Tutorial completed.");
    }
    
}
