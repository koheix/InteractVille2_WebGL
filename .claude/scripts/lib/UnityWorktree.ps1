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
# 呼び出し側は、先に hooks\lib\Common.ps1 を dot-source しておくこと（Get-GitExe / Invoke-Git を使う）。

Set-StrictMode -Version Latest

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

    # 登録済みの作業コピーか（git worktree list に出てくるか）。
    $list = Invoke-Git -GitExe $GitExe -RepoRoot $RepoRoot -GitArgs @('worktree', 'list', '--porcelain')
    $registered = @($list.StdOut -split '\r?\n' |
            Where-Object { $_ -like 'worktree *' } |
            ForEach-Object { [System.IO.Path]::GetFullPath(($_.Substring(9) -replace '/', '\')).TrimEnd('\') })
    $isRegistered = $registered -contains $Path

    if (-not $isRegistered) {
        if (Test-Path -LiteralPath $Path) {
            throw "$Path は既にありますが、このリポジトリの作業コピーではありません。別の場所を指定するか、中身を確かめてから消してください。"
        }
        # 登録だけ残っていて実体が消えた作業コピーを片付けてから作る。
        Invoke-Git -GitExe $GitExe -RepoRoot $RepoRoot -GitArgs @('worktree', 'prune') | Out-Null
        $add = Invoke-Git -GitExe $GitExe -RepoRoot $RepoRoot -GitArgs @('worktree', 'add', '--detach', $Path, $sha)
        if (-not $add.Ok) { throw "専用の作業コピーを作れませんでした: $($add.StdErr)" }
    } else {
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
