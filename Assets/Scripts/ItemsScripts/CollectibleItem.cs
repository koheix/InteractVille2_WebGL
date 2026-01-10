using UnityEngine;

// ===========================================
// アイテムのスクリプト
// ===========================================
public class CollectibleItem : MonoBehaviour
{
    [Header("アイテム設定")]
    // [SerializeField] private string itemName = "Apple";
    [SerializeField] private int itemValue = 1;
    // [SerializeField] private AudioClip collectSound;
    [SerializeField] private ItemData item;
    
    [Header("エフェクト")]
    // [SerializeField] private GameObject collectEffect;
    [SerializeField] private float bobSpeed = 2f;
    [SerializeField] private float bobHeight = 0.3f;
    
    private Vector3 startPosition;
    private bool isCollected = false;

    public static int appleCount =  SaveDao.LoadData(PlayerPrefs.GetString("userName", "default"), data => data.appleCount);
    
    void Start()
    {
        startPosition = transform.position;

        // appleCount = SaveDao.LoadData(PlayerPrefs.GetString("userName", "default"), data => data.appleCount);
        Debug.Log("CollectibleItem Start appleCount: " + appleCount);
    }
    
    void Update()
    {
        // ゲットされていなければふわふわ浮く動きをする
        if (!isCollected)
        {
            float newY = startPosition.y + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            transform.position = new Vector3(transform.position.x, newY, transform.position.z);
        }
    }
    
    /* 
    衝突判定
    */
    void OnTriggerEnter2D(Collider2D other)
    {
        // ゲットされている
        if (isCollected) return;
        
        // プレイヤーと接触したらアイテムを取得
        if (other.CompareTag("Player"))
        {
            CollectItem(other.gameObject);
        }
    }
    
    private void CollectItem(GameObject player)
    {
        isCollected = true;
        
        // // サウンド再生
        // if (collectSound != null)
        // {
        //     AudioSource.PlayClipAtPoint(collectSound, transform.position);
        // }
        
        // // エフェクト生成
        // if (collectEffect != null)
        // {
        //     Instantiate(collectEffect, transform.position, Quaternion.identity);
        // }

        
        // アイテム取得処理（プレイヤーのインベントリに追加）
        PlayerInventory inventory = player.GetComponent<PlayerInventory>();
        if (inventory != null)
        {
            inventory.AddItem(item, itemValue);
        }
        //暫定的に実装（後で修正するかも）
        try
        {
            //ゲットしたリンゴを増やす処理
            // PlayerPrefs.SetInt("appleCount", PlayerPrefs.GetInt("appleCount") + 1);   
            appleCount += 1;
            SaveDao.UpdateData(PlayerPrefs.GetString("userName", default), data => data.appleCount = data.appleCount + 1);
        }
        catch (System.Exception)
        {
            //エラー処理を後で書く
            throw;
        }

        // // GameManagerに通知
        // GameManager.Instance?.OnItemCollected(itemName, itemValue);

        // オブジェクト削除
        Destroy(gameObject);
    }
}