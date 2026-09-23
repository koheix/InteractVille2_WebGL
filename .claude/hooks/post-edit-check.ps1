# PostToolUse (Edit|Write) フック。
#
# 編集直後にファイルの健全性を確認し、壊れていればその場で Claude に差し戻す。
# テスト実行まで気づかない型エラー・構文エラーを、次の 1 手で潰せるようにするのが狙い。
#
#   .ps1 … PowerShell パーサで構文チェック（どのプロジェクトでも有効）
#   その他 … .claude\harness.json の postEditChecks に従う
#
# postEditChecks の例（TypeScript プロジェクト）:
#
#   "postEditChecks": [
#     { "name": "型チェック", "extensions": [".ts", ".tsx"], "command": "npx tsc -b --pretty false" },
#     { "name": "lint",       "extensions": [".ts", ".tsx"], "command": "npx biome check --no-errors-on-unmatched {file}" }
#   ]
#
# command はリポジトリのルートで PowerShell として実行する。{file} は編集したファイルの
# パス（引用符付き）に置き換わる。終了コードが 0 以外なら出力を添えて差し戻す。

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'lib\Common.ps1')

# 子プロセスは UTF-8 で出力することが多いが、Windows PowerShell 5.1 は既定で OEM コードページ
# として読むため、日本語を含む指摘が文字化けする。子プロセス出力の解釈を UTF-8 に揃える。
try { [Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false) } catch { }

function Write-Block {
    param([Parameter(Mandatory)][string]$Reason)

    Write-HookJson @{
        decision = 'block'
        reason   = $Reason
    }
    exit 0
}

$hookInput = Read-HookInput
$filePath = Get-Prop $hookInput 'tool_response.filePath'
if (-not $filePath) { $filePath = Get-Prop $hookInput 'tool_input.file_path' }
if (-not $filePath -or -not (Test-Path -LiteralPath $filePath -PathType Leaf)) { exit 0 }

$extension = [System.IO.Path]::GetExtension($filePath).ToLowerInvariant()

# --- PowerShell スクリプト ---
if ($extension -eq '.ps1') {
    # Windows PowerShell 5.1 は BOM の無い .ps1 をシステムの ANSI コードページとして読む。
    # 日本語を含むスクリプトが BOM 無しで保存されると文字化けし、ヒアドキュメントの
    # 終端すら壊れてスクリプト全体が構文エラーになる。編集ツールは BOM を落とすので、
    # ここで気づいたら黙って付け直す。
    $bytes = [System.IO.File]::ReadAllBytes($filePath)
    $hasBom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
    $hasNonAscii = $false
    foreach ($byte in $bytes) { if ($byte -gt 0x7F) { $hasNonAscii = $true; break } }

    if ($hasNonAscii -and -not $hasBom) {
        try {
            $text = [System.Text.UTF8Encoding]::new($false).GetString($bytes)
            [System.IO.File]::WriteAllText($filePath, $text, [System.Text.UTF8Encoding]::new($true))
            Write-Notice -HookEventName 'PostToolUse' `
                -Message "$filePath に UTF-8 BOM を付け直しました（BOM が無いと Windows PowerShell 5.1 が日本語を文字化けさせ、スクリプトが動かなくなるため）。"
        } catch {
            Write-Block "$filePath は日本語を含みますが UTF-8 BOM がありません。Windows PowerShell 5.1 が文字化けさせるため、BOM 付き UTF-8 で保存し直してください。"
        }
    }

    $tokens = $null
    $errors = $null
    [System.Management.Automation.Language.Parser]::ParseFile($filePath, [ref]$tokens, [ref]$errors) | Out-Null

    if ($errors -and $errors.Count -gt 0) {
        $details = ($errors | ForEach-Object { "  {0} 行目: {1}" -f $_.Extent.StartLineNumber, $_.Message }) -join "`n"
        Write-Block "$filePath に PowerShell の構文エラーがあります。修正してください。`n$details"
    }
    exit 0
}

# --- harness.json の postEditChecks ---
$gitExe = Get-GitExe
$repoRoot = $null
if ($gitExe) { $repoRoot = Get-RepoRoot -GitExe $gitExe -Directory (Split-Path -Path $filePath -Parent) }
if (-not $repoRoot) { exit 0 }

$checks = @(Get-Prop (Get-HarnessConfig $repoRoot) 'postEditChecks' @())
if ($checks.Count -eq 0) { exit 0 }

$failures = New-Object System.Collections.Generic.List[string]

foreach ($check in $checks) {
    $extensions = @(Get-Prop $check 'extensions' @() | ForEach-Object { ([string]$_).ToLowerInvariant() })
    if ($extensions -notcontains $extension) { continue }

    $command = [string](Get-Prop $check 'command' '')
    if ([string]::IsNullOrWhiteSpace($command)) { continue }
    $name = [string](Get-Prop $check 'name' $command)

    $quotedFile = "'" + ($filePath -replace "'", "''") + "'"
    $script = '[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false); ' +
        $command.Replace('{file}', $quotedFile) + '; exit $LASTEXITCODE'
    $encoded = [Convert]::ToBase64String([System.Text.Encoding]::Unicode.GetBytes($script))

    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    Push-Location -LiteralPath $repoRoot
    try {
        $output = & powershell.exe -NoProfile -ExecutionPolicy Bypass -EncodedCommand $encoded 2>&1 | Out-String
        $exitCode = $LASTEXITCODE
    } finally {
        Pop-Location
        $ErrorActionPreference = $previous
    }

    if ($exitCode -ne 0) {
        $failures.Add("[$name] 終了コード $exitCode`n$($output.Trim())")
    }
}

if ($failures.Count -gt 0) {
    Write-Block ("$filePath の編集後チェックで問題が見つかりました。修正してください。`n`n" + ($failures -join "`n`n"))
}

exit 0
