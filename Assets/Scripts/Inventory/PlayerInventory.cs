using UnityEngine;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;
using System.Linq;
using UnityEngine.SceneManagement;

public class PlayerInventory : MonoBehaviour
{
    [Header("インベントリ")]
    //すべてのアイテムを設定する
    [SerializeField] private List<ItemData> allItems;
    [SerializeField] private Dictionary<string, int> inventoryItems = new Dictionary<string, int>();
    // getter for inventoryItems
    public Dictionary<string, int> GetInventoryDictionary()
    {
        return inventoryItems;
    }
    // setter for inventoryItems
    public void SetInventoryDictionary(Dictionary<string, int> newInventory)
    {
        inventoryItems = newInventory;
    }

    private List<string> items = new List<string>();

    // アイテムボタンのプレハブ
    [SerializeField] private GameObject itemButtonPrefab;

    // インベントリのGridLayoutGroupにアイテムを表示するためのTransform
    // [SerializeField] private Transform inventoryPanel;
    private static Transform inventoryPanel;

    // singletonパターン
    public static PlayerInventory Instance { get; private set; }
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            // inventoryPanel = GetComponentInChildren<Panel>().transform;
            inventoryPanel = GameObject.Find("InventoryPanel").transform;
        }
        else
        {
            Destroy(gameObject);
        }
        // インベントリデータの読み込み
        LoadInventoryData();
    }

    // // パネルを更新するメソッド
    // public void SetPanel(Transform newPanel)
    // {
    //     inventoryPanel = newPanel;
    // }

    void Start()
    {
        // // インベントリデータの読み込み
        // LoadInventoryData();
        // QuitManagerに保存タスクを登録
        QuitManager.Instance.AddQuitTask(SaveInventoryData());
    }

//     private void OnEnable()
//     {
//         SceneManager.sceneLoaded += OnSceneLoaded;
//     }

//     // 無効になったときにイベントを解除（メモリリーク防止）
//     private void OnDisable()
//     {
//         SceneManager.sceneLoaded -= OnSceneLoaded;
//     }

//     // シーンが読み込まれたときに呼ばれるメソッド
//     private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
//     {
//         Debug.Log($"シーンが読み込まれました: {scene.name}");
        
//         // シーンチェンジするたびにinventoryPanelを更新する
//         inventoryPanel = GameObject.Find("InventoryPanel").transform;
//     }

    // // シーンチェンジのイベント
    // void OnEnable()
    // {
    //     // シーンチェンジするたびにinventoryPanelを更新する
    //     inventoryPanel = GameObject.Find("InventoryPanel").transform;
    // }

    // インベントリデータの読み込み
    public void LoadInventoryData()
    {
        Debug.Log("インベントリデータを読み込み中...");
        string userName = PlayerPrefs.GetString("userName", "default");
        // PlayerData data = SaveDao.LoadStructData(userName);
        // items = new List<ItemData>(data.inventoryItems);
        // インベントリの読み込みと変換
        List<InventorySlot> inventoryData = SaveDao.LoadData(userName, data => data.inventoryItems);
        // inventoryItems = inventoryData.ToDictionary(slot => slot.item, slot => slot.count);
        // nullは除外する
        inventoryItems = inventoryData.Where(slot => slot.itemName != null) .ToDictionary(slot => slot.itemName, slot => slot.count);
        //
        // inventoryItems = new Dictionary<ItemData, int>(data.inventoryItems);
        items = new List<string>(inventoryItems.Keys);
        // デバッグ表示
        foreach (var item in items)
        {
            Debug.Log($"Loaded item: {item}");
        }

        // インベントリUIの更新
        // PopulateInventory(inventoryPanel, itemButtonPrefab, items.Count);
        PopulateInventory(inventoryPanel, itemButtonPrefab, inventoryItems.Count);

        Debug.Log("インベントリデータの読み込み完了");
    }
    
    public void AddItem(ItemData item, int value)
    {
        // items.Add(item);
        if (inventoryItems.ContainsKey(item.itemName))
        {
            inventoryItems[item.itemName] += value;
        }
        else
        {
            inventoryItems[item.itemName] = value;
        }
        Debug.Log($"{item.itemName}を取得しました！");
        
        // UI更新など
        // UpdateUI();
        // 子のGridLayoutGroupにアイテムを追加表示
        // PopulateInventory(inventoryPanel, itemButtonPrefab, items.Count);
        PopulateInventory(inventoryPanel, itemButtonPrefab, inventoryItems.Count);
    }
    
    // private void UpdateUI()
    // {
    //     // UI更新処理をここに
    // }

    void PopulateInventory(Transform panel, GameObject itemButtonPrefab, int itemCount)
    {
        // 既存のアイテム表示をクリア
        //-------------------ここがエラーになるので要修正
        foreach (Transform child in panel)
        {
            Destroy(child.gameObject);
        }

        items = new List<string>(inventoryItems.Keys);
        for (int i = 0; i < itemCount; i++)
        {
            GameObject itemButtonObj = Instantiate(itemButtonPrefab, panel);
            
            // テスト用
            var text = itemButtonObj.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
            {
                // text.text = $"{items[i].itemName} x{inventoryItems[items[i]]}";
                text.text = $"{inventoryItems[items[i]]}";
            }
            // 画像を設定
            var iconImage = itemButtonObj.GetComponent<Image>();
            if (iconImage != null)
            {
                iconImage.sprite = allItems
                // リストの中から、itemNameがitems[i]と完全に一致するものを探す
                .Where(item => item.itemName == items[i])
                // 条件に一致する最初の要素を取得（見つからない場合はnullを返す）
                .FirstOrDefault().icon;
            }
        }
    }

    // プレイヤーのインベントリデータを保存するコルーチン
    public System.Collections.IEnumerator SaveInventoryData()
    {
        Debug.Log("インベントリデータを保存中...");
        string userName = PlayerPrefs.GetString("userName", "default");
        // // SaveDao.UpdateData(userName, data => data.inventoryItems = new List<ItemData>(items));
        // SaveDao.UpdateData(userName, data => data.inventoryItems = new Dictionary<ItemData, int>(inventoryItems));
        var inventoryList = inventoryItems.Select(kvp => 
            // new InventorySlot { item = kvp.Key, count = kvp.Value }
            new InventorySlot { itemName = kvp.Key, count = kvp.Value }
        ).ToList();
        SaveDao.UpdateData(userName, data => data.inventoryItems = inventoryList);

        yield return null;
        Debug.Log("インベントリデータの保存完了");
    }
}
