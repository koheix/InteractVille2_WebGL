using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System;
using TMPro;
using System.Collections.Generic;
using System.Collections;


public class TitleManager : MonoBehaviour
{
    [Header("UI要素")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button exitButton;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private Button userNameConfirmButton;
    
    [Header("BGM設定")]
    [SerializeField] private AudioSource bgmAudioSource;
    [SerializeField] private AudioClip titleBGM;

    [Header("設定内容")]
    [SerializeField] private TMP_InputField apiKeyInput;
    [SerializeField] private Slider bgmVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;
    [SerializeField] private Button settingsCloseButton;
    [SerializeField] private TMP_InputField userNameInput;

    [Header("日記表示用UI")]
    [SerializeField] private TextMeshProUGUI diaryDisplayText;

    [Header("playfab設定")]
    [SerializeField] private PlayFabLoginManager playFabLoginManager;
    
    // [Header("シーン設定")]
    // [SerializeField] private string gameSceneName = "MainGameScene";
    
    void Start()
    {
        // playFabLoginManagerを使ってPlayFabにログイン
        // playFabLoginManager.LoginWithDeviceID();

        // StartCoroutine(InitializeTitle());
        InitializeTitle();
    }

    // private IEnumerator InitializeTitle()
    private void InitializeTitle()
    {
        // 設定パネルを初期状態では非表示にする
        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        // ボタンにイベントリスナーを追加
        if (startButton != null)
            startButton.onClick.AddListener(OnStartButtonClicked);
            
        if (settingsButton != null)
            settingsButton.onClick.AddListener(OnSettingsButtonClicked);
            
        if (exitButton != null)
            exitButton.onClick.AddListener(OnExitButtonClicked);
            
        if (settingsCloseButton != null)
            settingsCloseButton.onClick.AddListener(OnSettingsCloseButtonClicked);

        if (userNameConfirmButton != null)
            userNameConfirmButton.onClick.AddListener(OnUserNameConfirmed);


        // // まずPlayFabにログイン
        // // playFabLoginManager.LoginWithDeviceID(success =>
        // playFabLoginManager.LoginWithUserName(success =>
        // {
        //     if (success)
        //     {
        //         // ログイン成功
        //         Debug.Log("ログインに成功しました");
        //     }
        //     else
        //     {
        //         Debug.LogError("ログインに失敗しました");
        //     }
        // });

        // // ログインが完了するまで待機
        // yield return null;

        if (userNameInput != null)
        {
            // プレイヤーの名前をロードして表示
            userNameInput.text = PlayerPrefs.GetString("userName", "");

            // if(userNameInput.text != "") {
            //     // まずPlayFabにログイン
            //     // playFabLoginManager.LoginWithDeviceID(success =>
            //     playFabLoginManager.LoginWithUserName(success =>
            //     {
            //         if (success)
            //         {
            //             // ログイン成功
            //             Debug.Log("ログインに成功しました");
            //         }
            //         else
            //         {
            //             Debug.LogError("ログインに失敗しました");
            //         }
            //     });
            //     // ログインが完了するまで待機
            //     yield return null;

            //     StartCoroutine(SetDiary(userNameInput.text));
            // }
            // if(userNameInput.text != "") 
            // {
            //     bool isLoginComplete = false;
            //     bool isLoginSuccess = false;

            //     // ログイン処理を開始
            //     playFabLoginManager.LoginWithUserName(success =>
            //     {
            //         isLoginSuccess = success;
            //         isLoginComplete = true;
                    
            //         if (success)
            //         {
            //             Debug.Log("ログインに成功しました");
            //         }
            //         else
            //         {
            //             Debug.LogError("ログインに失敗しました");
            //         }
            //     });

            //     // ログインが完了するまで待機
            //     while (!isLoginComplete)
            //     {
            //         yield return null;
            //     }

            //     // ログイン成功時のみ次の処理へ
            //     if (isLoginSuccess)
            //     {
            //         yield return StartCoroutine(SetDiary(userNameInput.text));
            //     }
            // }
            // // 名前が入っていれば、ともハムの日記も表示する
            // if (diaryDisplayText != null && userNameInput.text != "")
            // {
            //     List<string> diaryList = new List<string>();
            //     diaryList = SaveDao.LoadData(
            //         userNameInput.text,
            //         PlayerData => PlayerData.friendHamMemory
            //     );
            //     // 最後の日記1件を表示
            //     if (diaryList.Count > 0)
            //     {
            //         diaryDisplayText.text = diaryList[diaryList.Count - 1];
            //     }
            //     else
            //     {
            //         diaryDisplayText.text = "ともハムの日記:\nまだ日記はありません。";
            //     }
            // }
            // else if (diaryDisplayText != null)
            // {
            //     diaryDisplayText.text = "ともハムの日記:\n名前を入力してね！";
            // }
        }
            
        // BGMの設定と再生
        SetupBGM();
        
        // 設定値の読み込み
        LoadSettings();
    }

    // 日記の設定
    private void SetDiary(string userName)
    {
        // 名前が入っていれば、ともハムの日記も表示する
        if (diaryDisplayText != null && userName != "")
        {
            List<string> diaryList = new List<string>();
            diaryList = SaveDao.LoadData(
                userName,
                PlayerData => PlayerData.friendHamMemory
            );
            Debug.Log(diaryList);
            // // ここおかしい？
            // yield return null; // データのロードを待機
            // 最後の日記1件を表示
            if (diaryList != null && diaryList.Count > 0)
            {
                diaryDisplayText.text = diaryList[diaryList.Count - 1];
            }
            else
            {
                diaryDisplayText.text = "ともハムの日記:\nまだ日記はありません。";
            }
        }
        else if (diaryDisplayText != null)
        {
            diaryDisplayText.text = "ともハムの日記:\n名前を入力してね！";
        }
    }
    
    private void SetupBGM()
    {
        if (bgmAudioSource == null)
        {
            // AudioSourceが設定されていない場合、自動で作成
            bgmAudioSource = gameObject.AddComponent<AudioSource>();
        }
        
        if (titleBGM != null)
        {
            bgmAudioSource.clip = titleBGM;
            bgmAudioSource.loop = true;
            bgmAudioSource.volume = PlayerPrefs.GetFloat("BGMVolume", 0.7f);
            bgmAudioSource.Play();
        }
    }
    
    private void LoadSettings()
    {
        // 保存された設定値を読み込み
        String apiKey = PlayerPrefs.GetString("APIKey");
        float bgmVolume = PlayerPrefs.GetFloat("BGMVolume", 0.7f);
        float sfxVolume = PlayerPrefs.GetFloat("SFXVolume", 0.7f);


        if (apiKeyInput != null)
        {
            apiKeyInput.text = apiKey;
            apiKeyInput.onValueChanged.AddListener(OnAPIKeyChanged);
        }

        if (bgmVolumeSlider != null)
        {
            bgmVolumeSlider.value = bgmVolume;
            bgmVolumeSlider.onValueChanged.AddListener(OnBGMVolumeChanged);
        }
            
        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.value = sfxVolume;
            sfxVolumeSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
        }
        // BGM音量を適用
        if (bgmAudioSource != null)
        {
            bgmAudioSource.volume = bgmVolume;
        }

        // // 名前のinputのリスナーを設定
        // if (userNameInput != null)
        // {
        //     userNameInput.onValueChanged.AddListener(OnUserNameChanged);
        // }
        
        // usernameconfirmedbuttonのリスナーを設定
        if (userNameConfirmButton != null)
        {
            userNameConfirmButton.onClick.AddListener(OnUserNameConfirmed);
        }
    }
    
    // スタートボタンクリック時の処理
    private void OnStartButtonClicked()
    {

        Debug.Log(userNameInput.text);
        //プレイヤーの名前をロード
        if (userNameInput.text == null || userNameInput.text == "")
        {
            //ポップアップとかで出すようにする
            Debug.Log("なまえをいれてログインしてね");
            return;
        }
        else
        {
            PlayerPrefs.SetString("userName", userNameInput.text);
        }

        Debug.Log("ゲームを開始します");

        
        // BGMをフェードアウト（オプション）
        StartCoroutine(FadeOutBGM(1.0f));
        
        // ゲームシーンに遷移
        // 最後にタイトルに移動する前のシーンに遷移する
        string lastSceneName = SaveDao.LoadData(PlayerPrefs.GetString("userName", default), PlayerData => PlayerData.lastSceneName);
        SceneManager.LoadScene(lastSceneName);
    }
    
    // 設定ボタンクリック時の処理
    private void OnSettingsButtonClicked()
    {
        Debug.Log("設定画面を開きます");
        
        if (settingsPanel != null)
            settingsPanel.SetActive(true);
    }
    
    // 設定閉じるボタンクリック時の処理
    private void OnSettingsCloseButtonClicked()
    {
        Debug.Log("設定画面を閉じます");
        
        if (settingsPanel != null)
            settingsPanel.SetActive(false);
            
        // 設定を保存
        SaveSettings();
    }

    // API Keyが更新されたとき
    private void OnAPIKeyChanged(String value)
    {
        Debug.Log("API Keyが入力されました");
        PlayerPrefs.SetString("APIKey", value);
    }
    
    // 終了ボタンクリック時の処理
    private void OnExitButtonClicked()
    {
        QuitManager.Instance.RequestQuit();
    }
    
    // BGM音量変更時の処理
    private void OnBGMVolumeChanged(float value)
    {
        if (bgmAudioSource != null)
            bgmAudioSource.volume = value;
            
        PlayerPrefs.SetFloat("BGMVolume", value);
    }
    
    // SFX音量変更時の処理
    private void OnSFXVolumeChanged(float value)
    {
        PlayerPrefs.SetFloat("SFXVolume", value);
        // ここでSFXの音量を更新する処理を追加
    }

    // // usernameinput変更時の処理（日記の反映)
    // private void OnUserNameChanged(string value)
    // {
    //     SetDiary(value);
    // }

    // usernameconfirmbuttonクリック時の処理
    private void OnUserNameConfirmed()
    {
        string userName = userNameInput.text;
        if (string.IsNullOrEmpty(userName))
        {
            Debug.Log("ユーザー名を入力してください");
            return;
        }

        PlayerPrefs.SetString("userName", userName);
        Debug.Log($"ユーザー名をローカルに保存しました: {userName}");

        // まずPlayFabにログイン
        // playFabLoginManager.LoginWithDeviceID(success =>
        playFabLoginManager.LoginWithUserName(success =>
        {
            if (success)
            {
                // ログイン成功
                Debug.Log("ログインに成功しました");
                SetDiary(userName);
            }
            else
            {
                Debug.LogError("ログインに失敗しました");
            }
        });


        // 日記の更新
    }
    
    private void SaveSettings()
    {
        PlayerPrefs.Save();
        Debug.Log("設定を保存しました");
    }
    
    // BGMフェードアウトのコルーチン
    private System.Collections.IEnumerator FadeOutBGM(float fadeTime)
    {
        if (bgmAudioSource == null) yield break;
        
        float startVolume = bgmAudioSource.volume;
        
        while (bgmAudioSource.volume > 0)
        {
            bgmAudioSource.volume -= startVolume * Time.deltaTime / fadeTime;
            yield return null;
        }
        
        bgmAudioSource.Stop();
        bgmAudioSource.volume = startVolume;
    }

    // ゲーム終了時の処理
    private void OnDestroy()
    {
        // イベントリスナーのクリーンアップ
        if (startButton != null)
            startButton.onClick.RemoveListener(OnStartButtonClicked);

        if (settingsButton != null)
            settingsButton.onClick.RemoveListener(OnSettingsButtonClicked);

        if (exitButton != null)
            exitButton.onClick.RemoveListener(OnExitButtonClicked);

        if (settingsCloseButton != null)
            settingsCloseButton.onClick.RemoveListener(OnSettingsCloseButtonClicked);

        if (bgmVolumeSlider != null)
            bgmVolumeSlider.onValueChanged.RemoveListener(OnBGMVolumeChanged);

        if (sfxVolumeSlider != null)
            sfxVolumeSlider.onValueChanged.RemoveListener(OnSFXVolumeChanged);
        if (apiKeyInput != null)
            apiKeyInput.onValueChanged.RemoveListener(OnAPIKeyChanged);
        // if (userNameInput != null)
        //     userNameInput.onValueChanged.RemoveListener(OnUserNameChanged);
        if (userNameConfirmButton != null)
            userNameConfirmButton.onClick.RemoveListener(OnUserNameConfirmed);
    }
}