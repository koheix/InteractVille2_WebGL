# vitest を実行する検証アダプタ（run-checks.ps1 から呼ぶ）。
#
#   .\.claude\scripts\checks\vitest-tests.ps1 -Path server\llm-proxy
#
# 実行したテストケースを run-checks.ps1 の結果形式（cases）で返すので、テスト仕様書に 1 件ずつ載る。
# テスト名（it の第 1 引数）が「何を保証するか」の説明になる。
# node_modules が無ければ npm ci で依存を入れてから実行する。

param(
    # package.json のあるディレクトリ（リポジトリのルートからの相対パス）
    [Parameter(Mandatory)][string]$Path
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$resultFile = $env:HARNESS_CHECK_RESULT
$outputDir = $env:HARNESS_CHECK_OUTPUT_DIR
if ([string]::IsNullOrWhiteSpace($outputDir)) { $outputDir = [System.IO.Path]::GetTempPath() }

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

$projectDir = (Resolve-Path -LiteralPath $Path).Path
if (-not (Test-Path -LiteralPath (Join-Path $projectDir 'package.json'))) {
    Complete-Check -Failed 1 -ExitCode 3 -Summary "package.json がありません: $projectDir"
}

Push-Location -LiteralPath $projectDir
try {
    if (-not (Test-Path -LiteralPath (Join-Path $projectDir 'node_modules'))) {
        Write-Host '依存が入っていないので npm ci を実行します…'
        & npm ci --no-audit --no-fund
        if ($LASTEXITCODE -ne 0) { Complete-Check -Failed 1 -ExitCode 3 -Summary 'npm ci に失敗しました。' }
    }

    $jsonPath = Join-Path $outputDir ('vitest-' + (Split-Path -Leaf $projectDir) + '.json')
    Remove-Item -LiteralPath $jsonPath -Force -ErrorAction SilentlyContinue

    & npx vitest run --reporter=default --reporter=json --outputFile.json="$jsonPath"
    $exitCode = $LASTEXITCODE
} finally {
    Pop-Location
}

if (-not (Test-Path -LiteralPath $jsonPath)) {
    Complete-Check -Failed 1 -ExitCode 1 -Summary "vitest が結果を出力しませんでした（終了コード $exitCode）。"
}

$report = Get-Content -LiteralPath $jsonPath -Raw -Encoding UTF8 | ConvertFrom-Json

$cases = New-Object System.Collections.Generic.List[object]
foreach ($file in @($report.testResults)) {
    $relative = ([string]$file.name).Replace('\', '/')
    $marker = $relative.LastIndexOf('/test/')
    if ($marker -ge 0) { $relative = $relative.Substring($marker + 1) }

    foreach ($assertion in @($file.assertionResults)) {
        $outcome = switch ([string]$assertion.status) {
            'passed' { 'Passed' }
            'failed' { 'Failed' }
            default { 'Skipped' }
        }
        $ancestors = @($assertion.ancestorTitles | Where-Object { $_ })
        $suite = if ($ancestors.Count -gt 0) { "$relative › $($ancestors -join ' › ')" } else { $relative }
        $message = (@($assertion.failureMessages) | ForEach-Object { ([string]$_ -split "`n")[0] }) -join ' / '
        $duration = 0.0
        if ($null -ne $assertion.duration) { $duration = [math]::Round([double]$assertion.duration / 1000, 3) }

        $cases.Add([ordered]@{
                suite           = $suite
                name            = [string]$assertion.title
                fullName        = [string]$assertion.fullName
                description     = [string]$assertion.title
                outcome         = $outcome
                durationSeconds = $duration
                message         = $message
            })
    }
}

$passed = @($cases | Where-Object { $_.outcome -eq 'Passed' }).Count
$failed = @($cases | Where-Object { $_.outcome -eq 'Failed' }).Count
$skipped = @($cases | Where-Object { $_.outcome -eq 'Skipped' }).Count
$failures = @($cases | Where-Object { $_.outcome -eq 'Failed' } | ForEach-Object { [ordered]@{ name = $_.fullName; message = $_.message } })

# テストファイル自体の読み込みに失敗した場合など、ケースに現れない失敗もある。終了コードを優先する。
if ($exitCode -ne 0 -and $failed -eq 0) { $failed = 1 }

$summary = "vitest（$Path）: 合計 $($cases.Count) 件（成功 $passed / 失敗 $failed / スキップ $skipped）"
$code = if ($failed -eq 0) { 0 } else { 1 }
Complete-Check -Passed $passed -Failed $failed -Skipped $skipped -Failures $failures -Cases $cases.ToArray() -Summary $summary -ExitCode $code
