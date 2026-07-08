param(
    [switch] $Refresh,
    [switch] $RunTests,
    [string] $TestFilter = "Market.Tests",
    [int] $WaitSeconds = 30,
    [int] $RecompileTimeoutMs = 120000,
    [int] $HealthTimeoutMs = 60000,
    [int] $TestTimeoutSeconds = 180
)

$ErrorActionPreference = "Stop"

$projectRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$callScript = Join-Path $projectRoot ".claude\tools\unity-ws-call.mjs"
$doctorScript = Join-Path $projectRoot ".claude\tools\mcp-doctor.ps1"
$nodeCommand = Get-Command node -ErrorAction SilentlyContinue

if ($null -eq $nodeCommand) {
    Write-Error "Node.js was not found on PATH."
    exit 1
}

function Invoke-UnityTool(
    [string] $Method,
    [hashtable] $Params,
    [int] $TimeoutMs,
    [string] $Client,
    [int] $Attempts = 6,
    [int] $RetryDelaySeconds = 2
) {
    $json = $Params | ConvertTo-Json -Depth 20 -Compress

    for ($attempt = 1; $attempt -le $Attempts; $attempt++) {
        $output = $null
        $exitCode = 1

        try {
            $output = $json | & $nodeCommand.Source $callScript $Method - --timeout $TimeoutMs --client $Client 2>&1
            $exitCode = $LASTEXITCODE
        }
        catch {
            $output = $_.Exception.Message
            $exitCode = 1
        }

        $text = ($output | Out-String).Trim()

        if ($exitCode -eq 0) {
            try {
                return $text | ConvertFrom-Json
            }
            catch {
                throw "Unity tool '$Method' returned invalid JSON: $text"
            }
        }

        $retryable = $text -match "ECONNREFUSED|closed before a response|entered Play mode|timed out"
        if ($retryable -and $attempt -lt $Attempts) {
            Write-Host "Unity tool '$Method' temporarily unavailable (attempt $attempt/$Attempts). Retrying..."
            Start-Sleep -Seconds $RetryDelaySeconds
            continue
        }

        throw "Unity tool '$Method' failed: $text"
    }
}

function Assert-Success([string] $Step, $Result) {
    if ($Result.success -ne $true) {
        throw "$Step failed: $($Result.message)"
    }
}

Push-Location -LiteralPath $projectRoot
try {
    Write-Host "== MCP doctor =="
    & powershell -ExecutionPolicy Bypass -File $doctorScript -WaitSeconds $WaitSeconds
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }

    if ($Refresh) {
        Write-Host "== Assets/Refresh =="
        $refreshResult = Invoke-UnityTool "execute_menu_item" @{ menuPath = "Assets/Refresh" } 30000 "Codex Fast Verify Refresh"
        Assert-Success "Assets/Refresh" $refreshResult
        Write-Host $refreshResult.message
    }

    Write-Host "== Recompile scripts =="
    $recompileResult = Invoke-UnityTool "recompile_scripts" @{
        returnWithLogs = $true
        logsLimit = 120
    } $RecompileTimeoutMs "Codex Fast Verify Recompile"
    Assert-Success "Recompile" $recompileResult
    Write-Host $recompileResult.message

    Write-Host "== Health report =="
    $healthResult = Invoke-UnityTool "get_health_report" @{
        includeTests = $false
        testMode = ""
        maxConsoleErrors = 20
        maxTests = 0
    } $HealthTimeoutMs "Codex Fast Verify Health"
    Assert-Success "Health report" $healthResult
    Write-Host $healthResult.message

    if ($healthResult.overallStatus -ne "ok") {
        throw "Health report is not ok: $($healthResult.message)"
    }

    if ($RunTests) {
        Write-Host "== EditMode tests =="
        $testsResult = Invoke-UnityTool "run_tests" @{
            testMode = "EditMode"
            testFilter = $TestFilter
            timeoutSeconds = $TestTimeoutSeconds
            returnOnlyFailures = $true
            returnWithLogs = $false
        } (($TestTimeoutSeconds + 10) * 1000) "Codex Fast Verify Tests"
        Assert-Success "EditMode tests" $testsResult
        Write-Host $testsResult.message

        if (($null -ne $testsResult.failCount -and $testsResult.failCount -gt 0) -or
            ($null -ne $testsResult.resultState -and $testsResult.resultState -notmatch "^Passed")) {
            throw "EditMode tests are not green: $($testsResult.message)"
        }
    }

    Write-Host "== Unity verification OK =="
}
finally {
    Pop-Location
}
