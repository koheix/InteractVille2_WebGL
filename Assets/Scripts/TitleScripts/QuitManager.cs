/* QuitManager.cs
 * ゲーム終了時に実行する処理を管理するマネージャークラス
 * セーブ処理やリソース解放など、終了前に行いたい処理を登録する
 * 友ハムのメモリーを保存したりする
 */

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class QuitManager : MonoBehaviour
{
    public static QuitManager Instance { get; private set; }

    private bool quitting = false;
    private bool return2title = false;
    private bool changeScene = false;

    // 終了時に実行する処理をリストで管理
    private List<IEnumerator> quitTasks = new List<IEnumerator>();
    private List<IEnumerator> return2TitleTasks = new List<IEnumerator>();
    // loading panel
    [SerializeField] private GameObject loadPanel;

    private void Awake()
    {
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
        // シーン変化を監視
        SceneManager.activeSceneChanged += OnSceneChanged;
    }

    private void OnSceneChanged(Scene pre, Scene next)
    {
        // loading panelを非表示にする
        loadPanel.SetActive(false);
    }

    // 終了処理を追加する
    public void AddQuitTask(IEnumerator task)
    {
        quitTasks.Add(task);
    }

    // 終了ボタンから呼ぶメソッド
    public void RequestQuit()
    {
        // loading panelを表示する
        loadPanel.SetActive(true);

        if (quitting) return;

        quitting = true;
        StartCoroutine(QuitFlow());
    }

    // 終了処理
    private IEnumerator QuitFlow()
    {
        Debug.Log("終了処理開始…");

        // 登録されてるタスクを順番に全部実行して待つ
        foreach (var task in quitTasks)
            yield return StartCoroutine(task);

        // 最低2秒待つ
        yield return new WaitForSeconds(2f);

        Debug.Log("終了処理完了");
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
    }

    // タイトルへ戻る処理
    // private IEnumerator Return2TitleFlow()
    // {
    //     Debug.Log("タイトルへ戻る処理開始…");

    //     // 登録されてるタスクを順番に全部実行して待つ
    //     foreach (var task in return2TitleTasks)
    //         yield return StartCoroutine(task);

    //     return2TitleTasks.Clear();
    //     Debug.Log("タイトルへ戻る処理完了");
    //     SceneManager.LoadScene("TitleScene", LoadSceneMode.Single);
    // }

    // タイトルへ戻る際の処理を追加する
    public void AddReturn2TitleTask(IEnumerator task)
    {
        return2TitleTasks.Add(task);
    }

    // タイトルへ戻るボタンから呼ぶメソッド
    public void RequestReturn2Title()
    {
        // loading panelを表示する
        loadPanel.SetActive(true);

        if (return2title) return;

        return2title = true;
        Debug.Log("QuitManager: タイトルへ戻る要求を受け取りました");
        StartCoroutine(Return2TitleFlow());
    }

    // 同時実行版タイトルへ戻る処理
    private IEnumerator Return2TitleFlow()
    {
        Debug.Log("タイトルへ戻る処理開始…");
        // ↓キャラクターのところでやるべきか
        // // 最後のシーン名と座標を記録
        // SaveDao.UpdateData(PlayerPrefs.GetString("userName", default), PlayerData => PlayerData.lastSceneName = SceneManager.GetActiveScene().name);

        // すべてのタスクを同時に開始
        List<Coroutine> runningCoroutines = new List<Coroutine>();
        foreach (var task in return2TitleTasks)
        {
            runningCoroutines.Add(StartCoroutine(task));
        }

        // すべてのタスクの完了を待つ
        foreach (var coroutine in runningCoroutines)
        {
            yield return coroutine;
        }
        // 最低2秒待つ
        yield return new WaitForSeconds(2f);

        return2TitleTasks.Clear();
        return2title = false;
        Debug.Log("タイトルへ戻る処理完了");
        SceneManager.LoadScene("TitleScene", LoadSceneMode.Single);
    }

    // コルーチンとして呼べる(ドアトリガーから呼ばれる)
    public IEnumerator RequestChangeSceneCoroutine(string nextSceneName)
    {
        loadPanel.SetActive(true);
        
        if (changeScene) yield break;
        
        changeScene = true;
        Debug.Log("QuitManager: シーンチェンジの要求を受け取りました");
        
        // Return2TitleFlowの完了を待つ
        yield return StartCoroutine(ChangeSceneFlow(nextSceneName));
    }

    private IEnumerator ChangeSceneFlow(string nextSceneName)
    {
        Debug.Log("シーンチェンジ処理開始…");

        // すべてのタスクを同時に開始
        List<Coroutine> runningCoroutines = new List<Coroutine>();
        foreach (var task in return2TitleTasks)
        {
            runningCoroutines.Add(StartCoroutine(task));
        }

        // すべてのタスクの完了を待つ
        foreach (var coroutine in runningCoroutines)
        {
            yield return coroutine;
        }
        // 最低2秒待つ
        yield return new WaitForSeconds(2f);

        return2TitleTasks.Clear();
        changeScene = false;
        Debug.Log("シーンチェンジ処理完了");
        SceneManager.LoadScene(nextSceneName, LoadSceneMode.Single);
    }
}
