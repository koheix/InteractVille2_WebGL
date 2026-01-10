
using UnityEngine;
using PlayFab;
using PlayFab.ClientModels;
using System;

public class PlayFabLoginManager : MonoBehaviour
{
    private const string PLAYER_DATA_KEY = "PlayerData";
    private static bool _isLoggedIn = false;

    public static bool IsLoggedIn => _isLoggedIn;

    // ゲーム開始時に呼び出す
    public void LoginWithCustomID(string customID, Action<bool> onComplete = null)
    {
        var request = new LoginWithCustomIDRequest
        {
            CustomId = customID,
            CreateAccount = true, // アカウントが存在しない場合は自動作成
            // ここで取得したいデータを指定する
            InfoRequestParameters = new GetPlayerCombinedInfoRequestParams
            {
                // GetUserReadOnlyData = true, // 読み取り専用データが必要な場合
                GetUserData = true,         // プレイヤーデータ(TitleDataなど)が必要な場合
                // UserReadOnlyDataNames = new List<string> { "MyKey" } // 特定のキーだけ指定も可能
            }
        };

        PlayFabClientAPI.LoginWithCustomID(request,
            result =>
            {
                Debug.Log("PlayFabログイン成功");
                _isLoggedIn = true;
                onComplete?.Invoke(true);
                var json = result.InfoResultPayload.UserData[PLAYER_DATA_KEY].Value;
                var loadedData = JsonUtility.FromJson<PlayerData>(json);
                SaveDao.SetCache(customID, loadedData);
            },
            error =>
            {
                Debug.LogError($"PlayFabログイン失敗: {error.GenerateErrorReport()}");
                _isLoggedIn = false;
                onComplete?.Invoke(false);
            }
        );
    }

    // デバイスIDを使った自動ログイン（推奨）
    public void LoginWithDeviceID(Action<bool> onComplete = null)
    {
        string deviceID = SystemInfo.deviceUniqueIdentifier;
        LoginWithCustomID(deviceID, onComplete);
    }

    // userNameを使ったログイン
    public void LoginWithUserName(Action<bool> onComplete = null)
    {
        string userName = PlayerPrefs.GetString("userName", "");
        if (string.IsNullOrEmpty(userName))
        {
            Debug.LogError("userNameが設定されていません。");
            onComplete?.Invoke(false);
            return;
        }
        LoginWithCustomID(userName, onComplete);
    }

    // ログアウト
    public void Logout()
    {
        PlayFabClientAPI.ForgetAllCredentials();
        _isLoggedIn = false;
        Debug.Log("PlayFabログアウト");
    }
}