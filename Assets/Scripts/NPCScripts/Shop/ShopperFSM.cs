/*
    shopper の状態を管理する FSMクラス
*/

using UnityEngine;

// ショップ会話の状態
public enum ShopState
{
    Idle,       // 待機中
    Greeting,   // 挨拶
    BuyMenu,    // 購入メニュー
    // SellMenu,   // 売却メニュー
    End         // 終了
}

public class ShopperFSM
{
    // 現在の状態を保持
    // getterのみ公開
    private ShopState currentState = ShopState.Idle;
    public ShopState CurrentState { get { return currentState; } }

    // 現在の状態とyes, noボタンの選択に基づいて状態を変更
    // ボタンを押すときに呼び出す
    public DialogueLine[] changeState(bool isYes)
    {
        if(currentState == ShopState.Idle)
        {
            return EnterState(ShopState.Greeting);
        }
        else if (currentState == ShopState.Greeting)
        {
            if (isYes)
            {
                return EnterState(ShopState.BuyMenu);
            }
            else
            {
                return EnterState(ShopState.End);
            }
        }
        else if (currentState == ShopState.BuyMenu)
        {
            return EnterState(ShopState.End);
        }
        else // currentState == ShopState.End
        {
            return EnterState(ShopState.Idle);
        }

    }

    DialogueLine[] EnterState(ShopState newState)
    {
        currentState = newState;
        switch (newState)
        {
            case ShopState.Greeting:
                Debug.Log("NPC：いらっしゃい！今日はお買い物ですか？");
                DialogueLine[] lines = new DialogueLine[1];
                lines[0] = new DialogueLine
                {
                    characterName = "おみせのひと",
                    text = "いらっしゃい！今日はお買い物ですか？"
                };
                // ShowMainMenu(); ここはdialoguesystemでやる
                return lines;

            case ShopState.BuyMenu:
                Debug.Log("NPC：こちらが商品一覧です");
                DialogueLine[] buyLines = new DialogueLine[1];
                buyLines[0] = new DialogueLine
                {
                    characterName = "おみせのひと",
                    text = "こちらが商品一覧です"
                };
                // 商品リスト表示処理はdialoguesystemでやる
                return buyLines;

            // case ShopState.SellMenu:
            //     Debug.Log("NPC：売りたいもんあるん？見せて💰");
            //     // インベントリの売却処理を書く
            //     break;

            case ShopState.End:
                Debug.Log("NPC：また来てね！");
                DialogueLine[] endLines = new DialogueLine[1];
                endLines[0] = new DialogueLine
                {
                    characterName = "おみせのひと",
                    text = "また来てね！"
                };
                // 会話終了処理はdialoguesystemでやる
                return endLines;
        }
        DialogueLine[] defaultLines = new DialogueLine[0];
        return defaultLines;
    }

    void ShowMainMenu()
    {
        Debug.Log("1. 買う\n2. 売る\n3. やめる");

        // // 仮：キー入力でメニュー選択
        // StartCoroutine(WaitForMenuInput());
    }

    // System.Collections.IEnumerator WaitForMenuInput()
    // {
    //     bool selected = false;

    //     while (!selected)
    //     {
    //         if (Input.GetKeyDown(KeyCode.Alpha1))
    //         {
    //             EnterState(ShopState.BuyMenu);
    //             selected = true;
    //         }
    //         else if (Input.GetKeyDown(KeyCode.Alpha2))
    //         {
    //             EnterState(ShopState.SellMenu);
    //             selected = true;
    //         }
    //         else if (Input.GetKeyDown(KeyCode.Alpha3))
    //         {
    //             EnterState(ShopState.End);
    //             selected = true;
    //         }
    //         yield return null;
    //     }
    // }
}