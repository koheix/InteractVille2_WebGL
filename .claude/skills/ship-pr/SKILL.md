---
name: ship-pr
description: 現在のブランチを検証して、結果のレポートを本文に埋め込んだ PR を作る。レビュー時に検証結果が必ず目に入るようにするための必須の手順。「PR を作って」「PR を出して」と言われたときに使う。
---

# 検証結果つきの PR を作る

このリポジトリでは「変更ごとの検証結果を、レビュー時に見られること」を必須にしている。
検証結果の載っていない PR は未完成として扱う。

記録とレポートの置き場所（`<reportsDir>`）は `.claude/harness.json` の `reportsDir`（既定 `reports`）。

## 1. ブランチを確認する

```powershell
git status --short --branch
git log --oneline origin/main..HEAD
```

- 保護ブランチ（`main` / `master` など）にいるなら、ここで止めてユーザーに知らせる（PR にできない）
- 未コミットの変更があるなら、コミットするか退避するかをユーザーに確認する
- コミットが 1 つも無いなら、PR にするものが無い

## 2. origin と同期する

```powershell
git fetch origin --prune
git rev-list --left-right --count HEAD...origin/main
```

`origin/main` が大きく進んでいるなら、rebase するかをユーザーに確認する。

## 3. 検証を実行する

```powershell
.\.claude\scripts\run-checks.ps1
```

**失敗が 1 件でもあるなら、PR を作らずに報告する。**
「落ちているが PR は出す」場合は、ユーザーが明示的にそう言ったときだけ。
その場合でもゲートは通らないので、利用者自身に Claude Code の外で `gh pr create` してもらう。

次の 3 つが生成される。

| ファイル | 中身 |
|---|---|
| `<reportsDir>/test-report.md` | 件数と失敗の詳細 |
| `<reportsDir>/test-spec.md` | **テスト仕様書**。実行した全テストケースについて、何を保証するか・結果・スキップ理由 |
| `<reportsDir>/verification.json` | `guard-gh.ps1` が読む記録。いまのコミットで全緑でなければ `gh pr create` が通らない |

`test-spec.md` の冒頭に「説明が書かれていないテストケースが N 件」と出ていたら、PR の前に
テストへ説明を足す（書き方は `AGENTS.md` の「テスト方針」）。
これらのファイルは `guard-records.ps1` が保護しており、`run-checks.ps1` 以外は書き換えられない。
中身を PR 本文に使うときは、読み取ってから一時ファイルに写す。

## 3.5. レビューを済ませて記録する

`pr-reviewer`（差分のレビュー）と、テストを書いたなら `test-auditor`（テストの監査）を
実行する。

**記録はコミットの SHA に紐づく。** 指摘を直したら、必ずこの順でやり直す。

1. 直す
2. **コミットする**
3. `.\.claude\scripts\run-checks.ps1`（`verification.json` が新しい HEAD で作られる）
4. レビューをやり直して記録する

順番を間違えて「記録してからコミット」にすると、記録が古いコミットのものになり、
PR 作成が拒否される。理由が分かりにくいので気をつける。

```powershell
.\.claude\scripts\record-review.ps1 -Reviewers test-auditor,pr-reviewer -Verdict pass
```

**この記録が無いと `gh pr create` は拒否される**（`harness.json` の `prGate.requireReview` が true のとき）。

## 4. push する

```powershell
git push -u origin <ブランチ名>
```

## 5. PR 本文を組み立てる

`<reportsDir>/test-report.md` と `<reportsDir>/test-spec.md` の内容を読み、次の構成で本文を作る。

```markdown
## 何をしたか
（1〜3 行。何を解決したのか）

## 変更の内容
- `path/to/file` — 何をどう変えたか
- `path/to/test` — 何を検証しているか

## なぜこうしたか
（設計上の判断と、他の選択肢を採らなかった理由。自明なら省略）

## 検証結果

| 項目 | 件数 |
|---|---|
| 成功 | 42 |
| 失敗 | 0 |
| スキップ | 3 |
| 所要時間 | 12.4 秒 |

スキップの内訳: （理由）

<details>
<summary>検証の詳細</summary>

（test-report.md の本体をそのまま貼る）

</details>

## テスト仕様書

実行したテストケース: N 件（うち今回追加・変更したもの: M 件）

<details>
<summary>テストケースの一覧（何を保証しているか）</summary>

（test-spec.md の本体をそのまま貼る。見出しは 1 段下げてよい）

</details>

## 手動で確認してほしいこと
- （自動テストで確認できない見た目・操作感など。手順つきで）

## レビューで見てほしいところ
- （判断が必要だった箇所、設計の選択）

## やっていないこと
- （意図的に範囲外にしたもの）

🤖 Generated with [Claude Code](https://claude.com/claude-code)
```

**検証結果の表は `<details>` の外に置く。** 折りたたむと見られない。

テスト仕様書は要約・並べ替え・抜粋をせずに貼る（人間が「どのテストが実行されたか」を
確かめるための一次資料なので、手を加えると意味がなくなる）。
PR 本文が GitHub の上限（約 65,000 文字）を超えそうなら、本文には件数と
「テスト仕様書はコメントに記載」とだけ書き、PR を作ったあとに
`gh pr comment <番号> --body-file <一時ファイル>` で仕様書を投稿する（長ければ複数回に分ける）。

## 6. PR を作る

本文は一時ファイル経由で渡す（改行と日本語が壊れないように）。

```powershell
gh pr create --title "<日本語のタイトル>" --body-file <一時ファイル>
```

一時ファイルは `$env:TEMP` に置き、作成後に消す。リポジトリに残さない。

## 7. 報告して、止まる

PR の URL と検証結果の要約をユーザーに伝える。**ここで終わり。** マージ・デプロイへ
進まない。次に何をするかは人間が決める。

## 守ること

- **PR をマージしない。** 承認とマージは人間がやる（`guard-gh.ps1` が機械的に止める）
- 検証を実行せずに PR を作らない
- 検証結果を「全部通りました」と要約だけ書かない。件数と内訳を載せる
- スキップされたテストがあれば、その理由を必ず書く
- テスト仕様書を載せずに PR を作らない
