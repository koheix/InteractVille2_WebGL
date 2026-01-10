using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;

public class ButtonSoundEffect : MonoBehaviour
{
    [Header("個別音源設定（オプション）")]
    [SerializeField] private AudioClip customClickSound;
    [SerializeField] private AudioClip customHoverSound;
    
    [Header("音量調整")]
    [Range(0f, 2f)]
    [SerializeField] private float volumeMultiplier = 1f;
    
    private Button button;
    
    void Start()
    {
        button = GetComponent<Button>();
        if (button != null)
        {
            // ボタンクリック時のイベントを追加
            button.onClick.AddListener(PlayClickSound);
        }
        else
        {
            Debug.LogWarning($"ButtonSoundEffect: Button component not found on {gameObject.name}");
        }
    }
    
    // クリック音を再生
    private void PlayClickSound()
    {
        if (SoundManager.Instance != null)
        {
            if (customClickSound != null)
            {
                SoundManager.Instance.PlaySFX(customClickSound, volumeMultiplier);
            }
            else
            {
                SoundManager.Instance.PlayButtonClickSound();
            }
        }
    }
    
    // ホバー音を再生（オプション）
    public void PlayHoverSound()
    {
        if (SoundManager.Instance != null)
        {
            if (customHoverSound != null)
            {
                SoundManager.Instance.PlaySFX(customHoverSound, volumeMultiplier * 0.5f);
            }
            else
            {
                SoundManager.Instance.PlayButtonHoverSound();
            }
        }
    }
    
    void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(PlayClickSound);
        }
    }
}