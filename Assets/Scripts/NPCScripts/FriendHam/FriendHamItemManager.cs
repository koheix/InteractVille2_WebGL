// ここに、ともハムがアイテムをプレゼントされたときの処理を追加する。
// ShopManagerとplayerinventoryを参考にする
using UnityEngine;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;
using System.Linq;

public class FriendHamItemManager : MonoBehaviour
{
    [Header("インベントリ")]
    //すべてのアイテムを設定する
    [SerializeField] private List<ItemData> allItems;
    // プレイヤーのインベントリアイテムをセットする
    // [SerializeField] private Dictionary<string, int> inventoryItems = new Dictionary<string, int>();
    [SerializeField] private PlayerInventory playerInventory;
    private Dictionary<string, int> inventoryItems = new Dictionary<string, int>();

    private List<string> items = new List<string>();

    // アイテムボタンのプレハブ
    [SerializeField] private GameObject itemButtonPrefab;

    // インベントリのGridLayoutGroupにアイテムを表示するためのTransform
    [SerializeField] private Transform presentBoxListPanel;
    // プレゼントボックスのテキストボックス
    [SerializeField] private TextMeshProUGUI presentDialogueText;
    // プレゼントしたアイテムの管理用（ともハムのインベントリ）
    [SerializeField] private Dictionary<string, int> presentItems = new Dictionary<string, int>();

    private ItemData selectedItem = null; // 現在選択されているアイテム 

    // singletonパターン
    public static FriendHamItemManager Instance { get; private set; }
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
        // インベントリデータの読み込み
        LoadInventoryData();
        // ともハムのインベントリデータの読み込み  
        LoadPresentItemData();
    }

    void Start()
    {
        // // インベントリデータの読み込み
        // LoadInventoryData();
        // QuitManagerに保存タスクを登録
        QuitManager.Instance.AddQuitTask(SavePresentItemData());

    }

    // void OnEnable()
    // {
    //     // インベントリデータの読み込み
    //     LoadInventoryData();
    //     // ともハムのインベントリデータの読み込み  
    //     LoadPresentItemData();
    // }

    // インベントリデータの読み込み
    private void LoadInventoryData()
    {
        Debug.Log("インベントリデータを読み込み中...");
        string userName = PlayerPrefs.GetString("userName", "default");

        // インベントリの読み込みと変換
        // List<InventorySlot> inventoryData = SaveDao.LoadData(userName, data => data.inventoryItems);
        // inventoryItems = inventoryData.ToDictionary(slot => slot.item, slot => slot.count);
        // nullは除外する
        // inventoryItems = inventoryData.Where(slot => slot.itemName != null) .ToDictionary(slot => slot.itemName, slot => slot.count);
        //
        inventoryItems = playerInventory.GetInventoryDictionary();
        items = new List<string>(inventoryItems.Keys);
        // デバッグ表示
        foreach (var item in items)
        {
            Debug.Log($"Loaded item: {item}");
        }

        // インベントリUIの更新
        // PopulateInventory(inventoryPanel, itemButtonPrefab, items.Count);
        PopulateInventory(presentBoxListPanel, itemButtonPrefab, inventoryItems.Count);

        Debug.Log("インベントリデータの読み込み完了");
    }

    // ともハムのインベントリデータの読み込み
    private void LoadPresentItemData()
    {
        Debug.Log("ともハムのインベントリデータを読み込み中...");
        string userName = PlayerPrefs.GetString("userName", "default");
        // インベントリの読み込みと変換
        List<InventorySlot> presentData = SaveDao.LoadData(userName, data => data.friendHamInventoryItems);
        // presentItems = presentData.ToDictionary(slot => slot.item, slot => slot.count);
        // nullは除外する
        presentItems = presentData.Where(slot => slot.itemName != null) .ToDictionary(slot => slot.itemName, slot => slot.count);
        
        //
        // inventoryItems = new Dictionary<ItemData, int>(data.inventoryItems);
        // items = new List<string>(presentItems.Keys);
        // // デバッグ表示
        // foreach (var item in items)
        // {
        //     Debug.Log($"Loaded present item: {item}");
        // }
        Debug.Log("ともハムのインベントリデータの読み込み完了");
    }
    
    public void PresentItem()
    {
        // ともハムにアイテムをプレゼントする処理
        if (presentItems.ContainsKey(selectedItem.itemName))
        {
            presentItems[selectedItem.itemName] += 1;
        }
        else
        {
            presentItems[selectedItem.itemName] = 1;
        }
        Debug.Log($"{selectedItem.itemName}をともハムにプレゼントしました！");
        // activity メモリを更新する(friendHamActivityMemoryに、「selectedItem.itemNameがプレゼントされた（時間）」)
        SaveDao.UpdateData(
            PlayerPrefs.GetString("userName", "default"), 
            data => data.friendHamActivityMemory.Add($"{selectedItem.itemName}がプレゼントされた（{TimeUtil.GetCurrentTimeString()}）")
        );

        // プレイヤーのインベントリからアイテムを減らす処理
        if (inventoryItems.ContainsKey(selectedItem.itemName))
        {
            inventoryItems[selectedItem.itemName] -= 1;
            if (inventoryItems[selectedItem.itemName] <= 0)
            {
                inventoryItems.Remove(selectedItem.itemName);
            }
        }
        else
        {
            // ここはありえないが、一応警告処理を実装
            Debug.LogWarning($"{selectedItem.itemName}がプレイヤーのインベントリに存在しません！");
        }

        // プレイヤーのインベントリともハムのインベントリを即座に更新
        // プレイヤーのインベントリデータを保存する
        // プレイヤーのインベントリデータを反映する
        StartCoroutine(SavePresentItemData());
        playerInventory.SetInventoryDictionary(inventoryItems);
        StartCoroutine(playerInventory.SaveInventoryData());
        playerInventory.LoadInventoryData();

        // UI更新など
        // UpdateUI();
        // 子のGridLayoutGroupにアイテムを追加表示
        // PopulateInventory(inventoryPanel, itemButtonPrefab, items.Count);
        PopulateInventory(presentBoxListPanel, itemButtonPrefab, inventoryItems.Count);
    }
    
    // private void UpdateUI()
    // {
    //     // UI更新処理をここに
    // }

    void PopulateInventory(Transform panel, GameObject itemButtonPrefab, int itemCount)
    {
        // 既存のアイテム表示をクリア
        foreach (Transform child in panel)
        {
            Destroy(child.gameObject);
        }

        items = new List<string>(inventoryItems.Keys);
        Debug.Log(items.Count);
        Debug.Log(itemCount);
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
            // ボタンをクリックしたときのリスナーを設定
            var button = itemButtonObj.GetComponent<Button>();
            if (button != null)
            {
                int index = i; // ローカル変数にキャプチャ
                button.onClick.AddListener(() => {
                    Debug.Log($"index: {index}, items.Count: {items.Count}");
                    selectedItem = allItems.Where(item => item.itemName == items[index]).FirstOrDefault();
                    Debug.Log($"Clicked item: {selectedItem.itemName}");
                    // プレゼントボックスのテキストを更新
                    presentDialogueText.text = $"{selectedItem.itemName}をともハムにプレゼントしますか？";
                });
            }
        }
    }

    // プレイヤーのインベントリデータを保存するコルーチン
    private System.Collections.IEnumerator SavePresentItemData()
    {
        Debug.Log("プレゼントデータを保存中...");
        string userName = PlayerPrefs.GetString("userName", "default");
        // // SaveDao.UpdateData(userName, data => data.inventoryItems = new List<ItemData>(items));
        // SaveDao.UpdateData(userName, data => data.inventoryItems = new Dictionary<ItemData, int>(inventoryItems));
        var presentList = presentItems.Select(kvp => 
            // new InventorySlot { item = kvp.Key, count = kvp.Value }
            new InventorySlot { itemName = kvp.Key, count = kvp.Value }
        ).ToList();
        SaveDao.UpdateData(userName, data => data.friendHamInventoryItems = presentList);
        
        // Debug.Log("プレイヤーのインベントリデータを保存中...");
        // var inventoryList = inventoryItems.Select(kvp => 
        //     new InventorySlot { itemName = kvp.Key, count = kvp.Value }
        // ).ToList();
        // SaveDao.UpdateData(userName, data => data.inventoryItems = inventoryList);


        yield return null;
        Debug.Log("プレゼントデータの保存完了");
    }
}
