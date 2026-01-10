using UnityEngine;
using TMPro;

/**
 * プレイヤーがNPCに近づいたときにダイアログを開始するトリガースクリプト
 * NPCにアタッチして使用
 */
public class DialogueTrigger : MonoBehaviour
{
    [Header("Dialogue Settings")]
    public DialogueSystem dialogueSystem;
    private bool isPlayerInRange = false;

    [Header("UI Prompt")]
    [SerializeField] private GameObject interactionPrompt; // "スペースで話す"の表示用
    [SerializeField] private TextMeshProUGUI promptText;

    void Start()
    {
        // 初期状態でプロンプトを非表示
        ShowInteractionUI(false);
    }

    void Update()
    {
        // プレイヤーが範囲内にいる時のみspaceキーでダイアログ開始
        if (isPlayerInRange && Input.GetKeyDown(KeyCode.Space) && !dialogueSystem.IsDialogueActive)
        {
            if (dialogueSystem != null)
            {
                dialogueSystem.StartDialogue();

                // プロンプトを非表示
                ShowInteractionUI(false);
            }
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInRange = true;

            // インタラクションプロンプトを表示
            ShowInteractionUI(true);
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInRange = false;

            // プロンプトを非表示
            ShowInteractionUI(false);

            // ダイアログが進行中の場合は強制終了
            if (dialogueSystem != null)
            {
                dialogueSystem.ForceEndDialogue();
            }
        }
    }

    // プロンプトの表示・非表示を制御するメソッド
    private void ShowInteractionUI(bool show)
    {
        if (interactionPrompt == null) return;
        
        interactionPrompt.SetActive(show);
        
        if (show && promptText != null)
        {
            promptText.text = "スペースで話す";
        }
    }
}