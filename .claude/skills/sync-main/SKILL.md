---
name: sync-main
description: origin と同期し、オープン中の PR、マージ済みで消せるローカルブランチ、現在のブランチが main からどれだけ遅れているかを一望する。作業を始める前と、PR を出したあと承認を待つ間に使う。「同期して」「PR の状況は」「main を取り込んで」と言われたときに使う。
---

# origin と同期して状況を把握する

PR がいつ承認されるかは分からない。知らないうちに `origin/main` が進み、
自分のブランチの土台が古くなる。このスキルはその状況を一望して整えるためのもの。

既定ブランチは `.claude/harness.json` の `defaultBranch`（通常 `main`）。以下では `main` と書く。

## 手順

### 1. 現状を確認する

```powershell
git fetch origin --prune
git status --short --branch
git branch -vv
```

### 2. オープン中の PR を確認する

```powershell
gh pr list --state open --json number,title,headRefName,isDraft,mergeable,statusCheckRollup
```

`gh` が未認証なら `gh auth login` をユーザーに依頼して、ここで止まる。

### 3. マージ済みのローカルブランチを整理する

```powershell
git branch --merged origin/main --format "%(refname:short)"
```

保護ブランチと現在のブランチを除いた結果を**ユーザーに提示してから**削除する。
勝手に消さない。

```powershell
git branch -d <ブランチ名>
```

### 4. 現在のブランチと origin/main のズレを見る

```powershell
git rev-list --left-right --count HEAD...origin/main
```

`main` にいる場合は `git pull --ff-only` で取り込む。

作業ブランチにいて `origin/main` が進んでいる場合は、**取り込むかどうかをユーザーに確認する**。

- 衝突しそうなファイルに触っているなら、早めに rebase したほうがよい
- まだ小さな変更なら、PR を出してからでも間に合う

取り込む場合:

```powershell
git rebase origin/main
```

作業ツリーが汚れていたら先に `git stash push -u` する。rebase 後に `git stash pop`。

### 5. 報告する

次の形でまとめる。

```markdown
## 現在地
ブランチ `xxx`（origin/main より 3 コミット遅れ / 2 コミット進み）
未コミットの変更: なし

## マージ待ちの PR
- #12 〇〇を実装する (xxx) — マージ可能、チェック通過

## 整理できるブランチ
- yyy（origin/main にマージ済み）

## 推奨する次の一手
（rebase すべきか、そのまま進めてよいか）
```

## 注意

- ブランチの削除と rebase は、必ずユーザーに確認してから実行する
- `git pull` を作業ブランチで無条件に打たない。履歴が分岐しているとマージコミットができてしまう
- rebase で衝突したファイルが、手で統合できない形式（バイナリや、エディタが生成する
  シリアライズ済みのファイル）なら、自分で解決せずにユーザーに相談する（`AGENTS.md` 参照）
