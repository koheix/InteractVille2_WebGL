using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;

public class FriendHamHouseTutorialManager : MonoBehaviour
{
    [Header("Tutorial Canvas")]
    public GameObject tutorialCanvas;
    // textmeshpro
    public TextMeshProUGUI tutorialText;
    public Button nextButton;

    // チュートリアルのステップ(ともハムの説明は初めて家に入ったときに行う？)
    protected List<string> tutorialSteps = new List<string>()
    {
        "ともハムのいえへようこそ!\n簡単な説明をはじめます。",
        "この家には、あなたのことが好きな\n友達のハムスター、「ともハム」が住んでいます。\nともハムはあなたとの会話が大好きです。",
        "ともハムとは自由におしゃべりができます。",
        "ともハムにはショップで購入したアイテムをプレゼントできます。",
        "ともハムはあなたがあげた家具を家に置いてくれるかもしれません。",
        "左上に、ともハムのステータスがあります。",
        "機嫌や親密度は、ともハムとの会話で変わります。\n会話をあまりしていないと親密度が下がることもあります。",  
        "以上です!\nともハムのいえでともハムとの交流をお楽しみください!"
    };


    // Start is called once before the first execution of Update after the MonoBehaviour is created
     void Start()
    {
        // チュートリアルが完了していない場合、チュートリアルを開始する
        // bool isTutorialCompleted = SaveDao.LoadData<bool>("Player1", data => data.isTutorialCompleted);
        
        bool isTutorialCompleted = SaveDao.LoadData<bool>(PlayerPrefs.GetString("userName", "default"), data => data.isFriendHamHouseTutorialCompleted);
        if (!isTutorialCompleted)
        {
            StartTutorial();
            InputController.Instance.DisableMovement();
        }else
        {
            tutorialCanvas.SetActive(false);
            Debug.Log("Friend Ham House Tutorial already completed.");
            InputController.Instance.EnableMovement();
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
        SaveDao.UpdateData(PlayerPrefs.GetString("userName", "default"),  data => data.isFriendHamHouseTutorialCompleted = true);
        Debug.Log("Friend Ham House Tutorial completed.");
        // 動けるようにする
        InputController.Instance.EnableMovement();
    }
    
}
