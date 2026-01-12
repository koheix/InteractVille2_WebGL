using UnityEngine;
using UnityEngine.SceneManagement;

public class Bootstrap : MonoBehaviour
{

    private void Start()
    {
        // CommonUI を読み込む（永続UI）
        SceneManager.LoadScene("CommonUIScene", LoadSceneMode.Additive);

        // タイトルシーンに移動
        SceneManager.LoadScene("TitleScene", LoadSceneMode.Single);
    }
}
