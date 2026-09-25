# Unity プロジェクトの「専用の作業コピー」（git worktree）を扱う共通処理。
#
# Unity は 1 つのプロジェクトを同時に 1 つのプロセスでしか開けない（Temp\UnityLockfile を排他ロックする）。
# 利用者がエディタで開いているプロジェクトを、検証やビルドのために batchmode でもう一度開くことはできない。
# そこで、同じリポジトリの別の作業コピー（Library も別）を用意し、batchmode はそちらで動かす。
#
#   - 作業コピーはリポジトリの外に置き、指定したコミットに合わせる（detached HEAD）
#   - コミットされていないファイルは消す（Library\ は残す。作り直すと全アセットのインポートで時間がかかる）
#   - 検証・ビルドされるのはコミット済みの内容だけになる（作業中の未コミットの変更は混ざらない）
#
# 強制的な checkout と clean -fdx は取り返しがつかないので、このスクリプトが作った作業コピーにだけ行う。
# 作るときに `git worktree lock --reason <印>` を付け、同期のたびに「印がある」「detached HEAD である」を確かめる。
# 利用者が普段使っている作業コピーを、パスの打ち間違いで指しても触らない。
#
# 作り直すとき: git worktree unlock <パス>; git worktree remove --force <パス>
#
# 呼び出し側は、先に hooks\lib\Common.ps1 を dot-source しておくこと（Get-GitExe / Invoke-Git を使う）。

Set-StrictMode -Version Latest

$script:UnityWorktreeMarker = 'harness-unity-worktree'

# Unity がそのプロジェクトを開いているか（Temp\UnityLockfile が排他ロックされているか）。
# ファイルが残っているだけ（異常終了の跡）なら開けるので、実際に開けるかで判定する。
function Test-UnityProjectLocked {
    param([Parameter(Mandatory)][string]$ProjectPath)

    $lockFile = Join-Path $ProjectPath 'Temp\UnityLockfile'
    if (-not (Test-Path -LiteralPath $lockFile)) { return $false }
    try {
        $stream = [System.IO.File]::Open($lockFile, 'Open', 'ReadWrite', 'None')
        $stream.Dispose()
        return $false
    } catch {
        return $true
    }
}

# 相対パスはリポジトリのルートから解決する（harness.json にリポジトリの外の場所を相対で書けるように）。
function Resolve-UnityWorktreePath {
    param([Parameter(Mandatory)][string]$RepoRoot, [Parameter(Mandatory)][string]$Path)

    if (-not [System.IO.Path]::IsPathRooted($Path)) { $Path = Join-Path $RepoRoot $Path }
    return [System.IO.Path]::GetFullPath($Path).TrimEnd('\')
}

# git worktree list --porcelain を、作業コピーごとの情報に分ける。
function Get-WorktreeEntries {
    param([Parameter(Mandatory)][string]$GitExe, [Parameter(Mandatory)][string]$RepoRoot)

    $list = Invoke-Git -GitExe $GitExe -RepoRoot $RepoRoot -GitArgs @('worktree', 'list', '--porcelain')
    if (-not $list.Ok) { throw "作業コピーの一覧を取得できませんでした: $($list.StdErr)" }

    $entries = New-Object System.Collections.Generic.List[object]
    $current = $null
    foreach ($line in ($list.StdOut -split '\r?\n')) {
        if ($line -like 'worktree *') {
            $current = [pscustomobject]@{
                Path     = [System.IO.Path]::GetFullPath(($line.Substring(9) -replace '/', '\')).TrimEnd('\')
                Detached = $false
                Locked   = $false
                Reason   = ''
            }
            $entries.Add($current)
        } elseif ($null -ne $current -and $line -eq 'detached') {
            $current.Detached = $true
        } elseif ($null -ne $current -and ($line -eq 'locked' -or $line -like 'locked *')) {
            $current.Locked = $true
            if ($line.Length -gt 7) { $current.Reason = $line.Substring(7).Trim() }
        }
    }
    return $entries.ToArray()
}

# 専用の作業コピーを、指定したコミットに合わせる。無ければ作る。
# 失敗したら理由を書いた例外を投げる（呼び出し側で検証の失敗として扱う）。
function Sync-UnityWorktree {
    param(
        [Parameter(Mandatory)][string]$GitExe,
        [Parameter(Mandatory)][string]$RepoRoot,
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$Commit
    )

    $Path = Resolve-UnityWorktreePath -RepoRoot $RepoRoot -Path $Path
    if ($Path -eq [System.IO.Path]::GetFullPath($RepoRoot).TrimEnd('\')) {
        throw '専用の作業コピーに、リポジトリ本体と同じ場所は指定できません。'
    }

    $resolved = Invoke-Git -GitExe $GitExe -RepoRoot $RepoRoot -GitArgs @('rev-parse', '--verify', "$Commit^{commit}")
    if (-not $resolved.Ok) { throw "コミット '$Commit' が見つかりません。$($resolved.StdErr)" }
    $sha = $resolved.StdOut.Trim()

    # 実体が消えた作業コピーの登録を片付けてから判定する（印の付いたものは lock されているので prune では消えない）。
    Invoke-Git -GitExe $GitExe -RepoRoot $RepoRoot -GitArgs @('worktree', 'prune') | Out-Null
    $entry = Get-WorktreeEntries -GitExe $GitExe -RepoRoot $RepoRoot | Where-Object { $_.Path -eq $Path } | Select-Object -First 1

    if ($null -ne $entry -and $entry.Reason -ne $script:UnityWorktreeMarker) {
        throw "$Path はこのリポジトリの作業コピーですが、このスクリプトが作った専用の作業コピーではありません（印 '$($script:UnityWorktreeMarker)' が無い）。未コミットの変更を消さないよう、何もしません。-Worktree の指定を確かめてください。"
    }

    # 印はあるが実体が消えている（利用者がフォルダを消した）なら、登録を外して作り直す。
    if ($null -ne $entry -and -not (Test-Path -LiteralPath $Path)) {
        Invoke-Git -GitExe $GitExe -RepoRoot $RepoRoot -GitArgs @('worktree', 'unlock', $Path) | Out-Null
        Invoke-Git -GitExe $GitExe -RepoRoot $RepoRoot -GitArgs @('worktree', 'prune') | Out-Null
        $entry = $null
    }

    if ($null -eq $entry) {
        if (Test-Path -LiteralPath $Path) {
            throw "$Path は既にありますが、このリポジトリの作業コピーではありません。別の場所を指定するか、中身を確かめてから消してください。"
        }
        $add = Invoke-Git -GitExe $GitExe -RepoRoot $RepoRoot -GitArgs @('worktree', 'add', '--detach', $Path, $sha)
        if (-not $add.Ok) { throw "専用の作業コピーを作れませんでした: $($add.StdErr)" }
        $lock = Invoke-Git -GitExe $GitExe -RepoRoot $RepoRoot -GitArgs @('worktree', 'lock', '--reason', $script:UnityWorktreeMarker, $Path)
        if (-not $lock.Ok) { throw "専用の作業コピーに印を付けられませんでした: $($lock.StdErr)" }
    } else {
        if (-not $entry.Detached) {
            throw "専用の作業コピー（$Path）がブランチを checkout しています（detached HEAD ではない）。誰かが作業に使っている可能性があるので、何もしません。"
        }
        if (Test-UnityProjectLocked -ProjectPath $Path) {
            throw "専用の作業コピー（$Path）は別の Unity で使用中です（別の検証やビルドが実行中）。終わってから再実行してください。"
        }
        $checkout = Invoke-Git -GitExe $GitExe -RepoRoot $Path -GitArgs @('checkout', '--detach', '--force', $sha)
        if (-not $checkout.Ok) { throw "専用の作業コピーをコミット $sha に合わせられませんでした: $($checkout.StdErr)" }
        # コミットされていないファイルを消す。Library\ は残す（消すと全アセットのインポートからやり直しになる）。
        $clean = Invoke-Git -GitExe $GitExe -RepoRoot $Path -GitArgs @('clean', '-fdx', '-e', '/Library/')
        if (-not $clean.Ok) { throw "専用の作業コピーの掃除に失敗しました: $($clean.StdErr)" }
    }

    $head = Invoke-Git -GitExe $GitExe -RepoRoot $Path -GitArgs @('rev-parse', 'HEAD')
    if (-not $head.Ok -or $head.StdOut.Trim() -ne $sha) {
        throw "専用の作業コピーが $sha になっていません（$($head.StdOut)）。"
    }

    return [pscustomobject]@{ Path = $Path; Commit = $sha }
}
