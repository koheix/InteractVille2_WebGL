using UnityEngine;
using System;
using PlayFab;
using PlayFab.ClientModels;

public class TimeUtil
{
    public static string GetCurrentTimeString()
    {
        return DateTime.Now.ToString("yyyy年MM月dd日 HH時mm分ss秒");
    }

    public static DateTime GetCurrentDateTime()
    {
        return DateTime.Now;
    }

    // PlayFabのサーバー時刻を取得して返す（非同期）
    public static void GetSafeDateTime(Action<DateTime> onNormalizedTimeReceived, Action<PlayFabError> onError)
    {
        PlayFabClientAPI.GetTime(new GetTimeRequest(), result =>
        {
            // サーバー時刻（UTC）をローカル時刻に変換して返す
            DateTime serverTime = result.Time.ToLocalTime();
            onNormalizedTimeReceived?.Invoke(serverTime);
        }, 
        error => 
        {
            Debug.LogError("サーバー時刻の取得に失敗しました");
            onError?.Invoke(error);
        });
    }

    // 表示用のフォーマット済み文字列を取得する場合
    public static void GetSafeTimeString(Action<string> onStringReceived)
    {
        GetSafeDateTime(time => 
        {
            string formatted = time.ToString("yyyy年MM月dd日 HH時mm分ss秒");
            onStringReceived?.Invoke(formatted);
        }, 
        error => 
        { 
            Debug.LogError("サーバー時刻の取得に失敗しました");
        });
    }
}