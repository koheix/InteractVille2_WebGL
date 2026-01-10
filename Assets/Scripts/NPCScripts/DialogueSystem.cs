/*
    DialogueSystem.cs
    会話のUIとロジックを管理する基本的なダイアログシステム
    他のNPC固有のダイアログシステムはこのクラスを継承して実装
*/

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ダイアログの1行分のデータ構造
[System.Serializable]
public class DialogueLine
{
    public string characterName;
    [TextArea(3, 5)]
    public string text;
}

public class DialogueSystem : MonoBehaviour
{
    [Header("UI References")]
    public GameObject dialogueBox;
    public TextMeshProUGUI characterNameText;
    public TextMeshProUGUI dialogueText;
    public Button nextButton;

    [Header("Dialogue Data")]
    [SerializeField]
    protected DialogueLine[] dialogueLines;
    
    protected int currentLineIndex = 0;
    protected bool isDialogueActive = false;
    // getter for isDialogueActive
    public bool IsDialogueActive { get { return isDialogueActive; } }
    protected bool isTyping = false;
    protected Coroutine typingCoroutine;
    
    [Header("Typing Animation")]
    public float typeSpeed = 0.05f;
    
    public virtual void Start()
    {
        // 初期状態でダイアログボックスを非表示
        dialogueBox.SetActive(false);
        
        // Next buttonにクリックイベントを追加
        if (nextButton != null)
        {
            Debug.Log("Adding NextLine listener to nextButton");
            nextButton.onClick.AddListener(NextLine);
            Debug.Log("Listener added successfully");
        }
    }
    
    public virtual void StartDialogue()
    {
        if (dialogueLines.Length == 0) return;
        
        isDialogueActive = true;
        currentLineIndex = 0;
        dialogueBox.SetActive(true);
        // 移動入力を無効化
        InputController.Instance.DisableMovement();
        
        DisplayLine();
    }
    
    protected void DisplayLine()
    {
        if (currentLineIndex < dialogueLines.Length)
        {
            DialogueLine currentLine = dialogueLines[currentLineIndex];
            
            // キャラクター名を設定
            if (characterNameText != null)
            {
                characterNameText.text = currentLine.characterName;
            }
            
            // タイピングエフェクトでテキストを表示
            if (typingCoroutine != null)
            {
                StopCoroutine(typingCoroutine);
            }
            typingCoroutine = StartCoroutine(TypeText(currentLine.text));
        }
        else
        {
            EndDialogue();
        }
    }

    IEnumerator TypeText(string text)
    {
        isTyping = true;
        dialogueText.text = "";

        foreach (char letter in text.ToCharArray())
        {
            dialogueText.text += letter;
            yield return new WaitForSeconds(typeSpeed);
        }

        isTyping = false;
    }

    // ボタンが押されたとき次の行へ進むメソッド
    void NextLine()
    {
        Debug.Log("NextLine called");
        // タイピング中の場合は即座に全文表示
        if (isTyping)
        {
            if (typingCoroutine != null)
            {
                StopCoroutine(typingCoroutine);
            }
            dialogueText.text = dialogueLines[currentLineIndex].text;
            isTyping = false;
            return;
        }

        currentLineIndex++;
        DisplayLine();
    }
    
    // 会話を終了し、UIを非表示にするメソッド
    protected virtual void EndDialogue()
    {
        isDialogueActive = false;
        dialogueBox.SetActive(false);
        currentLineIndex = 0;
        // 移動入力を有効化
        InputController.Instance.EnableMovement();
    }

    // 外部からダイアログを終了させるメソッド
    public void ForceEndDialogue()
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
        }
        EndDialogue();
    }
}