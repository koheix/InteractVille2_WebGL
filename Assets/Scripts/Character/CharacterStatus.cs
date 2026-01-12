using UnityEngine;
// using Microsoft.Unity.VisualStudio.Editor;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/*
characterのステータスを演算するコード
体力: 減ると歩く速度が遅くなる。ひまわりの種で回復する。
*/
public class CharacterStatus : MonoBehaviour
{
    //体力
    public static int hunger;
    private CharacterController cc;

    [Header("体力のUI")]
    [SerializeField] private Image hungerGauge;

    [Header("りんごのUI")]
    [SerializeField] private TextMeshProUGUI appleCountUI;
    //リンゴの数
    // public static int appleCount;
    // public int appleCount;
    // public static int appleCount;
    private int appleCount;

    // 停止時のhunger回復用
    [SerializeField] private float hungerRecoveryInterval = 0.1f; // 0.1秒ごと
    [SerializeField] private int hungerRecoveryAmount = 1; // 回復量
    private float idleTimer = 0f; // 止まっている時間をためて記録していく
    private float previousWalkDistance = 0f; // 前の移動距離

    void Start()
    {
        cc = GetComponent<CharacterController>();
        // 体力のロード
        hunger = SaveDao.LoadData(PlayerPrefs.GetString("userName", "default"), data => data.hunger);
        appleCount =  SaveDao.LoadData(PlayerPrefs.GetString("userName", "default"), data => data.appleCount);
        // シーンチェンジ後も体力を維持するため
        UpdateHungerUI();
        UpdateAppleCountUI();

        //最初の歩行距離
        previousWalkDistance = cc.GetTotalWalkDistance();

        QuitManager.Instance.AddReturn2TitleTask(SaveHungerAndAppleCount());
    }

    void Update()
    {
        
        //移動距離の取得
        float currentWalkDistance = cc.GetTotalWalkDistance();

        // 単純に歩数が定数の閾値を超えたら体力を減らす
        if (currentWalkDistance > 1)
        {
            //体力を減らす
            hunger = Mathf.Clamp(--hunger, 0, 100);
            // PlayerPrefs.SetInt("hunger", hunger);
            // SaveDao.UpdateData(PlayerPrefs.GetString("userName", "default"), data => data.hunger = hunger);
            cc.ResetWalkDistance();
            // statusのUIに反映
            UpdateHungerUI();

            // 歩いているのでタイマーをリセット
            idleTimer = 0f;
        }
        else
        {
            // プレイヤーが止まっている
            if(currentWalkDistance == previousWalkDistance)
            {
                // タイマー加算
                idleTimer += Time.deltaTime;

                // 1秒ごとにhungerを回復
                if(idleTimer >= hungerRecoveryInterval)
                {
                    // hungerの反映
                    hunger = Mathf.Clamp(hunger + hungerRecoveryAmount, 0, 100);
                    // PlayerPrefs.SetInt("hunger", hunger);
                    // SaveDao.UpdateData(PlayerPrefs.GetString("userName", "default"), data => data.hunger = hunger);
                    UpdateHungerUI();

                    // タイマーをリセット
                    idleTimer = 0f;

                }
            }
            else
            {
                // 移動しているならタイマーをリセット
                idleTimer = 0f;
            }
        }

        previousWalkDistance = currentWalkDistance;

        //リンゴの数をUIに反映
        UpdateAppleCountUI();
    }

    // hungerのUIを更新するメソッド
    private void UpdateHungerUI()
    {
        hungerGauge.fillAmount = hunger / 100f;
        if (hunger > 60)
        {
            hungerGauge.color = Color.green;
        }
        else if (hunger > 30)
        {
            hungerGauge.color = Color.yellow;
        }
        else
        {
            hungerGauge.color = Color.red;
        }
    }

    // apple countのUIを更新するメソッド
    private void UpdateAppleCountUI()
    {
        appleCount = SaveDao.LoadData(PlayerPrefs.GetString("userName", "default"), data => data.appleCount);
        appleCountUI.text = appleCount.ToString();
    }


    // hungerを保存
    public IEnumerator SaveHungerAndAppleCount()
    {
        SaveDao.UpdateData(PlayerPrefs.GetString("userName", default), data => data.hunger = hunger);
        SaveDao.UpdateData(PlayerPrefs.GetString("userName", default), data => data.appleCount = appleCount);
        yield return null;
        Debug.Log("体力の保存完了");
        Debug.Log("リンゴの保存完了");
    }


}