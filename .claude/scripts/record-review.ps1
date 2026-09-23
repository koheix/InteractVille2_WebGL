# レビューの結果を、いまの HEAD に紐づけて記録する。
#
#   .\.claude\scripts\record-review.ps1 -Reviewers test-auditor,pr-reviewer -Verdict pass
#
# <reportsDir>/review-record.json に書く。guard-gh.ps1 は gh pr create のときに
# 「記録のコミットが HEAD と一致し、verdict が pass であること」を確認する。
#
# 記録はコミットに紐づくので、指摘を直したら「直す → コミット → 検証 → レビュー → 記録」の
# 順でやり直すこと。記録してからコミットすると、記録が古いコミットのものになる。

param(
    [Parameter(Mandatory)][string[]]$Reviewers,
    [Parameter(Mandatory)][ValidateSet('pass', 'changes-requested')][string]$Verdict,
    [string]$Notes = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot '..\hooks\lib\Common.ps1')

$gitExe = Get-GitExe
if (-not $gitExe) { Write-Error 'git が見つかりません。'; exit 2 }

$repoRoot = Get-RepoRoot -GitExe $gitExe -Directory (Join-Path $PSScriptRoot '..\..')
if (-not $repoRoot) { Write-Error 'git リポジトリの中で実行してください。'; exit 2 }

$head = Invoke-Git -GitExe $gitExe -RepoRoot $repoRoot -GitArgs @('rev-parse', 'HEAD')
if (-not $head.Ok) { Write-Error 'HEAD を特定できません。'; exit 2 }

# -Reviewers a,b をまとめて 1 要素で渡されても分けて記録する。
$names = @($Reviewers | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })

$status = Invoke-Git -GitExe $gitExe -RepoRoot $repoRoot -GitArgs @('status', '--porcelain')
if ($status.Ok -and $status.StdOut) {
    Write-Warning '未コミットの変更があります。記録は HEAD（コミット済みの内容）に紐づきます。レビューした差分がコミット済みか確認してください。'
}

$config = Get-HarnessConfig $repoRoot
$reportsDir = Join-Path $repoRoot ((Get-ReportsDir $config) -replace '/', '\')
New-Item -ItemType Directory -Force -Path $reportsDir | Out-Null

$record = [ordered]@{
    commit     = $head.StdOut.Trim()
    branch     = (Get-CurrentBranch -GitExe $gitExe -RepoRoot $repoRoot)
    reviewers  = $names
    verdict    = $Verdict
    notes      = $Notes
    recordedAt = (Get-Date).ToString('o')
}

$path = Join-Path $reportsDir 'review-record.json'
[System.IO.File]::WriteAllText($path, (ConvertTo-Json -InputObject $record -Depth 4),
    (New-Object System.Text.UTF8Encoding($false)))

Write-Host "レビューを記録しました: $Verdict（$($names -join ', ')）→ $path"
