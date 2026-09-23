# Unity Test Runner を batchmode で実行する検証アダプタ（run-checks.ps1 から呼ぶ）。
#
#   .\.claude\scripts\checks\unity-tests.ps1 -Platform EditMode
#
# スクリプトがコンパイルできなければテストは走らないので、コンパイル確認も兼ねる。
# テストが 0 件でも、コンパイルが通れば成功として扱う（その旨を summary に書く）。
#
# Unity エディタの場所は次の順で決める。
#   1. -UnityPath
#   2. 環境変数 UNITY_EDITOR_PATH
#   3. Unity Hub の既定の場所（ProjectSettings\ProjectVersion.txt のバージョン）
#
# 同じプロジェクトを Unity エディタで開いたままだと batchmode は起動できない。
# その場合は実行せずに失敗として返す（エディタを閉じてから再実行する）。

param(
    [ValidateSet('EditMode', 'PlayMode')][string]$Platform = 'EditMode',
    [string]$ProjectPath = (Get-Location).Path,
    [string]$UnityPath = $env:UNITY_EDITOR_PATH,
    [int]$TimeoutMinutes = 30,
    # 特定のテストだけを走らせる（Unity の -testFilter にそのまま渡す）。
    [string]$TestFilter
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'lib\NUnitResults.ps1')

$resultFile = $env:HARNESS_CHECK_RESULT

# 結果を表示し、run-checks.ps1 に渡す結果 JSON を書いて終了する。
# cases は実行した全テストケース（テスト仕様書の元になる）。
function Complete-Check {
    param(
        [int]$Passed = 0, [int]$Failed = 0, [int]$Skipped = 0,
        [string]$Summary = '', [object[]]$Failures = @(), [object[]]$Cases = @(), [int]$ExitCode
    )

    Write-Host $Summary
    foreach ($f in $Failures) { Write-Host "  ✘ $($f.name): $($f.message)" }

    if ($resultFile) {
        $payload = [ordered]@{
            passed = $Passed; failed = $Failed; skipped = $Skipped
            summary = $Summary; failures = @($Failures); cases = @($Cases)
        }
        [System.IO.File]::WriteAllText($resultFile, (ConvertTo-Json -InputObject $payload -Depth 6),
            (New-Object System.Text.UTF8Encoding($false)))
    }
    exit $ExitCode
}

$ProjectPath = (Resolve-Path -LiteralPath $ProjectPath).Path
$versionFile = Join-Path $ProjectPath 'ProjectSettings\ProjectVersion.txt'
if (-not (Test-Path -LiteralPath $versionFile)) {
    Complete-Check -Failed 1 -ExitCode 3 -Summary "Unity プロジェクトではありません（$versionFile がありません）。"
}

# --- エディタを探す ---
$version = $null
foreach ($line in (Get-Content -LiteralPath $versionFile -Encoding UTF8)) {
    if ($line -match '^m_EditorVersion:\s*(\S+)') { $version = $Matches[1]; break }
}

if ([string]::IsNullOrWhiteSpace($UnityPath)) {
    $UnityPath = Join-Path $env:ProgramFiles "Unity\Hub\Editor\$version\Editor\Unity.exe"
}
if (-not (Test-Path -LiteralPath $UnityPath)) {
    Complete-Check -Failed 1 -ExitCode 3 -Summary @"
Unity $version のエディタが見つかりません: $UnityPath
Unity Hub でこのバージョンをインストールするか、環境変数 UNITY_EDITOR_PATH に Unity.exe のパスを設定してください。
"@
}

# --- エディタで開かれていないか ---
# Unity はプロジェクトを開いている間 Temp\UnityLockfile を排他ロックする。
# ファイルが残っているだけ（異常終了の跡）なら開けるので、実際に開けるかで判定する。
$lockFile = Join-Path $ProjectPath 'Temp\UnityLockfile'
if (Test-Path -LiteralPath $lockFile) {
    try {
        $stream = [System.IO.File]::Open($lockFile, 'Open', 'ReadWrite', 'None')
        $stream.Dispose()
    } catch {
        Complete-Check -Failed 1 -ExitCode 3 -Summary @"
Unity エディタでこのプロジェクトが開かれているため、batchmode で検証できません。
エディタを閉じてから再実行してください（利用者に依頼すること。エディタを勝手に終了させない）。
"@
    }
}

# --- 実行 ---
$outputDir = $env:HARNESS_CHECK_OUTPUT_DIR
if ([string]::IsNullOrWhiteSpace($outputDir)) { $outputDir = [System.IO.Path]::GetTempPath() }
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
$xmlPath = Join-Path $outputDir "unity-$($Platform.ToLowerInvariant())-results.xml"
$logPath = Join-Path $outputDir "unity-$($Platform.ToLowerInvariant())-editor.log"
Remove-Item -LiteralPath $xmlPath, $logPath -Force -ErrorAction SilentlyContinue

$arguments = @(
    '-batchmode',
    '-projectPath', "`"$ProjectPath`"",
    '-runTests',
    '-testPlatform', $Platform,
    '-testResults', "`"$xmlPath`"",
    '-logFile', "`"$logPath`""
)
# PlayMode は描画を伴うテストがあり得るので -nographics を付けない。
if ($Platform -eq 'EditMode') { $arguments += '-nographics' }
if ($TestFilter) { $arguments += @('-testFilter', "`"$TestFilter`"") }

Write-Host "Unity $version で $Platform テストを実行します（初回や Library 再構築時は数分かかります）…"
$started = Get-Date
$process = Start-Process -FilePath $UnityPath -ArgumentList $arguments -PassThru -NoNewWindow
$null = $process.Handle
if (-not $process.WaitForExit($TimeoutMinutes * 60 * 1000)) {
    & taskkill.exe /PID $process.Id /T /F 2>$null | Out-Null
    Complete-Check -Failed 1 -ExitCode 4 -Summary "Unity が $TimeoutMinutes 分以内に終わらなかったため打ち切りました。ログ: $logPath"
}
$process.WaitForExit()
$exitCode = $process.ExitCode
$elapsed = [math]::Round(((Get-Date) - $started).TotalSeconds, 1)

# --- 結果の読み取り ---
if (-not (Test-Path -LiteralPath $xmlPath)) {
    # 結果ファイルが無い＝テストまで到達していない。ほとんどはコンパイルエラー。
    $compileErrors = @()
    if (Test-Path -LiteralPath $logPath) {
        $compileErrors = @(Select-String -LiteralPath $logPath -Pattern ': error CS\d+:' -Encoding UTF8 |
                ForEach-Object { $_.Line.Trim() } | Select-Object -Unique | Select-Object -First 30)
    }

    if ($compileErrors.Count -gt 0) {
        $failures = @($compileErrors | ForEach-Object { [ordered]@{ name = 'コンパイルエラー'; message = $_ } })
        Complete-Check -Failed $compileErrors.Count -Failures $failures -ExitCode 1 `
            -Summary "スクリプトのコンパイルに失敗しました（$($compileErrors.Count) 件）。ログ: $logPath"
    }

    $tail = ''
    if (Test-Path -LiteralPath $logPath) {
        $tail = (Get-Content -LiteralPath $logPath -Encoding UTF8 -Tail 30) -join "`n"
    }
    Complete-Check -Failed 1 -ExitCode 1 -Summary @"
Unity がテスト結果を出力しませんでした（終了コード $exitCode）。ログ: $logPath
$tail
"@
}

try {
    $parsed = ConvertFrom-NUnitResults -Path $xmlPath
} catch {
    Complete-Check -Failed 1 -ExitCode 1 -Summary "テスト結果の形式を読めませんでした: $xmlPath（$($_.Exception.Message)）"
}

$total = $parsed.total
$passed = $parsed.passed
$failed = $parsed.failed
$skipped = $parsed.skipped
$failures = @($parsed.failures)
$cases = @($parsed.cases)

# Unity の終了コード: 0 = 全成功、2 = 失敗したテストあり、それ以外 = 実行エラー。
if ($exitCode -ne 0 -and $failed -eq 0) { $failed = 1 }

$summary = "${Platform}: 合計 $total 件（成功 $passed / 失敗 $failed / スキップ $skipped）、$elapsed 秒"
if ($total -eq 0) { $summary += '。テストは 0 件（コンパイルが通ることのみ確認）' }

$code = if ($failed -eq 0) { 0 } else { 1 }
Complete-Check -Passed $passed -Failed $failed -Skipped $skipped -Failures $failures -Cases $cases `
    -Summary $summary -ExitCode $code
