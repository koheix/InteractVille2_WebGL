using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine.UI;


/*
characterの動きを制御するためのコード
歩行距離を計測する
*/
public class CharacterController : MonoBehaviour
{
    [Header("player speed")]
    [SerializeField] private float moveSpeed = 5f;
    private Animator animator;
    private Rigidbody2D rb;
    private Vector2 moveInput;

    [Header("joystickの参照")]
    [SerializeField] private VariableJoystick joystick;

    // 歩行距離計測用の変数
    private Vector3 lastPosition;
    private float totalWalkDistance = 0f;

    private int hunger = 100;

    void Start()
    {
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();  // Rigidbody2D を取得
        rb.gravityScale = 0;  // 重力を無効化
        rb.freezeRotation = true;  // 回転を固定

        // データから最後の位置を読み込んで設定
        Vector2 savedPosition = new Vector2();
        // savedPosition.x = SaveDao.LoadData(PlayerPrefs.GetString("userName", default), data => data.lastPosition[0]);
        // savedPosition.y = SaveDao.LoadData(PlayerPrefs.GetString("userName", default), data => data.lastPosition[1]);
        // ゲームをプレイしたことがあれば最後のシーンから再開する
        List<LastPositionClass> positionData = SaveDao.LoadData(PlayerPrefs.GetString("userName", default), data => data.lastPostions);
        // if (positionData != null)
        // {
        savedPosition.x = positionData.Find(x => x.sceneName == SceneManager.GetActiveScene().name).lastPosition[0];
        savedPosition.y = positionData.Find(x => x.sceneName == SceneManager.GetActiveScene().name).lastPosition[1];
        // }
        // // ゲームが初プレイならメインゲームシーンにデフォルト座標でスポーンする

        transform.position = savedPosition;

        // 初期位置を記録
        lastPosition = transform.position;

        hunger = SaveDao.LoadData(PlayerPrefs.GetString("userName", default), data => data.hunger);

        // プレイヤーの最後のシーンを記録する
        QuitManager.Instance.AddReturn2TitleTask(SaveLastSceneName());
        QuitManager.Instance.AddReturn2TitleTask(SavePlayerPosition());
    }

    // void Update()
    // {

    //     // 移動距離を計算
    //     float distanceThisFrame = Vector3.Distance(transform.position, lastPosition);
    //     totalWalkDistance += distanceThisFrame;
    //     lastPosition = transform.position;

    //     // Debug.Log($"総移動距離: {totalWalkDistance:F2}");

    //     if (InputController.Instance != null && InputController.Instance.canMove)
    //     {
    //         // 入力処理（Raw を使うとキビキビした動きになる）
    //         moveInput.x = Input.GetAxisRaw("Horizontal");
    //         moveInput.y = Input.GetAxisRaw("Vertical");
    //         moveInput.Normalize();  // 斜め移動を速くしすぎないように正規化
    //         // アニメーション処理
    //         if (moveInput == Vector2.zero)
    //         {
    //             animator.SetInteger("WalkDirection", 0);
    //         }
    //         else if (moveInput.x > 0)
    //         {
    //             animator.SetInteger("WalkDirection", 4);
    //         }
    //         else if (moveInput.x < 0)
    //         {
    //             animator.SetInteger("WalkDirection", 2);
    //         }
    //         else if (moveInput.y > 0)
    //         {
    //             animator.SetInteger("WalkDirection", 3);
    //         }
    //         else if (moveInput.y < 0)
    //         {
    //             animator.SetInteger("WalkDirection", 1);
    //         }
    //     }
    // }

    void Update()
    {
        float distanceThisFrame = Vector3.Distance(transform.position, lastPosition);
        totalWalkDistance += distanceThisFrame;
        lastPosition = transform.position;

        if (InputController.Instance != null && InputController.Instance.canMove)
        {
            // --- キーボードとジョイスティックの両方に対応 ---
            
            // キーボード入力
            float moveX = Input.GetAxisRaw("Horizontal");
            float moveY = Input.GetAxisRaw("Vertical");

            // ジョイスティック入力があれば上書き（または加算）
            if (joystick != null && joystick.Direction != Vector2.zero)
            {
                moveX = joystick.Horizontal;
                moveY = joystick.Vertical;
            }

            moveInput = new Vector2(moveX, moveY);
            
            if (moveInput.magnitude > 0.1f) // 少しでも入力があれば
            {
                moveInput.Normalize();
                UpdateAnimation(moveInput); 
            }
            else
            {
                animator.SetInteger("WalkDirection", 0);
            }
        }
    }

    private void UpdateAnimation(Vector2 move)
    {
        // // 入力がほとんどない場合はアイドル状態(0)へ
        // if (move.magnitude < 0.1f)
        // {
        //     animator.SetInteger("WalkDirection", 0);
        //     return;
        // }

        // X軸とY軸どちらの入力が大きいかで、優先する向きを決める
        if (Mathf.Abs(move.x) > Mathf.Abs(move.y))
        {
            // 横方向への移動が強い場合
            if (move.x > 0)
                animator.SetInteger("WalkDirection", 4); // 右
            else
                animator.SetInteger("WalkDirection", 2); // 左
        }
        else
        {
            // 縦方向への移動が強い場合
            if (move.y > 0)
                animator.SetInteger("WalkDirection", 3); // 上
            else
                animator.SetInteger("WalkDirection", 1); // 下
        }
    }

    void FixedUpdate()
    {
        // Rigidbody2D で移動する（transform.position ではなく velocity を使う）
        rb.linearVelocity = moveInput * moveSpeed;

        // //体力が減っていたらスピードを遅くする
        int hunger = CharacterStatus.hunger;
        if (hunger > 60)
        {
            moveSpeed = 5f;
        }
        else if (hunger > 30)
        {
            moveSpeed = 4f;
        }
        else
        {
            moveSpeed = 3f;
        }
    }

    // 外部から歩行距離を取得するメソッド
    public float GetTotalWalkDistance()
    {
        return totalWalkDistance;
    }

    // 歩行距離をリセットするメソッド
    public void ResetWalkDistance()
    {
        totalWalkDistance = 0f;
    }

    // シーンチェンジの際に、プレイヤーの最後の座標を記録する
    public IEnumerator SavePlayerPosition()
    {
        Debug.Log("このシーンの最後の座標を記録中...");
        // 今のシーンのキャラクターの座標を記録する
        string nowSceneName = SceneManager.GetActiveScene().name;
        List<LastPositionClass> positionData = SaveDao.LoadData(PlayerPrefs.GetString("userName", default), data => data.lastPostions);
        positionData.Find(x => x.sceneName == SceneManager.GetActiveScene().name).lastPosition[0] = transform.position.x;
        // positionData.Find(x => x.sceneName == SceneManager.GetActiveScene().name).lastPosition[1] = transform.position.y - 1.0f;
        positionData.Find(x => x.sceneName == SceneManager.GetActiveScene().name).lastPosition[1] = transform.position.y;
        SaveDao.UpdateData(PlayerPrefs.GetString("userName", default), data => data.lastPostions = positionData);
        yield return null;
        Debug.Log("プレイヤーの座標データの保存完了");
    }

    // // タイトルへ戻るときにシーン名とpositionを記録する
    // タイトルへ戻るときにシーン名を記録する
    public IEnumerator SaveLastSceneName()
    {
        // List<LastPositionClass> positionData = SaveDao.LoadData(PlayerPrefs.GetString("userName", default), data => data.lastPostions);
        // LastPositionClass lastPosition = positionData.Find
        // SaveDao.LoadData<
        // SaveDao.UpdateData(PlayerPrefs.GetString("userName", "default"), data => data.LastPositions. = new LastPositionClass);
        SaveDao.UpdateData(PlayerPrefs.GetString("userName", default), data => data.lastSceneName = SceneManager.GetActiveScene().name);
        yield return null;
        Debug.Log("最後のシーン名の保存完了");
    }
}
