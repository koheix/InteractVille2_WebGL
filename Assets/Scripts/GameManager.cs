using UnityEngine;

public class GameManager : MonoBehaviour
{
    // MainGameSceneがロードされたときに読み込まれる
    private void Start()
    {
        //プレイヤーのゲームデータをロードしておく?

        HandlePlayerSpawn();
    }

    private void HandlePlayerSpawn()
    {
        if (PlayerPrefs.HasKey("SpawnX") && PlayerPrefs.HasKey("SpawnY"))
        {
            float spawnX = PlayerPrefs.GetFloat("SpawnX");
            float spawnY = PlayerPrefs.GetFloat("SpawnY");

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                player.transform.position = new Vector3(spawnX, spawnY, 0);
                Debug.Log($"プレイヤーを位置({spawnX}, {spawnY})にスポーンしました。");
            }

            PlayerPrefs.DeleteKey("SpawnX");
            PlayerPrefs.DeleteKey("SpawnY");
        }
    }
}