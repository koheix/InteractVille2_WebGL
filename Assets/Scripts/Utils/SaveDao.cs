// using UnityEngine;
// using System.IO;
// using System.Collections.Generic;

using UnityEngine;
using PlayFab;
using PlayFab.ClientModels;
using System;
using System.Collections.Generic;

// アイテムとその個数を管理するクラス
[System.Serializable]
public class InventorySlot
{
    // public ItemData item;
    public string itemName;
    public int count;
}

// シーン名とゲーム終了時のハムスターの場所を管理するクラス
[System.Serializable]
public class LastPositionClass
{
    public string sceneName;
    public float[] lastPosition;
}

// ともハムが置いた家具を管理するクラス
[System.Serializable]
public class TileSaveData {
    public Vector3Int position;
    public string tileName; // ItemDataの名前をIDとして使う
}

//セーブデータ
[System.Serializable]
public class PlayerData
{
    public string name;
    public int hunger = 100;
    public int appleCount = 0;
    // ゲーム終了時のシーン名を記録しておく
    public string lastSceneName = "MainGameScene";
    // public float[] lastPosition= {4f, 1.3f};

    // ゲーム終了時の位置を、シーン名ごとに保存する
    public List<LastPositionClass> lastPostions =  new List<LastPositionClass>()
    {
        new LastPositionClass { sceneName = "MainGameScene", lastPosition = new float[] {0.48f, -2.84f} },
        new LastPositionClass { sceneName = "HouseScene", lastPosition = new float[] {0.48f, -2.84f} },
        new LastPositionClass { sceneName = "ShopScene", lastPosition = new float[] {0.48f, -2.84f} }
    };
    //インベントリデータ(各アイテムの個数もここで管理する)
    // public List<ItemData> inventoryItems = new List<ItemData>();
    // public Dictionary<ItemData, int> inventoryItems = new Dictionary<ItemData, int>();
    public List<InventorySlot> inventoryItems = new List<InventorySlot>();

    // 友ハムのデータ

    public List<InventorySlot> friendHamInventoryItems = new List<InventorySlot>();
    // ともハムが置いた家具のデータ
    public List<TileSaveData> friendHamPlacedFurniture = new List<TileSaveData>();

    // ともハムが家具を置いたお知らせの有無
    public bool isFriendHamPlacedFurnitureNoticeShown = false;

    // ともハムのステータス
    public int friendHamValence = 50;
    public int friendHamArousal = 50;
    public int friendHamHunger = 50;
    public int friendHamCloseness = 50;

    public string friendHamCurrentMood = "普通";

    // プレゼントとか家具配置の思い出リスト(固定文字列で管理、（）で末端に時間記録) 
    public List<string> friendHamActivityMemory = new List<string>();
    public List<string> friendHamMemory = new List<string>();

    // 会話履歴
    public List<Message> conversationHistory = new List<Message>();


    // メタデータ
    public string lastPlayedDate = System.DateTime.Now.ToString(); // 要変更

    public bool isTutorialCompleted = false;

    // ともハムハウスでのチュートリアル完了フラグ
    public bool isFriendHamHouseTutorialCompleted = false;
}


// public class SaveDao
// {
//     public static void SaveStructData(string userName, PlayerData data)
//     {
//         string SavePath = Application.persistentDataPath + "/" + userName + ".json";
//         string json = JsonUtility.ToJson(data, true);
//         File.WriteAllText(SavePath, json);
//     }

//     public static PlayerData LoadStructData(string userName)
//     {
//         string SavePath = Application.persistentDataPath + "/" + userName + ".json";
//         if(File.Exists(SavePath))
//         {
//             string json = File.ReadAllText(SavePath);

//             return JsonUtility.FromJson<PlayerData>(json);
//         }
//         else
//         {
//             // データがなければ作成する
//             PlayerData data = new PlayerData();

//             data.name = userName;
//             //保存
//             SaveStructData(userName, data);

//             return data;
//         }
//     }

//     // 汎用更新メソッド
//     public static void UpdateData(string userName, System.Action<PlayerData> updateAction)
//     {
//         PlayerData data = LoadStructData(userName);
//         updateAction(data);  // 任意の更新処理を実行
//         SaveStructData(userName, data);
//     }
    
//     // 汎用ロードメソッド
//     public static T LoadData<T>(string userName, System.Func<PlayerData, T> selector)
//     {
//         PlayerData data = LoadStructData(userName);
//         return selector(data);
//     }

// }

// webGL対応版
public class SaveDao
{
    private const string PLAYER_DATA_KEY = "PlayerData";
    private static MonoBehaviour _coroutineRunner;

    // コルーチン実行用のMonoBehaviourを設定（初回に自動で作成）
    private static MonoBehaviour CoroutineRunner
    {
        get
        {
            if (_coroutineRunner == null)
            {
                GameObject go = new GameObject("SaveDaoCoroutineRunner");
                _coroutineRunner = go.AddComponent<CoroutineRunnerComponent>();
                UnityEngine.Object.DontDestroyOnLoad(go);
            }
            return _coroutineRunner;
        }
    }

    // 保存メソッド（元の使い方を維持）
    public static void SaveStructData(string userName, PlayerData data)
    {
        CoroutineRunner.StartCoroutine(SaveStructDataCoroutine(userName, data));
    }

    private static System.Collections.IEnumerator SaveStructDataCoroutine(string userName, PlayerData data)
    {
        string json = JsonUtility.ToJson(data, true);
        bool isDone = false;
        bool isSuccess = false;

        var request = new UpdateUserDataRequest
        {
            Data = new System.Collections.Generic.Dictionary<string, string>
            {
                { PLAYER_DATA_KEY, json }
            }
        };

        PlayFabClientAPI.UpdateUserData(request,
            result =>
            {
                Debug.Log("データ保存成功");
                isSuccess = true;
                isDone = true;
            },
            error =>
            {
                Debug.LogError($"データ保存失敗: {error.GenerateErrorReport()}");
                isDone = true;
            }
        );

        yield return new WaitUntil(() => isDone);
    }

    // ロードメソッド（非同期版なのでメソッド呼び出しを書き換える必要がある）
    public static void LoadStructData(string userName, System.Action<PlayerData> onComplete)
    {
        // キャッシュをチェック
        if (DataCache.HasCache(userName))
        {
            onComplete?.Invoke(DataCache.GetCache(userName));
            return;
        }

        // キャッシュがない場合は非同期ロード
        CoroutineRunner.StartCoroutine(LoadAndCacheData(userName, onComplete));
    }

    private static System.Collections.IEnumerator LoadAndCacheData(string userName, System.Action<PlayerData> onComplete)
    {
        bool isDone = false;
        PlayerData loadedData = null;

        var request = new GetUserDataRequest
        {
            Keys = new System.Collections.Generic.List<string> { PLAYER_DATA_KEY }
        };
        // // ログインしていたら、データを取得する
        // if (PlayFabClientAPI.IsClientLoggedIn())
        // {
        //     PlayFabClientAPI.GetUserData(request,
        //         result =>
        //         {
        //             if (result.Data != null && result.Data.ContainsKey(PLAYER_DATA_KEY))
        //             {
        //                 string json = result.Data[PLAYER_DATA_KEY].Value;
        //                 loadedData = JsonUtility.FromJson<PlayerData>(json);
        //                 Debug.Log("データロード成功");
        //             }
        //             else
        //             {
        //                 Debug.Log("データが存在しないため新規作成");
        //                 loadedData = new PlayerData();
        //                 loadedData.name = userName;
        //             }
        //             isDone = true;
        //         },
        //         error =>
        //         {
        //             Debug.LogError($"データロード失敗: {error.GenerateErrorReport()}");
        //             loadedData = new PlayerData();
        //             loadedData.name = userName;
        //             isDone = true;
        //         }
        //     );
        // }
        // else
        // {
        //     Debug.Log("未ログインのため新規データ作成");
        //     loadedData = new PlayerData();
        //     loadedData.name = userName;
        //     isDone = true;
        // }

        PlayFabClientAPI.GetUserData(request,
            result =>
            {
                if (result.Data != null && result.Data.ContainsKey(PLAYER_DATA_KEY))
                {
                    string json = result.Data[PLAYER_DATA_KEY].Value;
                    loadedData = JsonUtility.FromJson<PlayerData>(json);
                    Debug.Log("データロード成功");
                }
                else
                {
                    Debug.Log("データが存在しないため新規作成");
                    loadedData = new PlayerData();
                    loadedData.name = userName;
                }
                isDone = true;
            },
            error =>
            {
                Debug.LogError($"データロード失敗: {error.GenerateErrorReport()}");
                loadedData = new PlayerData();
                loadedData.name = userName;
                isDone = true;
            }
        );

        yield return new WaitUntil(() => isDone);

        if (loadedData != null)
        {
            DataCache.SetCache(userName, loadedData);
            onComplete?.Invoke(loadedData);
        }
    }

    // // ロードメソッド（元の使い方を維持 - キャッシュ使用）
    // public static PlayerData LoadStructData(string userName)
    // {
    //     // キャッシュをチェック
    //     if (DataCache.HasCache(userName))
    //     {
    //         return DataCache.GetCache(userName);
    //     }

    //     // キャッシュがない場合は非同期ロードを開始し、一時データを返す
    //     CoroutineRunner.StartCoroutine(LoadAndCacheData(userName));
        
    //     // 初回は新規データを返す（次回からはキャッシュを使用）
    //     PlayerData newData = new PlayerData();
    //     newData.name = userName;
    //     DataCache.SetCache(userName, newData);
    //     return newData;
    // }

    // private static System.Collections.IEnumerator LoadAndCacheData(string userName)
    // {
    //     bool isDone = false;
    //     PlayerData loadedData = null;

    //     var request = new GetUserDataRequest
    //     {
    //         Keys = new System.Collections.Generic.List<string> { PLAYER_DATA_KEY }
    //     };

    //     PlayFabClientAPI.GetUserData(request,
    //         result =>
    //         {
    //             if (result.Data != null && result.Data.ContainsKey(PLAYER_DATA_KEY))
    //             {
    //                 string json = result.Data[PLAYER_DATA_KEY].Value;
    //                 loadedData = JsonUtility.FromJson<PlayerData>(json);
    //                 Debug.Log("データロード成功");
    //             }
    //             else
    //             {
    //                 Debug.Log("データが存在しないため新規作成");
    //                 loadedData = new PlayerData();
    //                 loadedData.name = userName;
    //             }
    //             isDone = true;
    //         },
    //         error =>
    //         {
    //             Debug.LogError($"データロード失敗: {error.GenerateErrorReport()}");
    //             loadedData = new PlayerData();
    //             loadedData.name = userName;
    //             isDone = true;
    //         }
    //     );

    //     yield return new WaitUntil(() => isDone);

    //     if (loadedData != null)
    //     {
    //         DataCache.SetCache(userName, loadedData);
    //     }
    // }

    // 汎用更新メソッド（元の使い方を維持）
    public static void UpdateData(string userName, System.Action<PlayerData> updateAction)
    {
        // PlayerData data = LoadStructData(userName);
        // updateAction(data);
        // SaveStructData(userName, data);
        LoadStructData(userName, data =>
        {   
            updateAction(data);  // 任意の更新処理を実行
            SaveStructData(userName, data);
        });
    }
    
    // 汎用ロードメソッド（元の使い方を維持）
    public static T LoadData<T>(string userName, System.Func<PlayerData, T> selector)
    {
        // PlayerData data = LoadStructData(userName);
        // return selector(data);
        T result = default;
        LoadStructData(userName, data =>
        {
            result = selector(data);
        });
        return result;
    }

    // キャッシュをクリア（必要に応じて使用）
    public static void ClearCache(string userName)
    {
        DataCache.ClearCache(userName);
    }

    // キャッシュにデータを保存（ログインの際に必要に応じて使用）
    public static void SetCache(string userName, PlayerData data)
    {
        DataCache.SetCache(userName, data);
    }
}

// データキャッシュ管理クラス
internal static class DataCache
{
    private static System.Collections.Generic.Dictionary<string, PlayerData> _cache = 
        new System.Collections.Generic.Dictionary<string, PlayerData>();

    public static bool HasCache(string userName)
    {
        return _cache.ContainsKey(userName);
    }

    public static PlayerData GetCache(string userName)
    {
        return _cache[userName];
    }

    public static void SetCache(string userName, PlayerData data)
    {
        _cache[userName] = data;
    }

    public static void ClearCache(string userName)
    {
        if (_cache.ContainsKey(userName))
        {
            _cache.Remove(userName);
        }
    }
}

// コルーチン実行用のコンポーネント
internal class CoroutineRunnerComponent : MonoBehaviour { }
