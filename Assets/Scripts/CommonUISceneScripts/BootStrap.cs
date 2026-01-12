using UnityEngine;
using UnityEngine.SceneManagement;

public class Bootstrap : MonoBehaviour
{

    private void Start()
    {

// WebGLビルド時のみ、キーボード入力をブラウザに逃がす設定
#if !UNITY_EDITOR && UNITY_WEBGL
UnityEngine.WebGLInput.captureAllKeyboardInput = false;
#endif

        // CommonUI を読み込む（永続UI）
        SceneManager.LoadScene("CommonUIScene", LoadSceneMode.Additive);

        // タイトルシーンに移動
        SceneManager.LoadScene("TitleScene", LoadSceneMode.Single);
    }
}
