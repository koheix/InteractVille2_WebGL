# 専用の作業コピー（git worktree）で Unity を batchmode で実行する。
# エディタでプロジェクトを開いたままでも、ビルドなどを実行できる。
#
#   # 例：origin/main をビルドする（ビルド用のメソッドはプロジェクトごとに用意する）
#   .\.claude\scripts\unity-worktree-run.ps1 -Worktree ..\<プロジェクト名>.ci -Commit origin/main `
#       -LogFile <ログのパス> -UnityArgs '-buildTarget', 'WebGL', '-executeMethod', '<クラス>.<メソッド>', '-nographics'
#
#   # 例：インポートだけ済ませておく（初回の準備。全アセットのインポートに時間がかかる）
#   .\.claude\scripts\unity-worktree-run.ps1 -Worktree ..\<プロジェクト名>.ci -LogFile <ログのパス> -UnityArgs '-quit', '-nographics'
#
# -Commit を省くと、リポジトリ本体の HEAD（コミット済みの内容）を使う。
# 作業コピーは指定したコミットに合わせ、コミットされていないファイルは消す（Library\ は残す）。
# Unity プロジェクトがリポジトリのサブフォルダにあるときは -ProjectSubPath に指定する。
# -UnityArgs の各値は空白で連結して Unity に渡すので、空白を含む値は使えない（パスは空白の無い場所を使う）。
# 終了コードは Unity の終了コード（作業コピーの準備に失敗したら 3、時間切れなら 4）。

param(
    [Parameter(Mandatory)][string]$Worktree,
    [string]$Commit = 'HEAD',
    [Parameter(Mandatory)][string]$LogFile,
    [string[]]$UnityArgs = @(),
    [string]$ProjectSubPath = '',
    [string]$UnityPath = $env:UNITY_EDITOR_PATH,
    [int]$TimeoutMinutes = 90
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot '..\hooks\lib\Common.ps1')
. (Join-Path $PSScriptRoot 'lib\UnityWorktree.ps1')

$gitExe = Get-GitExe
if (-not $gitExe) { Write-Host '✘ git が見つかりません。'; exit 3 }
$repoRoot = Get-RepoRoot -GitExe $gitExe -Directory (Join-Path $PSScriptRoot '..\..')
if (-not $repoRoot) { Write-Host '✘ git リポジトリの中で実行してください。'; exit 3 }

# 注意: PowerShell の変数名は大文字と小文字を区別しない。引数の $Worktree（文字列）と同じ名前で
# 受けると結果が文字列に変換されてしまうので、別の名前（$synced）で受ける。
try {
    $synced = Sync-UnityWorktree -GitExe $gitExe -RepoRoot $repoRoot -Path $Worktree -Commit $Commit
} catch {
    Write-Host "✘ $($_.Exception.Message)"
    exit 3
}
Write-Host "専用の作業コピー: $($synced.Path)（コミット $($synced.Commit.Substring(0, 7))）"

$projectPath = if ($ProjectSubPath) { Join-Path $synced.Path $ProjectSubPath } else { $synced.Path }
$versionFile = Join-Path $projectPath 'ProjectSettings\ProjectVersion.txt'
if (-not (Test-Path -LiteralPath $versionFile)) {
    Write-Host "✘ Unity プロジェクトではありません（$versionFile がありません）。-ProjectSubPath を確かめてください。"
    exit 3
}
$version = $null
foreach ($line in (Get-Content -LiteralPath $versionFile -Encoding UTF8)) {
    if ($line -match '^m_EditorVersion:\s*(\S+)') { $version = $Matches[1]; break }
}
if ([string]::IsNullOrWhiteSpace($UnityPath)) {
    $UnityPath = Join-Path $env:ProgramFiles "Unity\Hub\Editor\$version\Editor\Unity.exe"
}
if (-not (Test-Path -LiteralPath $UnityPath)) {
    Write-Host "✘ Unity $version のエディタが見つかりません: $UnityPath"
    exit 3
}

$LogFile = [System.IO.Path]::GetFullPath($LogFile)
New-Item -ItemType Directory -Force -Path (Split-Path $LogFile) | Out-Null
$arguments = @('-batchmode', '-projectPath', "`"$projectPath`"", '-logFile', "`"$LogFile`"") + $UnityArgs

Write-Host "Unity $version を実行します: $($UnityArgs -join ' ')"
$started = Get-Date
$process = Start-Process -FilePath $UnityPath -ArgumentList $arguments -PassThru -NoNewWindow
$null = $process.Handle
if (-not $process.WaitForExit($TimeoutMinutes * 60 * 1000)) {
    & taskkill.exe /PID $process.Id /T /F 2>$null | Out-Null
    Write-Host "✘ Unity が $TimeoutMinutes 分以内に終わらなかったため打ち切りました。ログ: $LogFile"
    exit 4
}
$process.WaitForExit()
$minutes = [math]::Round(((Get-Date) - $started).TotalMinutes, 1)
Write-Host "終了コード $($process.ExitCode)（$minutes 分）。ログ: $LogFile"
exit $process.ExitCode
