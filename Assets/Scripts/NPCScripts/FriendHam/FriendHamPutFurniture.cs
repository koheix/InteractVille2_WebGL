using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Tilemaps;

public class FriendHamPutFurniture : MonoBehaviour
{
    // ともハムが家具を置いていたら、ポップアップを表示するためにtrueにする
    private static bool isPlaced = false;

    [Header("家具を置いたポップアップのCanvas")]
    public GameObject NoticePutFurnitureCanvas;
    // OKボタン
     public GameObject okButton;
    [Header("ともハムが家具を置くグリッドのレイヤー")]
    // public LayerMask furnitureLayer;
    public Tilemap tilemap;

    [Header("すべての家具アイテムを設定")]
    //すべてのアイテムを設定する
    [SerializeField] private List<ItemData> allFurnitureItems;
    // ともハムのインベントリ
    private Dictionary<string, int> friendHamItems = new Dictionary<string, int>();
    // 配置する家具のSprite
    private Sprite spriteToPlace;
    private Tile tileToPlace; // Spriteから変換したTile

    private void LoadPresentItemData()
    {
        Debug.Log("ともハムのインベントリデータを読み込み中...");
        string userName = PlayerPrefs.GetString("userName", "default");
        // インベントリの読み込みと変換
        List<InventorySlot> presentData = SaveDao.LoadData(userName, data => data.friendHamInventoryItems);
        // nullは除外する
        friendHamItems = presentData.Where(slot => slot.itemName != null) .ToDictionary(slot => slot.itemName, slot => slot.count);
        Debug.Log("ともハムのインベントリデータの読み込み完了");
    }

    void Start()
    {
        // ともハムのインベントリデータの読み込み
        LoadPresentItemData();
        // isPlacedの初期化
        isPlaced = SaveDao.LoadData(PlayerPrefs.GetString("userName", "default"), data => data.isFriendHamPlacedFurnitureNoticeShown);
        // 家具がおかれていたらポップアップを表示する
        NoticePutFurnitureCanvas.SetActive(isPlaced);
        // okButtonを押したときにポップアップを非表示にする
        okButton.GetComponent<Button>().onClick.AddListener(() =>
        {
            // ポップアップを非表示にする
            NoticePutFurnitureCanvas.SetActive(false);
            isPlaced = false;
            SaveDao.UpdateData(PlayerPrefs.GetString("userName", "default"), data => data.isFriendHamPlacedFurnitureNoticeShown = false);
        });
        // QuitManagerにともハムが家具を置く処理をセットする
        QuitManager.Instance.AddReturn2TitleTask(checkPutFurniture());

        // ともハムが置いた家具をtilemapに復元する
        refreshPlacedFurniture();
    }

    // 家具を置くか判定するメソッド
    public IEnumerator checkPutFurniture()
    {
        // ともハムのインベントリを確認して、家具アイテムがあるかチェックする
        foreach (var item in friendHamItems)
        {
            string itemName = item.Key;
            int itemCount = item.Value;

            // allFurnitureItemsにitemNameが含まれているかチェック
            bool isFurniture = allFurnitureItems.Exists(furnitureItem => furnitureItem.itemName == itemName);

            if (isFurniture && itemCount > 0)
            {
                // 家具アイテムが見つかった場合、家具を置く処理を実行
                yield return StartCoroutine(PutFurniture(itemName));
                yield break; // 一つ置いたら終了
            }
        }
    }

    // gridに家具を置く処理（quitmanagerにセットしておく？）
    private IEnumerator PutFurniture(string itemName)
    {
        // 家具を置いたお知らせをtrueにする
        isPlaced = true;
        SaveDao.UpdateData(PlayerPrefs.GetString("userName", "default"), data => data.isFriendHamPlacedFurnitureNoticeShown = true);
        // gridのともハムが家具を置くレイヤーにアイテムを置く
        ItemData furnitureItem = allFurnitureItems.Find(item => item.itemName == itemName);
        if (furnitureItem != null)
        {
            // SpriteをTileに変換して配置
            spriteToPlace = furnitureItem.icon;
            // x = -10から7まで、y=-3から3までの範囲でランダムな空いているセルを探して配置
            int x = Random.Range(-10, 8);
            int y = Random.Range(-3, 4);
            PlaceSpriteAsTile(new Vector3Int(x, y, 0), spriteToPlace);
            Debug.Log($"{itemName}をともハムが配置しました");

            // ともハムのインベントリから家具アイテムを1つ減らす
            friendHamItems[itemName] -= 1;
            if (friendHamItems[itemName] <= 0)
            {
                friendHamItems.Remove(itemName);
            }

            // ともハムのインベントリデータを保存する
            string userName = PlayerPrefs.GetString("userName", "default");
            var friendHamItemsList = friendHamItems.Select(kvp =>
                new InventorySlot { itemName = kvp.Key, count = kvp.Value }
            ).ToList();
            SaveDao.UpdateData(userName, data => data.friendHamInventoryItems = friendHamItemsList);

            // ともハムが置いた家具のデータも保存する
            var placedFurnitureList = SaveDao.LoadData(userName, data => data.friendHamPlacedFurniture);;
            placedFurnitureList.Add(new TileSaveData { position = new Vector3Int(x, y, 0), tileName = itemName });
            SaveDao.UpdateData(userName, data => data.friendHamPlacedFurniture = placedFurnitureList);

            // 家具を置いたというアクティビティメモリを保存
            // activity メモリを更新する(friendHamActivityMemoryに、「itemNameを部屋に置いた（時間）」)
            SaveDao.UpdateData(
                PlayerPrefs.GetString("userName", "default"), 
                data => data.friendHamActivityMemory.Add($"{itemName}を部屋に置いた（{TimeUtil.GetCurrentTimeString()}）")
            );


            yield return null;
        }
        else
        {
            Debug.LogWarning($"家具アイテム {itemName} が見つかりませんでした");
        }
    }

    // 指定した座標が空いていればタイルを配置
    // public void PlaceTileIfEmpty(Vector3Int cellPosition)
    // {
    //     if (tilemap == null || tileToPlace == null)
    //     {
    //         Debug.LogWarning("TilemapまたはTileが設定されていません");
    //         return;
    //     }

    //     // そのセルにタイルがあるか確認
    //     if (!tilemap.HasTile(cellPosition))
    //     {
    //         // 空いているのでタイルを配置
    //         tilemap.SetTile(cellPosition, tileToPlace);
    //         Debug.Log($"タイルを配置しました: {cellPosition}");
    //     }
    //     else
    //     {
    //         Debug.Log($"既にタイルが存在します: {cellPosition}");
    //     }
    // }

    // Spriteを指定して動的に変換・配置
    public void PlaceSpriteAsTile(Vector3Int cellPosition, Sprite sprite)
    {
        if (tilemap == null || sprite == null)
        {
            Debug.LogWarning("TilemapまたはSpriteが設定されていません");
            return;
        }

        if (!tilemap.HasTile(cellPosition))
        {
            Tile tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprite;
            tilemap.SetTile(cellPosition, tile);
            Debug.Log($"SpriteをTileとして配置しました: {cellPosition}");
        }
        else
        {
            Debug.Log($"既にタイルが存在します: {cellPosition}");
        }
    }

    // // ランダムな位置の空いているセルにタイルを配置
    // public void PlaceTileAtRandomEmptyCell(int minX, int maxX, int minY, int maxY)
    // {
    //     int attempts = 0;
    //     int maxAttempts = 100;

    //     while (attempts < maxAttempts)
    //     {
    //         int x = Random.Range(minX, maxX + 1);
    //         int y = Random.Range(minY, maxY + 1);
    //         Vector3Int pos = new Vector3Int(x, y, 0);

    //         if (!tilemap.HasTile(pos))
    //         {
    //             tilemap.SetTile(pos, tileToPlace);
    //             Debug.Log($"ランダム配置: {pos}");
    //             return;
    //         }

    //         attempts++;
    //     }

    //     Debug.LogWarning("空いているセルが見つかりませんでした");
    // }

    // ともハムが置いた家具をtilemapに復元する
    private void refreshPlacedFurniture()
    {
        string userName = PlayerPrefs.GetString("userName", "default");
        var placedFurnitureList = SaveDao.LoadData(userName, data => data.friendHamPlacedFurniture);
        foreach (var furniture in placedFurnitureList)
        {
            ItemData furnitureItem = allFurnitureItems.Find(item => item.itemName == furniture.tileName);
            if (furnitureItem != null)
            {
                PlaceSpriteAsTile(furniture.position, furnitureItem.icon);
                Debug.Log($"ともハムが置いた家具 {furniture.tileName} を復元しました");
            }
            else
            {
                Debug.LogWarning($"家具アイテム {furniture.tileName} が見つかりませんでした");
            }
        }
    }
}
