using UnityEngine;
using System;

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
}
