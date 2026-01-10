/* ともハムのFSMを管理するスクリプト
 *
 */

using UnityEngine;

// ともハム会話の状態
public enum FriendHamState
{
    // アイデアが固まるまで保留
    Idle,       // 待機中
    Greeting,   // 挨拶
    Present,    // プレゼントを渡す
    Chatting,   // 雑談
    Petting,    // なでる
    Farewell    // 別れの挨拶
}

// ともハムの会話の方向性の状態
// LLMの発話を助けるための詳細な状態（方針）
public enum FriendHamDetailState
{
    askHelp,        // 手伝いをお願いする
    giveAdvice,     // アドバイスをする
    shareStory,     // 自分の話をする
    expressFeelings, // 感情を表現する
    jokeAround      // 冗談を言う
}

public class FriendHamFSM
{

    // 現在の状態を保持
    // getterのみ公開
    private FriendHamState currentState = FriendHamState.Idle;
    public FriendHamState CurrentState { get { return currentState; } }

    // public DialogueLine[] changeState(string nextState)
    // {
    //     if(currentState == FriendHamState.Idle)
    //     {
    //         return EnterState(FriendHamState.Greeting);
    //     }
    //     else if (currentState == FriendHamState.Greeting)
    //     {
    //         if (isYes)
    //         {
    //             return EnterState(FriendHamState.BuyMenu);
    //         }
    //         else
    //         {
    //             return EnterState(FriendHamState.End);
    //         }
    //     }
    //     else if (currentState == FriendHamState.BuyMenu)
    //     {
    //         return EnterState(FriendHamState.End);
    //     }
    //     else // currentState == FriendHamState.End
    //     {
    //         return EnterState(FriendHamState.Idle);
    //     }
    // }

    public DialogueLine[] EnterState(FriendHamState newState)
    {
        currentState = newState;
        switch (newState)
        {
            case FriendHamState.Greeting:
                Debug.Log("NPC：こんにちは！ともハムだよ！");
                DialogueLine[] lines = new DialogueLine[1];
                lines[0] = new DialogueLine
                {
                    characterName = "ともハム",
                    text = "こんにちは！ともハムだよ！"
                };
                // ShowMainMenu(); ここはdialoguesystemでやる
                return lines;

            case FriendHamState.Present:
                Debug.Log("NPC：プレゼントくれるの！？");
                lines = new DialogueLine[1];
                lines[0] = new DialogueLine
                {
                    characterName = "ともハム",
                    text = "プレゼントくれるの！？"
                };
                return lines;
                // 未実装

            case FriendHamState.Chatting:
                Debug.Log("NPC：雑談しよう");
                lines = new DialogueLine[1];
                lines[0] = new DialogueLine
                {
                    characterName = "ともハム",
                    text = "雑談しよう"
                };
                return lines;
                // 未実装

            case FriendHamState.Farewell:
                Debug.Log("NPC：またね！");
                DialogueLine[] endLines = new DialogueLine[1];
                endLines[0] = new DialogueLine
                {
                    characterName = "ともハム",
                    text = "またね！"
                };
                // 会話終了処理はdialoguesystemでやる
                return endLines;
        }
        DialogueLine[] defaultLines = new DialogueLine[0];
        return defaultLines;
    }

}
