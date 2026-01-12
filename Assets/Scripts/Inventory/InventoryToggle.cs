using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InventoryToggle : MonoBehaviour
{
    // public GameObject inventoryUI;
    public GameObject inventoryPanel;
    public Button inventoryToggleButton;
    // public Text inventoryToggleButtonText;
    public TextMeshProUGUI inventoryToggleButtonText;
    private bool isOpen = false;

    // singleton
    public static InventoryToggle Instance { get; private set; }
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
    }

    void Start(){
        // inventoryUI.SetActive(false);
        inventoryPanel.SetActive(false);
        if(inventoryToggleButton != null)
        {
            inventoryToggleButton.onClick.AddListener(OnInventoryToggleButtonClicked);
            inventoryToggleButtonText.text = "インベントリを開く";
            
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            // isOpen = !isOpen;
            // inventoryPanel.SetActive(isOpen);
            toggleInventory();
        }
    }

    // インベントリオープンボタンを押したときの処理
    private void OnInventoryToggleButtonClicked()
    {
        Debug.Log("インベントリボタンが押された");
        toggleInventory();
    }

    private void toggleInventory()
    {
        isOpen = !isOpen;
        inventoryPanel.SetActive(isOpen);
        if(isOpen)
        {
            inventoryToggleButtonText.text = "インベントリを閉じる";
        }
        else
        {
            inventoryToggleButtonText.text = "インベントリを開く";
        }
    }
}