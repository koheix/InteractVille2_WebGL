using UnityEngine;
using TMPro;
using System.Collections;

public class NowLoading : MonoBehaviour
{
    [Header("nowloadingのラベル")]
    [SerializeField] private TextMeshProUGUI nowLoadingText;

    private int cnt = 0;
    private Coroutine loadingCoroutine; // 実行中のコルーチンを保持する変数

    // 有効化されるたびに呼ばれる
    void OnEnable()
    {
        Debug.Log("NowLoading: OnEnable");
        // すでに動いている場合は一旦止める（二重起動防止）
        if (loadingCoroutine != null)
        {
            StopCoroutine(loadingCoroutine);
        }
        // コルーチンを開始
        loadingCoroutine = StartCoroutine(NowLoadingLoop());
    }

    // 無効化されたときに呼ばれる
    void OnDisable()
    {
        Debug.Log("NowLoading: OnDisable");
        // オブジェクトが非表示になったら停止させる
        if (loadingCoroutine != null)
        {
            StopCoroutine(loadingCoroutine);
            loadingCoroutine = null;
        }
    }

    private IEnumerator NowLoadingLoop()
    {
        // ループ開始時にリセット
        cnt = 0;
        nowLoadingText.text = "Now Loading ";

        while (true)
        {
            yield return new WaitForSeconds(0.3f);
            
            if (nowLoadingText != null)
            {
                cnt++;
                if (cnt > 3)
                {
                    cnt = 0;
                    nowLoadingText.text = "Now Loading ";
                }
                nowLoadingText.text += ".";
            }
        }
    }
}