using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;

// ========================================
// 効果音マネージャー（シングルトン）
// ========================================
public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }
    
    [Header("効果音設定")]
    [SerializeField] private AudioSource sfxAudioSource;
    [SerializeField] private AudioClip buttonClickSound;
    [SerializeField] private AudioClip buttonHoverSound;
    
    [Header("音量設定")]
    [Range(0f, 1f)]
    [SerializeField] private float sfxVolume = 0.7f;
    
    void Awake()
    {
        // シングルトンパターン
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // シーン切り替え時も残す
            InitializeSoundManager();
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private void InitializeSoundManager()
    {
        // AudioSourceがない場合は作成
        if (sfxAudioSource == null)
        {
            sfxAudioSource = gameObject.AddComponent<AudioSource>();
        }
        
        sfxAudioSource.playOnAwake = false;
        sfxAudioSource.loop = false;
        
        // 保存された音量を読み込み
        sfxVolume = PlayerPrefs.GetFloat("SFXVolume", 0.7f);
        sfxAudioSource.volume = sfxVolume;
    }
    
    // ボタンクリック音を再生
    public void PlayButtonClickSound()
    {
        if (buttonClickSound != null)
        {
            sfxAudioSource.PlayOneShot(buttonClickSound, sfxVolume);
        }
    }
    
    // ボタンホバー音を再生
    public void PlayButtonHoverSound()
    {
        if (buttonHoverSound != null)
        {
            sfxAudioSource.PlayOneShot(buttonHoverSound, sfxVolume * 0.5f); // ホバー音は少し小さめ
        }
    }
    
    // 任意の効果音を再生
    public void PlaySFX(AudioClip clip, float volumeScale = 1.0f)
    {
        if (clip != null)
        {
            sfxAudioSource.PlayOneShot(clip, sfxVolume * volumeScale);
        }
    }
    
    // SFX音量を設定
    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        if (sfxAudioSource != null)
        {
            sfxAudioSource.volume = sfxVolume;
        }
        PlayerPrefs.SetFloat("SFXVolume", sfxVolume);
    }
    
    public float GetSFXVolume()
    {
        return sfxVolume;
    }
}
