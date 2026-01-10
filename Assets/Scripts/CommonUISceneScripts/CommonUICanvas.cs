/* CommonUICanvas.cs
 * 
 * シーンをまたいでタイトルへ戻るボタンを保持するためのクラス
 */

using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CommonUICanvas : MonoBehaviour
{
    private Canvas rootCanvas;
    private Button return2TitleButton;

    [Header("最初のロード画面のCanvas")]
    [SerializeField] private Canvas firstLoadCanvas;

    // singleton
    public static CommonUICanvas Instance { get; private set; }

    private void Awake()
    {
        rootCanvas = GetComponent<Canvas>();
        firstLoadCanvas.enabled = true;
        // rootCanvasを非表示にしておく
        // rootCanvas.enabled = false;
        // Singleton
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        return2TitleButton =  GetComponentInChildren<Button>();
        return2TitleButton.onClick.AddListener(() =>
        {
            QuitManager.Instance.RequestReturn2Title();
        });
        // シーン変化を監視
        SceneManager.activeSceneChanged += OnSceneChanged;
    }

    private void OnSceneChanged(Scene pre, Scene next)
    {
        // 最初のロード画面を非表示にする
        // if (firstLoadCanvas != null && firstLoadCanvas.enabled)
        if (firstLoadCanvas != null && pre.name == "CommonUIScene")
        {
            firstLoadCanvas.enabled = false;
        }
        // タイトルシーン名に応じて切り替え
        if (next.name == "TitleScene") 
        {
            rootCanvas.enabled = false;  // UI 非表示
        }
        else
        {
            rootCanvas.enabled = true;   // UI 表示
        }
    }
}