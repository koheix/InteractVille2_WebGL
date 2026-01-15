using Unity.Collections;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using TMPro;
using UnityEngine.UI;

public class DoorTrigger : MonoBehaviour
{
    // private string prevSceneName = SceneManager.GetActiveScene().name;
    private string prevSceneName;
    [SerializeField] private string nextSceneName;
    //if input key with change scene
    // [SerializeField] private bool requireInput = true;
    private bool requireInput = true;

    //whether player in range
    private bool playerInRange = false;

    // Interaction UI
    [SerializeField] private GameObject interactionPrompt;
    [SerializeField] private TextMeshProUGUI promptText;
    // 押すと移動するボタン
    [SerializeField] private Button interactionButton;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Start()
    {
        GetComponent<Collider2D>().isTrigger = true;
        prevSceneName = SceneManager.GetActiveScene().name;

        // // ボタンにイベントリスナーを追加
        // if (interactionButton != null)
        //     interactionButton.onClick.AddListener(OnInteractionButton);

        // ヒントを非表示
        interactionPrompt.SetActive(false);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // ボタンにイベントリスナーを追加
        if (interactionButton != null)
            interactionButton.onClick.AddListener(OnInteractionButton);
        if (other.CompareTag("Player"))
        {
            if (requireInput)
            {
                playerInRange = true;
                // display UI message e.g. : press E to enter
                ShowInteractionUI(true);
            }
            else
            {
                // ChangeScene();
                StartCoroutine(ChangeScene());
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        // 一度全部のイベントリスナーを削除
        interactionButton.onClick.RemoveAllListeners();
        if (other.CompareTag("Player"))
        {
            playerInRange = false;
            ShowInteractionUI(false);
        }
    }

    // Update is called once per frame
    void Update()
    {
        // if need input key to enter
        if (requireInput && playerInRange && Input.GetKeyDown(KeyCode.Space))
        {
            StartCoroutine(ChangeScene());
        }
    }

    private IEnumerator ChangeScene()
    {
        //のちに非同期処理
        //UnityEngine.SceneManagement.SceneManager.LoadScene(nextSceneName);

        //データセーブ等
        yield return StartCoroutine(QuitManager.Instance.RequestChangeSceneCoroutine(nextSceneName));

        // SceneManager.LoadScene(nextSceneName);
    }

    private void ShowInteractionUI(bool show)
    {
        if (interactionPrompt == null) return;
        
        interactionPrompt.SetActive(show);
        
        if (show && promptText != null)
        {
            promptText.text = "スペースで移動";
        }
    }

    // ボタンは当たり判定があるときにしか表示されない
    private void OnInteractionButton()
    {
        StartCoroutine(ChangeScene());
    }
}
