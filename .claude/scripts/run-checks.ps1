# 検証をまとめて実行し、PR 作成ゲート用の記録とレビュー用のレポートを作る。
#
#   .\.claude\scripts\run-checks.ps1
#
# 実行する検証は .claude\harness.json の checks に書く。言語やビルドツールに依存しない
# ように、ここは「コマンドを順に実行して結果を集める」ことだけを受け持つ。
#
#   "checks": [
#     { "name": "単体テスト", "command": "npm test", "timeoutMinutes": 10 }
#   ]
#
# 各コマンドはリポジトリのルートで PowerShell として実行する。終了コード 0 が成功。
# 件数を報告したい検証は、環境変数 HARNESS_CHECK_RESULT が指すパスに次の JSON を書けば
# 集計に反映される（書かなければ「成功 1 / 失敗 1」として数える）。
#
#   { "passed": 10, "failed": 1, "skipped": 0, "summary": "...",
#     "failures": [ { "name": "Foo.Bar", "message": "..." } ],
#     "cases": [ { "suite": "Foo", "name": "Bar", "description": "何を保証するか",
#                  "outcome": "Passed|Failed|Skipped", "durationSeconds": 0.01, "message": "" } ] }
#
# cases（実行した全テストケース）を書いた検証は、テスト仕様書に 1 件ずつ載る。
# suiteDescription / fullName / categories も任意で書ける。
#
# 出力（<reportsDir> は harness.json の reportsDir、既定 reports）:
#
#   <reportsDir>/verification.json  guard-gh.ps1 が PR 作成時に読む記録（HEAD と失敗件数）
#   <reportsDir>/test-report.md     PR 本文に貼るレポート（件数と失敗の詳細）
#   <reportsDir>/test-spec.md       テスト仕様書（どのテストケースを実行し、何を保証し、結果がどうだったか）
#   <reportsDir>/logs/              各検証の出力
#
# 検証が 1 件も設定されていなければ失敗扱いにする。何も検証していない記録で
# ゲートを通させないため。

param(
    # 特定の検証だけを実行する（name の部分一致）。記録は作らない。
    [string]$Only
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot '..\hooks\lib\Common.ps1')

$gitExe = Get-GitExe
if (-not $gitExe) { Write-Error 'git が見つかりません。'; exit 2 }

$repoRoot = Get-RepoRoot -GitExe $gitExe -Directory (Join-Path $PSScriptRoot '..\..')
if (-not $repoRoot) { Write-Error 'git リポジトリの中で実行してください。'; exit 2 }

$config = Get-HarnessConfig $repoRoot
$reportsDir = Join-Path $repoRoot ((Get-ReportsDir $config) -replace '/', '\')
$logsDir = Join-Path $reportsDir 'logs'
New-Item -ItemType Directory -Force -Path $logsDir | Out-Null

$checks = @(Get-Prop $config 'checks' @())
if ($Only) { $checks = @($checks | Where-Object { ([string](Get-Prop $_ 'name' '')) -like "*$Only*" }) }

$head = (Invoke-Git -GitExe $gitExe -RepoRoot $repoRoot -GitArgs @('rev-parse', 'HEAD')).StdOut
$branch = Get-CurrentBranch -GitExe $gitExe -RepoRoot $repoRoot
$status = Invoke-Git -GitExe $gitExe -RepoRoot $repoRoot -GitArgs @('status', '--porcelain')
$dirtyFiles = @()
if ($status.Ok -and $status.StdOut) { $dirtyFiles = @($status.StdOut -split '\r?\n' | Where-Object { $_ }) }

function Invoke-Check {
    param([Parameter(Mandatory)]$Check, [Parameter(Mandatory)][int]$Index)

    $name = [string](Get-Prop $Check 'name' "check-$Index")
    $command = [string](Get-Prop $Check 'command' '')
    $timeoutMinutes = 30
    $parsed = 0
    if ([int]::TryParse([string](Get-Prop $Check 'timeoutMinutes' ''), [ref]$parsed) -and $parsed -gt 0) {
        $timeoutMinutes = $parsed
    }

    $logPath = Join-Path $logsDir ("check-{0}.log" -f $Index)
    $errPath = Join-Path $logsDir ("check-{0}.err.log" -f $Index)
    $resultPath = Join-Path $logsDir ("check-{0}.result.json" -f $Index)
    Remove-Item -LiteralPath $logPath, $errPath, $resultPath -Force -ErrorAction SilentlyContinue

    Write-Host "▶ $name"
    $started = Get-Date

    if ([string]::IsNullOrWhiteSpace($command)) {
        return [pscustomobject]@{
            name = $name; command = $command; exitCode = -1; timedOut = $false
            passed = 0; failed = 1; skipped = 0; durationSeconds = 0
            summary = 'command が空です（harness.json を確認してください）'; failures = @(); log = $null
        }
    }

    # コマンドを一時スクリプトに書いて -File で実行する。-Command / -EncodedCommand だと
    # リダイレクトした標準エラーが CLIXML で書かれてしまい、ログとして読めない。
    # 子の出力は UTF-8 に揃える（日本語のメッセージが化けないように）。
    # 終了コードを返さないコマンド（コマンドレット）の失敗も $? で拾って非 0 にする。
    $script = @"
[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding(`$false)
`$OutputEncoding = [Console]::OutputEncoding
`$ProgressPreference = 'SilentlyContinue'
`$global:LASTEXITCODE = 0
$command
if (-not `$?) { if (`$LASTEXITCODE) { exit `$LASTEXITCODE } else { exit 1 } }
exit `$LASTEXITCODE
"@
    $scriptPath = Join-Path $logsDir ("check-{0}.command.ps1" -f $Index)
    [System.IO.File]::WriteAllText($scriptPath, $script, (New-Object System.Text.UTF8Encoding($true)))

    $env:HARNESS_CHECK_RESULT = $resultPath
    $env:HARNESS_CHECK_OUTPUT_DIR = $logsDir
    try {
        $process = Start-Process -FilePath 'powershell.exe' `
            -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$scriptPath`"") `
            -WorkingDirectory $repoRoot -NoNewWindow -PassThru `
            -RedirectStandardOutput $logPath -RedirectStandardError $errPath
        # Handle に触れておかないと、終了後に ExitCode が取れないことがある（.NET の既知の挙動）。
        $null = $process.Handle

        $timedOut = -not $process.WaitForExit($timeoutMinutes * 60 * 1000)
        if ($timedOut) {
            & taskkill.exe /PID $process.Id /T /F 2>$null | Out-Null
            $exitCode = -1
        } else {
            $process.WaitForExit()
            $exitCode = $process.ExitCode
        }
    } finally {
        Remove-Item Env:\HARNESS_CHECK_RESULT -ErrorAction SilentlyContinue
        Remove-Item Env:\HARNESS_CHECK_OUTPUT_DIR -ErrorAction SilentlyContinue
    }

    # 標準エラーは標準出力のログの末尾にまとめる。
    if (Test-Path -LiteralPath $errPath) {
        $errText = Get-Content -LiteralPath $errPath -Raw -Encoding UTF8 -ErrorAction SilentlyContinue
        if ($errText -and $errText.Trim()) {
            Add-Content -LiteralPath $logPath -Value "`n--- stderr ---`n$errText" -Encoding UTF8
        }
        Remove-Item -LiteralPath $errPath -Force -ErrorAction SilentlyContinue
    }

    # cases は「一覧を報告しない検証」（$null）と「0 件だった検証」（空配列）を区別する。
    $passed = 0; $failed = 0; $skipped = 0; $summary = ''; $failures = @(); $cases = $null
    $result = Read-StateFile -Path $resultPath
    if ($null -ne $result) {
        foreach ($field in @('passed', 'failed', 'skipped')) {
            $value = 0
            if (-not [int]::TryParse([string](Get-Prop $result $field '0'), [ref]$value)) { $value = 0 }
            Set-Variable -Name $field -Value $value
        }
        $summary = [string](Get-Prop $result 'summary' '')
        $failures = @(Get-Prop $result 'failures' @())
        if ($null -ne $result.PSObject.Properties['cases']) { $cases = @(Get-Prop $result 'cases' @()) }
    } elseif ($exitCode -eq 0) {
        $passed = 1
    }

    # 終了コードが優先。件数の報告が 0 件失敗でも、終了コードが非 0 なら失敗として数える。
    if ($exitCode -ne 0 -and $failed -eq 0) { $failed = 1 }
    if ($timedOut) { $summary = "タイムアウト（$timeoutMinutes 分）で打ち切りました。$summary" }

    $duration = [math]::Round(((Get-Date) - $started).TotalSeconds, 1)
    $mark = if ($failed -eq 0) { '✔' } else { '✘' }
    Write-Host "$mark $name（成功 $passed / 失敗 $failed / スキップ $skipped、$duration 秒）"

    return [pscustomobject]@{
        name            = $name
        command         = $command
        exitCode        = $exitCode
        timedOut        = $timedOut
        passed          = $passed
        failed          = $failed
        skipped         = $skipped
        durationSeconds = $duration
        summary         = $summary
        failures        = $failures
        cases           = $cases
        log             = $logPath
    }
}

# Markdown の表のセルに入れられる形にする（改行と | を潰し、長すぎる文は切る）。
function Format-Cell {
    param([string]$Text, [int]$Max = 0)

    if ($null -eq $Text) { return '' }
    $value = (($Text -replace '\r?\n', ' ') -replace '\|', '\|').Trim()
    if ($Max -gt 0 -and $value.Length -gt $Max) { $value = $value.Substring(0, $Max) + '…' }
    return $value
}

function Format-Outcome {
    param([string]$Outcome)

    switch ($Outcome) {
        'Passed' { return '✅ 成功' }
        'Failed' { return '❌ 失敗' }
        'Skipped' { return '⏭ スキップ' }
        default { return "❔ $Outcome" }
    }
}

# テスト仕様書を組み立てる。人間が「どのテストケースを実行し、何を保証しているか」を
# 1 件ずつ確認するためのもの。説明（description）が無いケースは数えて目立たせる。
function New-TestSpec {
    param($Results, [string]$ShortHead, [string]$Branch, [int]$DirtyCount)

    $spec = New-Object System.Text.StringBuilder
    [void]$spec.AppendLine('# テスト仕様書')
    [void]$spec.AppendLine()
    [void]$spec.AppendLine("- コミット: ``$ShortHead``（$Branch）")
    [void]$spec.AppendLine("- 実行日時: $((Get-Date).ToString('yyyy-MM-dd HH:mm:ss'))")
    if ($DirtyCount -gt 0) {
        [void]$spec.AppendLine("- ⚠ 未コミットの変更が $DirtyCount ファイルある状態で実行しました。")
    }
    [void]$spec.AppendLine()

    $allCases = @($Results | Where-Object { $null -ne $_.cases } | ForEach-Object { $_.cases })
    $undescribed = @($allCases | Where-Object { -not [string](Get-Prop $_ 'description' '') })

    [void]$spec.AppendLine('| 検証 | テストケース | 成功 | 失敗 | スキップ |')
    [void]$spec.AppendLine('|---|---|---|---|---|')
    foreach ($r in $Results) {
        if ($null -eq $r.cases) {
            [void]$spec.AppendLine("| $(Format-Cell $r.name) | （一覧なし） | $($r.passed) | $($r.failed) | $($r.skipped) |")
            continue
        }
        $c = @($r.cases)
        $p = @($c | Where-Object { (Get-Prop $_ 'outcome' '') -eq 'Passed' }).Count
        $f = @($c | Where-Object { (Get-Prop $_ 'outcome' '') -eq 'Failed' }).Count
        $s = @($c | Where-Object { (Get-Prop $_ 'outcome' '') -eq 'Skipped' }).Count
        [void]$spec.AppendLine("| $(Format-Cell $r.name) | $($c.Count) | $p | $f | $s |")
    }
    [void]$spec.AppendLine()

    if ($undescribed.Count -gt 0) {
        [void]$spec.AppendLine("> ⚠ 何を保証するかの説明が書かれていないテストケースが $($undescribed.Count) 件あります（テスト名のみ表示）。")
        [void]$spec.AppendLine()
    }

    $number = 0
    foreach ($r in $Results) {
        [void]$spec.AppendLine("## $($r.name)")
        [void]$spec.AppendLine()

        if ($null -eq $r.cases) {
            [void]$spec.AppendLine('この検証はテストケースの一覧を出力しません（終了コードで合否を判定）。')
            if ($r.summary) { [void]$spec.AppendLine(); [void]$spec.AppendLine((Format-Cell $r.summary)) }
            [void]$spec.AppendLine()
            continue
        }
        if (@($r.cases).Count -eq 0) {
            [void]$spec.AppendLine('実行されたテストケースはありません。')
            if ($r.summary) { [void]$spec.AppendLine(); [void]$spec.AppendLine((Format-Cell $r.summary)) }
            [void]$spec.AppendLine()
            continue
        }

        # スイート（クラスやファイル）ごとにまとめる。出現順を保つ。
        $suites = New-Object System.Collections.Specialized.OrderedDictionary
        foreach ($case in $r.cases) {
            $key = [string](Get-Prop $case 'suite' '')
            if ([string]::IsNullOrWhiteSpace($key)) { $key = '（スイートなし）' }
            if (-not $suites.Contains($key)) { $suites[$key] = New-Object System.Collections.Generic.List[object] }
            $suites[$key].Add($case)
        }

        foreach ($key in $suites.Keys) {
            $items = $suites[$key]
            [void]$spec.AppendLine("### $key")
            [void]$spec.AppendLine()
            $suiteDescription = [string](Get-Prop $items[0] 'suiteDescription' '')
            if ($suiteDescription) { [void]$spec.AppendLine((Format-Cell $suiteDescription)); [void]$spec.AppendLine() }

            [void]$spec.AppendLine('| # | 保証すること（テストケース） | 結果 | 秒 | 備考 |')
            [void]$spec.AppendLine('|---|---|---|---|---|')
            foreach ($case in $items) {
                $number += 1
                $caseName = Format-Cell ([string](Get-Prop $case 'name' ''))
                $description = Format-Cell ([string](Get-Prop $case 'description' ''))
                $title = if ($description) { "$description<br>``$caseName``" } else { "``$caseName``（説明なし）" }

                $notes = @()
                $message = [string](Get-Prop $case 'message' '')
                if ($message) { $notes += (Format-Cell $message 200) }
                $categories = @(Get-Prop $case 'categories' @() | Where-Object { $_ })
                if ($categories.Count -gt 0) { $notes += "分類: $(Format-Cell ($categories -join ', '))" }

                $seconds = Get-Prop $case 'durationSeconds' ''
                [void]$spec.AppendLine("| $number | $title | $(Format-Outcome ([string](Get-Prop $case 'outcome' ''))) | $seconds | $($notes -join '<br>') |")
            }
            [void]$spec.AppendLine()
        }
    }

    return $spec.ToString()
}

$results = New-Object System.Collections.Generic.List[psobject]
$totalStarted = Get-Date
for ($i = 0; $i -lt $checks.Count; $i++) {
    $results.Add((Invoke-Check -Check $checks[$i] -Index ($i + 1)))
}
$totalDuration = [math]::Round(((Get-Date) - $totalStarted).TotalSeconds, 1)

$sumPassed = 0; $sumFailed = 0; $sumSkipped = 0
foreach ($r in $results) {
    $sumPassed += $r.passed
    $sumFailed += $r.failed
    $sumSkipped += $r.skipped
}

$noChecks = $checks.Count -eq 0
if ($noChecks) {
    Write-Host '✘ 検証が 1 件も設定されていません（.claude\harness.json の checks）。'
    $sumFailed = 1
}

if ($Only) {
    Write-Host '（-Only 指定のため記録は作りません）'
    if ($sumFailed -eq 0) { exit 0 } else { exit 1 }
}

# --- 記録（guard-gh.ps1 が読む）---
$verification = [ordered]@{
    commit          = $head
    branch          = $branch
    generatedAt     = (Get-Date).ToString('o')
    passed          = $sumPassed
    failed          = $sumFailed
    skipped         = $sumSkipped
    durationSeconds = $totalDuration
    dirtyFiles      = $dirtyFiles.Count
    checks          = @($results | ForEach-Object {
            [ordered]@{
                name = $_.name; exitCode = $_.exitCode; passed = $_.passed; failed = $_.failed
                skipped = $_.skipped; durationSeconds = $_.durationSeconds; timedOut = $_.timedOut
            }
        })
}
$utf8 = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText((Join-Path $reportsDir 'verification.json'),
    (ConvertTo-Json -InputObject $verification -Depth 6), $utf8)

# --- レポート（PR 本文に貼る）---
$md = New-Object System.Text.StringBuilder
$shortHead = if ($head.Length -ge 7) { $head.Substring(0, 7) } else { $head }
[void]$md.AppendLine('# 検証レポート')
[void]$md.AppendLine()
[void]$md.AppendLine("- コミット: ``$shortHead``（$branch）")
[void]$md.AppendLine("- 実行日時: $((Get-Date).ToString('yyyy-MM-dd HH:mm:ss'))")
if ($dirtyFiles.Count -gt 0) {
    [void]$md.AppendLine("- ⚠ 未コミットの変更が $($dirtyFiles.Count) ファイルある状態で実行しました。記録はコミット $shortHead に紐づきますが、検証した内容とは一致しない可能性があります。")
}
[void]$md.AppendLine()
[void]$md.AppendLine('| 項目 | 件数 |')
[void]$md.AppendLine('|---|---|')
[void]$md.AppendLine("| 成功 | $sumPassed |")
[void]$md.AppendLine("| 失敗 | $sumFailed |")
[void]$md.AppendLine("| スキップ | $sumSkipped |")
[void]$md.AppendLine("| 所要時間 | $totalDuration 秒 |")
[void]$md.AppendLine()

if ($noChecks) {
    [void]$md.AppendLine('**検証が設定されていません。** `.claude/harness.json` の `checks` を設定してください。')
    [void]$md.AppendLine()
}

if ($results.Count -gt 0) {
    [void]$md.AppendLine('## 検証ごとの結果')
    [void]$md.AppendLine()
    [void]$md.AppendLine('| 検証 | 結果 | 成功 | 失敗 | スキップ | 秒 | 備考 |')
    [void]$md.AppendLine('|---|---|---|---|---|---|---|')
    foreach ($r in $results) {
        $mark = if ($r.failed -eq 0) { '✅' } else { '❌' }
        $note = ($r.summary -replace '\r?\n', ' ' -replace '\|', '\|')
        [void]$md.AppendLine("| $($r.name) | $mark | $($r.passed) | $($r.failed) | $($r.skipped) | $($r.durationSeconds) | $note |")
    }
    [void]$md.AppendLine()
}

foreach ($r in ($results | Where-Object { $_.failed -gt 0 })) {
    [void]$md.AppendLine("## 失敗: $($r.name)")
    [void]$md.AppendLine()
    [void]$md.AppendLine("コマンド: ``$($r.command)``（終了コード $($r.exitCode)）")
    [void]$md.AppendLine()
    foreach ($f in $r.failures) {
        [void]$md.AppendLine("- **$(Get-Prop $f 'name' '')**: $((([string](Get-Prop $f 'message' '')) -replace '\r?\n', ' ').Trim())")
    }
    if ($r.log -and (Test-Path -LiteralPath $r.log)) {
        $tail = @(Get-Content -LiteralPath $r.log -Encoding UTF8 -Tail 40 -ErrorAction SilentlyContinue)
        if ($tail.Count -gt 0) {
            [void]$md.AppendLine()
            [void]$md.AppendLine('<details><summary>出力の末尾</summary>')
            [void]$md.AppendLine()
            [void]$md.AppendLine('```')
            foreach ($line in $tail) { [void]$md.AppendLine($line) }
            [void]$md.AppendLine('```')
            [void]$md.AppendLine()
            [void]$md.AppendLine('</details>')
        }
    }
    [void]$md.AppendLine()
}

[void]$md.AppendLine('実行したテストケースの一覧は `test-spec.md`（テスト仕様書）を参照。')

[System.IO.File]::WriteAllText((Join-Path $reportsDir 'test-report.md'), $md.ToString(), $utf8)

# --- テスト仕様書（人間がテストケースを確認する）---
$specText = New-TestSpec -Results $results -ShortHead $shortHead -Branch $branch -DirtyCount $dirtyFiles.Count
[System.IO.File]::WriteAllText((Join-Path $reportsDir 'test-spec.md'), $specText, $utf8)

Write-Host ''
Write-Host "合計: 成功 $sumPassed / 失敗 $sumFailed / スキップ $sumSkipped（$totalDuration 秒）"
Write-Host "レポート: $(Join-Path $reportsDir 'test-report.md')"
Write-Host "テスト仕様書: $(Join-Path $reportsDir 'test-spec.md')"

if ($sumFailed -eq 0) { exit 0 } else { exit 1 }
