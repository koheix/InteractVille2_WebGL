# NUnit 3 形式のテスト結果 XML を、run-checks.ps1 の結果形式（件数と cases）に変換する。
#
# Unity Test Runner（-testResults）、dotnet test の NUnit ロガーなどが出力する形式。
# テスト仕様書（<reportsDir>/test-spec.md）に載せるため、失敗したものだけでなく
# 実行した全テストケースを返す。
#
# 各ケースの説明は NUnit の [Description("...")] から取る。フィクスチャ（クラス）に付けた
# [Description] はスイートの説明になる。説明が無いケースは仕様書でテスト名がそのまま表示される。

Set-StrictMode -Version Latest

# NUnit の result 属性を、仕様書で使う結果の区分にそろえる。
function ConvertTo-CaseOutcome {
    param([string]$Result, [string]$Label)

    switch ($Result) {
        'Passed' { return 'Passed' }
        'Failed' { return 'Failed' }
        'Skipped' { return 'Skipped' }
        'Inconclusive' { return 'Skipped' }
        # 警告付きの成功（Assert.Warn など）。NUnit でも成功として数えられる。
        'Warning' { return 'Passed' }
        default { if ($Label) { return $Label } else { return $Result } }
    }
}

# <properties> から指定した名前の値をすべて返す。
function Get-NUnitProperties {
    param([System.Xml.XmlNode]$Node, [string]$Name)

    if ($null -eq $Node) { return @() }
    return @($Node.SelectNodes("properties/property[@name='$Name']") | ForEach-Object { $_.GetAttribute('value') })
}

function ConvertFrom-NUnitResults {
    param([Parameter(Mandatory)][string]$Path)

    [xml]$xml = Get-Content -LiteralPath $Path -Raw -Encoding UTF8
    $run = $xml.SelectSingleNode('/test-run')
    if ($null -eq $run) { throw "NUnit 3 形式のテスト結果ではありません: $Path" }

    $cases = New-Object System.Collections.Generic.List[object]
    foreach ($case in $xml.SelectNodes('//test-case')) {
        # スイートはクラス（フィクスチャ）単位。パラメータ化テストはその親のさらに上にある。
        #
        # 要素名は get_LocalName() で取る。PowerShell の XML アダプタでは $node.Name が
        # 要素名ではなく name 属性（テスト名）を返してしまう。
        $fixture = $case.ParentNode
        while ($null -ne $fixture -and $fixture.get_LocalName() -eq 'test-suite' -and
            $fixture.GetAttribute('type') -notin @('TestFixture', 'GenericFixture', 'SetUpFixture')) {
            $fixture = $fixture.ParentNode
        }
        if ($null -ne $fixture -and $fixture.get_LocalName() -ne 'test-suite') { $fixture = $null }

        $suite = $case.GetAttribute('classname')
        if (-not $suite -and $fixture) { $suite = $fixture.GetAttribute('fullname') }

        $message = ''
        $messageNode = $case.SelectSingleNode('failure/message')
        if ($null -eq $messageNode) { $messageNode = $case.SelectSingleNode('reason/message') }
        if ($messageNode) { $message = $messageNode.InnerText.Trim() }

        $duration = 0.0
        [void][double]::TryParse($case.GetAttribute('duration'),
            [System.Globalization.NumberStyles]::Float, [System.Globalization.CultureInfo]::InvariantCulture, [ref]$duration)

        # パラメータ化テストは、ケース自身に説明が無ければ親メソッドの説明を使う。
        $description = @(Get-NUnitProperties $case 'Description') -join ' '
        if (-not $description -and $case.ParentNode.GetAttribute('type') -eq 'ParameterizedMethod') {
            $description = @(Get-NUnitProperties $case.ParentNode 'Description') -join ' '
        }

        $cases.Add([ordered]@{
                suite            = $suite
                suiteDescription = (@(Get-NUnitProperties $fixture 'Description') -join ' ')
                name             = $case.GetAttribute('name')
                fullName         = $case.GetAttribute('fullname')
                description      = $description
                categories       = @(Get-NUnitProperties $case 'Category')
                outcome          = (ConvertTo-CaseOutcome $case.GetAttribute('result') $case.GetAttribute('label'))
                durationSeconds  = [math]::Round($duration, 3)
                message          = $message
            })
    }

    $failures = @($cases | Where-Object { $_.outcome -eq 'Failed' } |
            ForEach-Object { [ordered]@{ name = $_.fullName; message = $_.message } })

    return [pscustomobject]@{
        total    = [int]$run.GetAttribute('total')
        passed   = [int]$run.GetAttribute('passed')
        failed   = [int]$run.GetAttribute('failed')
        skipped  = [int]$run.GetAttribute('skipped') + [int]$run.GetAttribute('inconclusive')
        cases    = $cases.ToArray()
        failures = $failures
    }
}
