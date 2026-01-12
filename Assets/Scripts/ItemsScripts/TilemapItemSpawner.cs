using UnityEngine.Tilemaps;
using UnityEngine;
using System.Collections.Generic;

public class TilemapItemSpawner : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private Tilemap groundTilemap; // 地面のTilemap
    [SerializeField] private GameObject itemPrefab; // アイテムのプレハブ
    
    [Header("スポーン設定")]
    [SerializeField] private int itemCount = 15;
    [SerializeField] private float itemHeight = 0.5f; // タイルより少し上
    [SerializeField] private int maxSpawnAttempts = 200;
    
    [Header("スポーン条件")]
    [SerializeField] private List<TileBase> allowedTiles = new List<TileBase>(); // スポーン可能なタイル
    [SerializeField] private List<TileBase> forbiddenTiles = new List<TileBase>(); // スポーン禁止タイル
    [SerializeField] private float minDistanceBetweenItems = 2f;
    
    // アイテムの場所を保持するリスト
    private List<Vector3Int> spawnedCellPositions = new List<Vector3Int>();
    
    void Start()
    {
        // if (groundTilemap == null)
        // {
        //     // groundTilemap = FindObjectOfType<Tilemap>();
        //     Debug.LogError("地面Tilemapが設定されていません！");
        //     return;
        // }
        
        SpawnItems();
    }
    
    public void SpawnItems()
    {
        //地面とアイテムが設定されていなければ何もしない
        if (groundTilemap == null || itemPrefab == null)
        {
            Debug.LogError("Tilemap または ItemPrefab が設定されていません！");
            return;
        }
        
        // アイテムの場所をリセット
        spawnedCellPositions.Clear();
        BoundsInt bounds = groundTilemap.cellBounds;
        
        int spawnedCount = 0;
        int attempts = 0;
        
        // アイテムをitemCountの数だけスポーンする。
        while (spawnedCount < itemCount && attempts < maxSpawnAttempts)
        {
            attempts++;

            // ランダムなセル位置を選択
            Vector3Int randomCell = new Vector3Int(
                Random.Range(bounds.xMin, bounds.xMax),
                Random.Range(bounds.yMin, bounds.yMax),
                0
            );

            // スポーン可能かチェック
            if (CanSpawnAtCell(randomCell))
            {
                SpawnItemAtCell(randomCell);
                spawnedCellPositions.Add(randomCell);
                spawnedCount++;
            }
        }
        
        Debug.Log($"アイテムを{spawnedCount}個スポーンしました（試行回数: {attempts}）");
    }
    
    private bool CanSpawnAtCell(Vector3Int cellPosition)
    {
        // そのセルにタイルがあるかチェック
        TileBase tile = groundTilemap.GetTile(cellPosition);
        if (tile == null) return false;
        
        // 許可されたタイルリストがある場合
        if (allowedTiles.Count > 0 && !allowedTiles.Contains(tile))
        {
            return false;
        }
        
        // 禁止されたタイルかチェック
        if (forbiddenTiles.Contains(tile))
        {
            return false;
        }
        
        // 他のアイテムとの距離をチェック
        foreach (Vector3Int spawnedCell in spawnedCellPositions)
        {
            float distance = Vector3Int.Distance(cellPosition, spawnedCell);
            if (distance < minDistanceBetweenItems)
            {
                return false;
            }
        }
        
        return true;
    }
    
    private void SpawnItemAtCell(Vector3Int cellPosition)
    {
        // セル座標をワールド座標に変換
        Vector3 worldPosition = groundTilemap.CellToWorld(cellPosition);
        
        // セルの中心に配置
        worldPosition += groundTilemap.cellSize * 0.5f;
        
        // 高さを調整
        worldPosition.z = itemHeight;
        
        // アイテムを生成
        GameObject spawnedItem = Instantiate(itemPrefab, worldPosition, Quaternion.identity);
        spawnedItem.transform.SetParent(transform);
        
        // デバッグ用にセル座標を保存
        ItemCellInfo cellInfo = spawnedItem.GetComponent<ItemCellInfo>();
        if (cellInfo == null)
        {
            cellInfo = spawnedItem.AddComponent<ItemCellInfo>();
        }
        cellInfo.cellPosition = cellPosition;
    }
    
    public void ClearAllItems()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(transform.GetChild(i).gameObject);
        }
        spawnedCellPositions.Clear();
        Debug.Log("すべてのアイテムを削除しました");
    }
    
    // Scene Viewでスポーン位置を可視化
    private void OnDrawGizmosSelected()
    {
        if (groundTilemap == null) return;
        
        Gizmos.color = Color.green;
        foreach (Vector3Int cellPos in spawnedCellPositions)
        {
            Vector3 worldPos = groundTilemap.CellToWorld(cellPos);
            worldPos += groundTilemap.cellSize * 0.5f;
            worldPos.z = itemHeight;
            
            Gizmos.DrawWireCube(worldPos, groundTilemap.cellSize);
        }
    }
}

/*
アイテムのセル情報を保持するヘルパー
*/
public class ItemCellInfo : MonoBehaviour
{
    public Vector3Int cellPosition;
}
