using UnityEngine;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;

public class ShopManager : MonoBehaviour
{
    [Header("ショップに並ぶアイテム")]
    public List<ItemData> shopItems;
    private int appleCount; // プレイヤーの所持リンゴ
    public GameObject ItemButtonPrefab;   // アイテムボタンのプレハブ
    public Transform itemListPanel; // ItemDisplayUI ( GridLayoutGroup のついたオブジェクト )
    private ItemData selectedItem = null; // 現在選択されているアイテム

    [Header("Buy Box UI References")]
    // public GameObject buyBox;
    // public TextMeshProUGUI buyBoxCharacterNameText;
    public TextMeshProUGUI buyBoxDialogueText;
    // public Button buyBoxYesButton;
    // public Button buyBoxNoButton;

    [Header("Inventory Reference")]
    public PlayerInventory playerInventory;

    void Start()
    {
        // inventory参照の取得
        playerInventory = PlayerInventory.Instance;
    }

    void OnEnable()
    {
        // プレイヤーのリンゴを取得
        appleCount = SaveDao.LoadData(PlayerPrefs.GetString("userName", default), data => data.appleCount);
        Debug.Log("ShopManager OnEnable appleCount: " + appleCount);
        // QuitManager.Instance.AddQuitTask(SavePlayerData());

        // デバッグ用にショップアイテムを表示
        DisplayShopItems();

        // アイテムリストをUIに表示
        PopulateItemList(itemListPanel, ItemButtonPrefab, shopItems.Count);

        // // アイテム保存メソッドの登録
        // QuitManager.Instance.AddReturn2TitleTask(SavePlayerData());

    }
    
    // ショップアイテムを表示（デバッグ用）
    void DisplayShopItems()
    {
        Debug.Log("=== ショップアイテム一覧 ===");
        foreach (var item in shopItems)
        {
            Debug.Log($"{item.itemName} - {item.price}円");
        }
    }
    
    // アイテムを購入
    public bool BuyItem()
    {
        if(selectedItem == null)
        {
            Debug.Log("購入するアイテムが選択されていません！");
            // 何もしない
            return false;
        }
        if (appleCount >= selectedItem.price)
        {
            appleCount -= selectedItem.price;
            // 所持リンゴ数を保存(UIに表示しているので即時保存する)
            SaveDao.UpdateData(PlayerPrefs.GetString("userName", default), data => data.appleCount = appleCount);
            
            Debug.Log($"{selectedItem.itemName}を購入しました！ 残金: {appleCount}円");
            
            // ここでインベントリに追加する処理を呼ぶ
            AddToInventory(selectedItem);

            //インベントリデータの保存を行う
            StartCoroutine(playerInventory.SaveInventoryData());

            // selectedItem = null; // 選択状態を解除
            
            return true;
        }
        else
        {
            Debug.Log("所持リンゴ数が足りません！");
            return false;
        }
    }
    
    // インベントリに追加（後で実装）
    void AddToInventory(ItemData item)
    {
        // TODO: インベントリシステムと連携
        Debug.Log($"{item.itemName}をインベントリに追加");
        playerInventory.AddItem(item, 1);
    }

    public int GetAppleCount()
    {
        return appleCount;
    }

    // アイテムリストをUIに表示
    void PopulateItemList(Transform panel, GameObject itemButtonPrefab, int itemCount)
    {
        for (int i = 0; i < itemCount; i++)
        {
            GameObject itemButtonObj = Instantiate(itemButtonPrefab, panel);
            
            // 右下の個数表示をオフにする
            var text = itemButtonObj.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
            {
                // テスト用
                // text.text = shopItems[i].itemName;
                text.text = "";
            }
            // 画像を設定
            var iconImage = itemButtonObj.GetComponent<Image>();
            if (iconImage != null)
            {
                iconImage.sprite = shopItems[i].icon;
            }
            // ボタンをクリックしたときのリスナーを設定
            var button = itemButtonObj.GetComponent<Button>();
            if (button != null)
            {
                int index = i; // ローカル変数にキャプチャ
                button.onClick.AddListener(() => {
                    buyBoxDialogueText.text = $"{shopItems[index].itemName}は{shopItems[index].price}りんごでかえますよ！\nかいますか？";
                    // // アイテムを選択状態にする
                    // shopItems[index].isSelected = true;
                    selectedItem = shopItems[index];
                    Debug.Log($"選択されたアイテム: {selectedItem.itemName}");

                    // // 他のアイテムは選択解除
                    // for (int j = 0; j < shopItems.Count; j++)
                    // {
                    //     if (j != index)
                    //     {
                    //         shopItems[j].isSelected = false;
                    //     }
                    // }
                });
            }
        }
    }

    // // プレイヤーデータを保存するコルーチン
    // private System.Collections.IEnumerator SavePlayerData()
    // {
    //     Debug.Log("プレイヤーデータを保存中...");
    //     Debug.Log($"所持リンゴ数: {appleCount}");
    //     // 所持リンゴ数を保存
    //     SaveDao.UpdateData(PlayerPrefs.GetString("userName", default), data => data.appleCount = appleCount);
    //     // インベントリデータも保存する処理をここで追加
    //     playerInventory.SaveInventoryData();
    //     yield return null; // 1フレーム待つ
    //     Debug.Log("プレイヤーデータの保存完了");
    // }

}
