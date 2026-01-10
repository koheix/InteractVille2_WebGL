using UnityEngine;

[CreateAssetMenu(fileName = "NewItem", menuName = "Item/NewItem")]
public class ItemData : ScriptableObject
{
    [Header("基本情報")]
    public string itemName;
    [TextArea(3, 5)]
    public string description;
    public Sprite icon;
    
    [Header("価格")]
    public int price;
    
    [Header("アイテムタイプ")]
    public ItemType itemType;
    // ショップに販売しているか
    public bool isSold;
    
    [Header("効果")]
    public int healAmount;  // 食べ物の回復量(精神的な)
    public int toolCnt; // 道具の使用回数

    [Header("その他の属性")]
    public bool isSelected = false; // 選択中かどうか(ショップで)
}

public enum ItemType
{
    Food, //食料
    Flower, // 花
    Seed, // 種(リンゴ等の)
    Tool, //道具
    Furniture, // 家具
    Misc  // その他
}