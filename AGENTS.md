# AGENTS.md — InteractVille2 WebGL

AI エージェント（Claude Code など）がこのリポジトリで作業するときの規約と前提。
`.claude/` 配下のエージェント・スキルは汎用に書かれており、プロジェクト固有のことは
すべてこのファイルを参照する。**作業を始める前に必ず読むこと。**

## 概要

森に住むハムスターになって、友達の「ともハム」と会話したり、りんごを集めて
ショップで家具を買ったりする 2D ゲーム。ともハムとの会話は LLM（Cloudflare Workers AI）で生成し、
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
| LLM | プロキシ（`server/llm-proxy/`、Cloudflare Workers、TypeScript）経由で Workers AI を呼ぶ。会話・要約・気分は `@cf/openai/gpt-oss-120b`、スコア（親密度・感情価・覚醒度）は `@cf/meta/llama-3.3-70b-instruct-fp8-fast` の JSON Mode。設定 1 つで Claude API に切り戻せる。無料枠（1 日 10,000 Neurons、日本時間 9:00 に回復）で運用 |
| JSON | Newtonsoft.Json（`com.unity.nuget.newtonsoft-json`）と `JsonUtility` |
| テスト | Unity：Unity Test Framework の EditMode（`Assets/Tests/EditMode/`）。Worker：vitest（`server/llm-proxy/test/`、Node 22.12 以上） |

## 構成

```
Assets/
  Scenes/            CommonUIScene（常駐 UI）, TitleScene, MainGameScene（村）, HouseScene（ともハムの家）, ShopScene
  Scripts/
    Character/             プレイヤーの移動・入力・ステータス
    CommonUISceneScripts/  起動（BootStrap）、共通 UI、ローディング
    Inventory/             インベントリ
    ItemsScripts/          アイテム定義（ItemData）、拾えるアイテム、Tilemap 上の出現
    LLMControler/          LLM プロキシとの通信（LLMBridge）
      Core/                MonoBehaviour に依存しない判断ロジック（asmdef: InteractVille2.LLM）。
                           API の組み立て・応答の解釈（LlmApi）、失敗時の巻き戻し・会話回数の返却・
                           ステータスへの反映（ConversationRules）、メッセージ構造（Message）
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
  Tests/EditMode/          EditMode テスト（asmdef: InteractVille2.LLM.Tests）
server/llm-proxy/          LLM プロキシ（Cloudflare Workers）。公開 URL: https://llm-proxy.grapeoxygen.workers.dev
  src/                     POST /chat（会話）・POST /score（スコア）・POST /（旧 API）
  test/                    vitest
  contract/                Unity と共有する API の契約（入力の上限、リクエスト・応答の例）
  wrangler.jsonc           モデル名・プロバイダ（LLM_PROVIDER）などの設定
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
- Unity はプロバイダ（Workers AI / Claude）を意識しない。送るのは `{system, messages}` と `{prompt}`、
  受け取るのは `{text}` と `{result}` だけ。モデル名やトークン数は Worker 側の設定で決め、クライアントから送らない
- API の形を変えるときは `server/llm-proxy/contract/` を更新する（Unity と Worker の両方のテストが契約を読む）
- 失敗したとき（`LlmResult.IsSuccess` が false）は、会話回数を返却し、会話履歴に失敗した発言やエラー文言を残さない。
  スコア・記憶・気分は会話前の値を保つ（`StatusRules`）
- 消すときは「今回追加したもの」を指定する（`ConversationRules.RemoveExact`、`SpeakTurns.Refund`）。
  「最後の 1 件を消す」は、通信中に別の記録が増えていると別のものを消してしまう
- タイムアウト・エラー応答・壊れた JSON・空の応答で、会話 UI が固まったりセーブデータが壊れたりしないこと
- プロンプトに入れるユーザー入力は、そのまま埋め込んでよい形か確認する

### LLM プロキシ（`server/llm-proxy/`）
- 上流の失敗は種別（`daily_quota` / `busy` / `invalid_score` / `upstream` / `bad_request` / `config`）だけを返し、
  例外の文言や上流の応答本文は返さない（ログにだけ残す）
- どの応答にも CORS ヘッダを付ける（付け忘れるとブラウザが応答を捨て、Unity からは通信エラーにしか見えない）
- 秘密情報（`CLAUDE_API_KEY`）は `wrangler secret` で設定し、`wrangler.jsonc` やコードに書かない。`.dev.vars` はコミットしない

## テスト方針

- Unity のテストは Unity Test Framework の **EditMode** を基本とする
  - 置き場所: `Assets/Tests/EditMode/`（asmdef: `InteractVille2.LLM.Tests`）
  - 本体のスクリプトの多くは `Assembly-CSharp`（asmdef なし）にあり、テストから参照できない。
    テストしたい判断ロジックは、`MonoBehaviour` に依存しない形で asmdef 付きのフォルダ
    （LLM まわりなら `Assets/Scripts/LLMControler/Core/`）に置く。既存のスクリプトを別の asmdef に移すときは、
    参照関係が壊れないかを利用者と相談してから進める
- Worker のテストは vitest（`server/llm-proxy/test/`）。`env.AI` と Claude 用の `fetch` はフェイクに差し替える。
  テスト名（`it` の第 1 引数）を日本語で「何を保証するか」にする（テスト仕様書にそのまま載る）
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

- 中身は次の 3 つ
  1. LLM プロキシの vitest（`.claude/scripts/checks/vitest-tests.ps1 -Path server\llm-proxy`。`node_modules` が無ければ `npm ci` する）
  2. LLM プロキシの型チェック（`npm --prefix server/llm-proxy run typecheck`）
  3. Unity を batchmode で起動し、スクリプトのコンパイルと EditMode テスト（`.claude/scripts/checks/unity-tests.ps1 -Platform EditMode`）
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

LLM の会話は、デプロイ済みのプロキシ（https://llm-proxy.grapeoxygen.workers.dev）に接続できる環境でのみ動く。
Unity エディタからの呼び出しは Origin ヘッダが無いので、プロキシの Origin 制限には掛からない。

### LLM プロキシをローカルで動かす

```powershell
cd server\llm-proxy
npm ci
npx wrangler login     # 初回のみ。利用者に依頼する
npm run dev            # http://localhost:8787 。Workers AI は実物を呼ぶ（無料枠を消費する）
```

Unity エディタからローカルのプロキシを使うときは、環境変数 `IV2_LLM_PROXY_URL` を設定してから Unity を起動する
（例: `$env:IV2_LLM_PROXY_URL = "http://localhost:8787"`）。エディタでだけ有効で、ビルドには影響しない。
`LLMBridge.cs` の `PROXY_URL` は書き換えない。

## デプロイ

### LLM プロキシ（先にデプロイする）

新しい WebGL ビルドは新しい API（`/chat`・`/score`）を使うので、**ビルドを公開する前に Worker をデプロイする**。
Worker は旧 API（`POST /`）も残しているので、公開中の古いビルドはそのまま動く。

```powershell
cd server\llm-proxy
npm ci
npm test
npx wrangler deploy    # 利用者の Cloudflare アカウントに反映される。実行前に利用者に確認する
```

確認: `curl -X POST https://llm-proxy.grapeoxygen.workers.dev/chat -H "Content-Type: application/json" -d '{"system":"短く答えて","messages":[{"role":"user","content":"こんにちは"}]}'` が `{"text":...}` を返す。

- **Claude に切り戻す**: `wrangler.jsonc` の `LLM_PROVIDER` を `"claude"` にしてデプロイする
  （`CLAUDE_API_KEY` は secret として設定済み。未設定なら `npx wrangler secret put CLAUDE_API_KEY`）
- **モデルを変える**: `CHAT_MODEL`・`SCORE_MODEL` を変えてデプロイする。`SCORE_MODEL` は JSON Mode 対応モデルに限る
- **旧 API を止める**: 新しい WebGL ビルドを公開して動作を確認したら、`LEGACY_CLAUDE_PASSTHROUGH` を `"false"` にしてデプロイし、
  その後コード（`src/index.ts` の `legacyPassthrough`）を削除する

### WebGL ビルド

公開先は https://koheix.github.io 配下。ビルドと公開の手順は未記載（決まったらここに書く）。
手順が書かれていない間は、`deploy` スキルで WebGL の公開は行わず、利用者に確認する。

## レビューで特に見る点

- 追加したテストがテスト仕様書（`reports/test-spec.md`）に載っているか（載っていなければ実行されていない）
- 新規・移動・削除したアセットとスクリプトに `.meta` が対になっているか
- シーン・プレハブの差分に、意図しない変更（位置のずれ、参照の外れ、`m_` 系の大量の書き換え）が混ざっていないか
- `PlayerData` の変更が古いセーブデータと互換か
- WebGL で使えない API（スレッド、ファイル I/O）を使っていないか
- プロキシの URL 以外に、API キーやトークンが混ざっていないか
- Unity と Worker の API の形（`server/llm-proxy/contract/`）を片方だけ変えていないか
- LLM が失敗したときに、会話回数・会話履歴・ステータス・記憶が壊れないか
- 会話回数・親密度・りんごの数など、ゲームの進行に関わる値の計算が変わっていないか
