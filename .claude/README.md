# .claude — 開発ハーネス

Claude Code が「ブランチを切る → テスト設計 → 実装 → 監査 → 検証 → レビュー → PR」を
工程を飛ばさずに進めるための仕組み。**このディレクトリはプロジェクトに依存しない。**
プロジェクト固有のことは次の 2 か所にだけ書く。

| ファイル | 書くこと |
|---|---|
| `.claude/harness.json` | フック・スクリプトが機械的に使う値（保護ブランチ、検証コマンド、レポート置き場など） |
| リポジトリ直下の `AGENTS.md` | エージェントが読む規約（構成、技術スタック、規約、テスト方針、起動・デプロイ手順、レビュー観点） |

`CLAUDE.md` は `@AGENTS.md` の 1 行だけにして、Claude Code に `AGENTS.md` を読ませる。

## 中身

```
.claude/
  settings.json          フックの登録と、読み取り系コマンドの許可（プロジェクト非依存）
  harness.json           ★ プロジェクト固有の設定
  hooks/
    session-start.ps1    開始時に origin・PR・Issue の状況を知らせる
    pre-edit-sync.ps1    編集前に origin と同期。保護ブランチでの編集を拒否
    guard-shell.ps1      保護ブランチへの commit / push、強制 push、保護ブランチの削除を拒否
    guard-gh.ps1         PR のマージ・承認、Issue のクローズ、gh api の書き込みを拒否。
                         gh pr create は検証・レビューの記録が HEAD と一致しなければ拒否
    guard-records.ps1    検証・レビューの記録とレポート・テスト仕様書を AI が直接書き換えることを拒否
    post-edit-check.ps1  編集直後の構文チェック（.ps1 は常時、それ以外は harness.json の postEditChecks）
    lib/Common.ps1       共通処理（コマンド解析、harness.json の読み込みなど）
  scripts/
    run-checks.ps1       harness.json の checks を実行し、verification.json・test-report.md・
                         test-spec.md（テスト仕様書）を作る
    record-review.ps1    レビュー結果を HEAD に紐づけて review-record.json に記録する
    checks/
      unity-tests.ps1    Unity Test Runner を batchmode で実行するアダプタ（Unity プロジェクト用）
      vitest-tests.ps1   vitest を実行し、テストケースを仕様書用の cases に変換するアダプタ（Node プロジェクト用）
      lib/NUnitResults.ps1  NUnit 3 形式の結果 XML を件数とテストケース一覧に変換する
  agents/                test-designer / test-auditor / test-runner / pr-reviewer / issue-developer
  skills/                feature / github-issue-development / ship-pr / sync-main / dev / deploy
  state/                 セッションローカルな状態（自身の .gitignore で git から除外）
```

## harness.json

```jsonc
{
  "defaultBranch": "main",                    // PR の土台。origin/<これ> と比較する
  "protectedBranches": ["main", "master"],    // 直接の編集・commit・push・削除を拒否する
  "branchExample": "feature/<内容>",           // 拒否メッセージで案内するブランチ名の例
  "syncThrottleSeconds": 600,                 // 編集前の fetch を間引く間隔
  "reportsDir": "reports",                    // 記録とレポートの置き場所（.gitignore に入れる）
  "prGate": {
    "requireVerification": true,              // gh pr create に検証の記録（全緑）を必須にする
    "requireReview": true                     // gh pr create にレビューの記録（pass）を必須にする
  },
  "checks": [                                 // run-checks.ps1 が順に実行する検証
    { "name": "単体テスト", "command": "npm test", "timeoutMinutes": 10 }
  ],
  "postEditChecks": [                         // 編集直後に走らせるチェック（{file} は編集したファイル）
    { "name": "型チェック", "extensions": [".ts", ".tsx"], "command": "npx tsc -b --pretty false" }
  ]
}
```

- 無い項目は既定値になる。ファイルが壊れていてもゲートは「有効」側に倒れる
- `prGate` を無効にできるのは JSON の `false` を明示したときだけ
- `harness.json`・フック・スクリプトを編集すると「ガード設定の変更」として通知される。PR で理由を説明すること

### checks の書き方

- `command` はリポジトリのルートで PowerShell として実行される。終了コード 0 が成功
- 件数を集計に載せたいときは、環境変数 `HARNESS_CHECK_RESULT` のパスに次の JSON を書く
  （書かなければ終了コードで「成功 1 / 失敗 1」と数える。件数と終了コードが食い違えば終了コードを優先）

  ```jsonc
  {
    "passed": 3, "failed": 1, "skipped": 1, "summary": "...",
    "failures": [ { "name": "Suite.Case", "message": "..." } ],
    "cases": [                                  // 任意。書くとテスト仕様書に 1 件ずつ載る
      {
        "suite": "SpeakTurnsTests",             // 見出しのまとまり（クラス・ファイルなど）
        "suiteDescription": "会話回数の計算",    // 任意
        "name": "RemainingTurns_ResetsAfterOneHour",
        "description": "1時間ちょうど経つと会話回数が回復する",  // 何を保証するか
        "outcome": "Passed",                    // Passed / Failed / Skipped
        "durationSeconds": 0.012,
        "message": "",                          // 失敗内容・スキップ理由
        "categories": ["時刻"]                  // 任意
      }
    ]
  }
  ```
- 追加の出力ファイルは環境変数 `HARNESS_CHECK_OUTPUT_DIR`（`<reportsDir>/logs`）に置く
- `checks` が空だと run-checks は失敗扱いになる（何も検証していない記録でゲートを通さないため）

プロジェクト種別ごとの例:

| 種別 | checks の例 |
|---|---|
| Unity | `.\.claude\scripts\checks\unity-tests.ps1 -Platform EditMode`（エディタを閉じて実行） |
| Node / TypeScript | `.\.claude\scripts\checks\vitest-tests.ps1 -Path <package.json のあるディレクトリ>`（仕様書に載る）、`npm run typecheck`、`npm run lint` |
| Python | `uv run pytest`、`uv run ruff check .` |
| .NET | `dotnet test` |

### テスト仕様書（`<reportsDir>/test-spec.md`）

`run-checks.ps1` を実行するたびに、実行した全テストケースを「何を保証するか・結果・スキップ理由」の
表にまとめて出力する。人間が「どのテストケースがテストされたか」を確かめるための文書で、
`ship-pr` スキルが PR 本文に載せる。

- 載るのは `cases` を報告した検証だけ。報告しない検証は「一覧なし」と表示される
- 説明（`description`）が無いケースは件数が冒頭に警告として出る。`test-auditor` は説明の無いテストを不合格にする
- NUnit 3 形式の XML を出すツール（Unity Test Runner、dotnet test の NUnit ロガー）は
  `checks/lib/NUnitResults.ps1` の `ConvertFrom-NUnitResults` で `cases` を作れる。説明は `[Description]` から取る
- 他の形式（JUnit XML など）を使うプロジェクトでは、同様の変換を `checks/` に足す

## 別のプロジェクトへ移植する手順

1. `.claude/` をコピーする（`state/` は除く）
2. `.claude/harness.json` をそのプロジェクトに合わせて書き換える（特に `checks`・`branchExample`）
3. リポジトリ直下に `AGENTS.md` を書く。エージェントとスキルが参照する節は次のとおり
   - 概要 / 技術スタック / 構成
   - ブランチとコミット（ブランチ名・コミットメッセージの書式）
   - 規約
   - テスト方針（テストの置き場所、特に注意する観点）
   - 検証コマンド
   - ローカルでの起動（`dev` スキル）
   - デプロイ（`deploy` スキル。未定なら「未定」と書く）
   - レビューで特に見る点（`pr-reviewer`）
4. `CLAUDE.md` に `@AGENTS.md` と書く
5. `.gitignore` に `reportsDir`（既定 `/reports/`）を追加する
6. `.\.claude\scripts\run-checks.ps1` を一度実行して、検証が通ることを確かめる

## 注意

- フックは Windows PowerShell 5.1 で動く。日本語を含む `.ps1` は **UTF-8 BOM 付き**で保存すること
  （BOM が無いと文字化けして構文エラーになる。`post-edit-check.ps1` が自動で付け直すが、
  `post-edit-check.ps1` 自身や `lib/Common.ps1` を壊すと付け直しも動かなくなる）
- ガードはコマンド文字列を解析して判定する。コマンドの説明文に禁止操作の文字列を
  そのまま書くと止められることがある（引数の中身は検査対象）
