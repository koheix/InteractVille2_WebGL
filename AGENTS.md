# AGENTS.md — InteractVille2 WebGL

AI エージェント（Claude Code など）がこのリポジトリで作業するときの規約と前提。
`.claude/` 配下のエージェント・スキルは汎用に書かれており、プロジェクト固有のことは
すべてこのファイルを参照する。**作業を始める前に必ず読むこと。**

## 概要

森に住むハムスターになって、友達の「ともハム」と会話したり、りんごを集めて
ショップで家具を買ったりする 2D ゲーム。ともハムとの会話は LLM（Claude）で生成し、
時刻・会話履歴・親密度によって内容や会話できる回数が変わる。
WebGL ビルドをブラウザ（PC・スマートフォン）で遊ぶ。遊び方は `readme.md` を参照。

## 技術スタック

| 分類 | 内容 |
|---|---|
| エンジン | Unity **6000.0.32f1**（`ProjectSettings/ProjectVersion.txt`） |
| 描画 | URP（2D）、Tilemap、Cinemachine、TextMesh Pro |
| 入力 | Input System（`Assets/InputSystem_Actions.inputactions`）、Joystick Pack（スマホ用） |
| 対象 | WebGL（PC ブラウザ・スマートフォンブラウザ） |
| ログイン・保存 | PlayFab（`Assets/PlayFabSDK`）。セーブデータは `SaveDao.cs` の `PlayerData` |
| LLM | Claude API を Cloudflare Workers のプロキシ経由で呼ぶ（`LLMBridge.cs`）。プロキシのコードはこのリポジトリには無い |
| JSON | Newtonsoft.Json（`com.unity.nuget.newtonsoft-json`）と `JsonUtility` |
| テスト | Unity Test Framework（`com.unity.test-framework`）。**現時点でテストは 0 件** |

## 構成

```
Assets/
  Scenes/            CommonUIScene（常駐 UI）, TitleScene, MainGameScene（村）, HouseScene（ともハムの家）, ShopScene
  Scripts/
    Character/             プレイヤーの移動・入力・ステータス
    CommonUISceneScripts/  起動（BootStrap）、共通 UI、ローディング
    Inventory/             インベントリ
    ItemsScripts/          アイテム定義（ItemData）、拾えるアイテム、Tilemap 上の出現
    LLMControler/          LLM 呼び出し（LLMBridge）とメッセージ構造
    NPCScripts/            会話システム。FriendHam/（ともハム：FSM・会話・家具配置・ステータス）、Shop/
    TitleScripts/          タイトル、PlayFab ログイン、サウンド
    TutorialScripts/       チュートリアル
    Utils/                 SaveDao（PlayFab 保存）、TimeUtil（時刻）、ボタン判定
    GameManager.cs         MainGameScene でのプレイヤーのスポーン位置
    DoorTrigger.cs         ドアによるシーン移動
    ButtonSoundEffect.cs   ボタンの効果音
  Prefabs/ Items/ Tiles/ Images/ Audio/ CharacterAnimation/ CharacterUIImages/ Resources/ Settings/
  FarmerAssets/            フォント・マテリアル・効果音・スプライト・タイル（外部アセットかどうかは要確認）
  PlayFabSDK/ PlayFabEditorExtensions/ Joystick Pack/ "Sprout Lands - Sprites - Basic pack 1"/ TextMesh Pro/   ← 外部アセット
Packages/manifest.json    パッケージ
ProjectSettings/          プロジェクト設定（ビルド対象シーンは EditorBuildSettings.asset）
```

## ブランチとコミット

- `main` へ直接コミット・push しない（フックが拒否する）。PR を経由してマージする
- ブランチ名: `devel-<内容がわかる名前>`（例: `devel-fix-speakturnsUI`）。Issue 対応なら `devel-issue<番号>-<内容>`
- コミットメッセージ: `<種別>: <日本語の要約>`。種別は `add` / `update` / `fix` など既存の履歴に合わせる
  （例: `fix: ともハムの発話のストリーミング`）
- PR のマージ、Issue のクローズは人間が行う

## 規約

### 共通
- コメント・ドキュメント・コミットメッセージ・PR は日本語
- 依頼されていないリファクタリングや整形をしない。差分は必要最小限に
- 秘密情報（API キー、トークン）をクライアントのコードやアセットに入れない。
  LLM の API キーはプロキシ側にあり、クライアントはプロキシの URL だけを知る

### Unity
- **`.meta` ファイルは対のアセットと必ず一緒に追加・移動・削除する。** 新しいスクリプトやアセットを
  エディタ外で作った場合、`.meta` は Unity がインポートしたときに生成される。
  `.meta` 無しでコミットすると GUID が環境ごとに変わり、参照が外れる
- アセットの移動・リネームは、できるだけ Unity エディタ上（または Unity MCP）で行う。
  ファイルシステム上で動かすと参照が切れることがある
- シーン（`.unity`）・プレハブ（`.prefab`）・`.asset` の YAML を手で書き換えるのは最後の手段。
  やむを得ず編集する場合は、変更箇所を PR に明記し、エディタで開いて壊れていないかの確認を利用者に依頼する
- `*.asset` は Git LFS 管理（`.gitattributes`）。LFS のポインタを壊さない
- `Library/` `Temp/` `Logs/` `UserSettings/` は生成物。触らない・コミットしない
- 外部アセット（PlayFabSDK、Joystick Pack、Sprout Lands、TextMesh Pro の同梱物）は改変しない。
  挙動を変えたいときは自前のスクリプトで包む
- `MonoBehaviour` にロジックを詰め込まない。判断ロジック（会話回数の計算、親密度、在庫の増減など）は
  `MonoBehaviour` に依存しない C# のクラス・メソッドに切り出し、EditMode テストで検証できる形にする
- 現在時刻は `TimeUtil` 経由で取得する。判断ロジックには時刻を引数で渡す（直接 `DateTime.Now` を呼ばない）

### WebGL
- スレッド（`System.Threading.Thread`、`Task.Run` による並列実行）は使えない。非同期はコルーチンか
  `UnityWebRequest` のコールバックで書く
- ローカルファイルへの保存に頼らない。永続化は PlayFab（`SaveDao`）か `PlayerPrefs`
- 日本語入力（IME）とスマートフォンのソフトウェアキーボードに配慮する。入力まわりを変えたら PC とスマホの両方で確認が要る
- 画面サイズ・縦横比が変わっても UI が見切れないようにする（全画面表示でも確認）

### LLM（ともハムの会話）
- リクエストは `LLMBridge` にまとめる。他のクラスから直接 HTTP を叩かない
- タイムアウト・エラー応答・壊れた JSON・空の応答で、会話 UI が固まったりセーブデータが壊れたりしないこと
- プロンプトに入れるユーザー入力は、そのまま埋め込んでよい形か確認する

## テスト方針

- テストは Unity Test Framework の **EditMode** を基本とする
  - 置き場所: `Assets/Tests/EditMode/`（テスト用の `.asmdef` を作り、本体のスクリプトを参照する）
  - 本体のスクリプトは現在 `Assembly-CSharp`（asmdef なし）にあり、テスト用 asmdef からは参照できない。
    テストを書くときは、対象のロジックを asmdef 付きのフォルダへ切り出すかどうかを利用者と相談してから進める
- **各テストには `[Description]` で「何を保証するか」を日本語で書く（必須）。** 検証のたびに生成される
  テスト仕様書（`reports/test-spec.md`、PR 本文にも掲載）にそのまま載り、人間はこれを読んで
  どのテストが実行されたかを確認する。クラスにも `[Description]` を付けると、仕様書の見出しの説明になる

  ```csharp
  [TestFixture, Description("ともハムと話せる回数の計算")]
  public class SpeakTurnsTests
  {
      [Test, Description("最後の会話から1時間ちょうど経つと会話回数が上限まで回復する")]
      public void RemainingTurns_ResetsAfterOneHour() { /* ... */ }

      // パラメータ化テストはメソッドの [Description] が各ケースに引き継がれる
      [Description("親密度のしきい値ちょうどで上限回数が増える")]
      [TestCase(10, 3)]
      [TestCase(20, 4)]
      public void MaxTurns_ByIntimacy(int intimacy, int expected) { /* ... */ }
  }
  ```

  「正常に動くこと」「〜のテスト」のような、何が守られているか分からない説明は書かない
- PlayMode テスト・実機確認が必要なもの（見た目、操作感、WebGL での日本語入力、スマホ表示）は
  自動化せず、PR に手動確認の手順を書く
- 特に注意する観点（test-designer / test-auditor はこれも一周する）
  - 時刻依存：会話回数のリセット（1 時間ごと）、日付の切り替わり、PlayFab のサーバー時刻と端末時刻のズレ
  - 親密度に応じた会話回数の上限（しきい値ちょうど・上限到達時）
  - セーブデータ：`PlayerData` にフィールドを足したとき、古いセーブデータを読めるか
  - シーン遷移の途中・会話中の連打・通信待ちの間の操作
  - LLM・PlayFab の失敗時に進行状態（りんごの数、インベントリ、会話回数）が壊れないこと

## 検証コマンド

PR 前の検証はこれだけを実行すればよい（`.claude/harness.json` の `checks` が走る）。

```powershell
.\.claude\scripts\run-checks.ps1
```

- 中身は `.claude/scripts/checks/unity-tests.ps1 -Platform EditMode`。Unity を batchmode で起動し、
  スクリプトのコンパイルと EditMode テストを行う
- **Unity エディタでこのプロジェクトを開いたままだと実行できない。** エディタを閉じるよう利用者に依頼する
  （エディタを勝手に終了させない）
- 数分かかるので、バックグラウンドで実行する
- Unity のインストール先が既定（`C:\Program Files\Unity\Hub\Editor\<バージョン>\`）と違う場合は
  環境変数 `UNITY_EDITOR_PATH` に `Unity.exe` のパスを設定する
- 結果は `reports/test-report.md`（件数と失敗の詳細）と `reports/test-spec.md`（テスト仕様書：
  実行した全テストケースの説明・結果・スキップ理由）、Unity のログと結果 XML は `reports/logs/` に出る

エディタを開いたまま手早くコンパイルやテストを確かめたいときは、Unity MCP が使えればそれを使ってよい
（ただし PR 作成ゲートの記録は `run-checks.ps1` でしか作られない）。

## ローカルでの起動

1. Unity Hub からこのプロジェクトを開く（または
   `& "C:\Program Files\Unity\Hub\Editor\6000.0.32f1\Editor\Unity.exe" -projectPath .`）
2. `Assets/Scenes/CommonUIScene.unity` を開いて Play する
   （`BootStrap` が常駐の共通 UI を読み込んでから TitleScene に移る。TitleScene から直接 Play すると
   共通 UI が読み込まれないので、動作確認には使わない）
3. 確認項目: タイトル表示 → ユーザー名でログイン → 村に移動できる → ともハムと会話できる

LLM の会話はプロキシ（Cloudflare Workers）に接続できる環境でのみ動く。

## デプロイ

未定（WebGL ビルドの出力先と公開先が決まったら、ここに手順を書く）。
手順が書かれていない間は、`deploy` スキルは実行せずに利用者に確認する。

## レビューで特に見る点

- 追加したテストがテスト仕様書（`reports/test-spec.md`）に載っているか（載っていなければ実行されていない）
- 新規・移動・削除したアセットとスクリプトに `.meta` が対になっているか
- シーン・プレハブの差分に、意図しない変更（位置のずれ、参照の外れ、`m_` 系の大量の書き換え）が混ざっていないか
- `PlayerData` の変更が古いセーブデータと互換か
- WebGL で使えない API（スレッド、ファイル I/O）を使っていないか
- プロキシの URL 以外に、API キーやトークンが混ざっていないか
- 会話回数・親密度・りんごの数など、ゲームの進行に関わる値の計算が変わっていないか
