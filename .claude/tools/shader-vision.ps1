<#
.SYNOPSIS
    Renders a Shader Vision job in the running Unity Editor and prints the measured result.

.DESCRIPTION
    One command turns "I changed a shader" into evidence: it stamps a fresh run id into the job,
    drops it at Artifacts/ShaderVision/job.json, triggers the Editor menu item over the MCP
    WebSocket, waits for the matching report and prints the numbers plus the contact sheet path.

    The sheet is a normal PNG - open it, or have the agent read it directly.

.EXAMPLE
    .\.claude\tools\shader-vision.ps1 water-lab
    .\.claude\tools\shader-vision.ps1 water-lab -CompareRun water-lab
    .\.claude\tools\shader-vision.ps1 -SceneView
#>
param(
    [Parameter(Position = 0)]
    [string]$Job,

    [switch]$SceneView,

    # outputName of an earlier run to diff against; "same name" = compare with the previous capture.
    [string]$CompareRun,

    [ValidateRange(5, 900)]
    [int]$TimeoutSec = 240
)

$ErrorActionPreference = "Stop"
$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = (Resolve-Path (Join-Path $scriptDirectory "..\..")).Path
$visionRoot = Join-Path $projectRoot "Artifacts\ShaderVision"
$wsCall = Join-Path $scriptDirectory "unity-ws-call.mjs"

# PowerShell 5.1's Out-File -Encoding utf8 emits a BOM, which both Node's JSON.parse and
# JsonUtility choke on. Write plain UTF-8 instead.
function Write-Utf8NoBom {
    param([string]$Path, [string]$Text)

    [System.IO.File]::WriteAllText($Path, $Text, (New-Object System.Text.UTF8Encoding($false)))
}

function Set-JsonField {
    param([string]$Text, [string]$Name, [string]$Value)

    $pattern = '"' + [regex]::Escape($Name) + '"\s*:\s*"[^"]*"'
    $replacement = '"' + $Name + '": "' + $Value + '"'
    if ([regex]::IsMatch($Text, $pattern)) {
        return [regex]::Replace($Text, $pattern, $replacement, 1)
    }

    $brace = $Text.IndexOf("{")
    if ($brace -lt 0) { throw "Job file is not a JSON object." }
    return $Text.Insert($brace + 1, "`n  $replacement,")
}

if ($SceneView) {
    $menuPath = "Market/Debug/Shader Vision/Capture Scene View"
    $outputName = "sceneview"
    $runId = ""
}
else {
    if ([string]::IsNullOrWhiteSpace($Job)) {
        throw "Pass a job name (a preset under .claude/shader-vision/) or a path to a job JSON file, or use -SceneView."
    }

    $jobPath = $Job
    if (-not (Test-Path -LiteralPath $jobPath)) {
        $jobPath = Join-Path $projectRoot ".claude\shader-vision\$Job.json"
    }
    if (-not (Test-Path -LiteralPath $jobPath)) {
        throw "Job file not found: $Job"
    }

    $text = Get-Content -LiteralPath $jobPath -Raw
    $runId = [guid]::NewGuid().ToString("N")
    $text = Set-JsonField -Text $text -Name "runId" -Value $runId
    if ($PSBoundParameters.ContainsKey("CompareRun")) {
        $text = Set-JsonField -Text $text -Name "compareRun" -Value $CompareRun
    }

    $parsed = $text | ConvertFrom-Json
    $outputName = if ($parsed.outputName) { $parsed.outputName } else { "run" }

    New-Item -ItemType Directory -Force -Path $visionRoot | Out-Null
    Write-Utf8NoBom -Path (Join-Path $visionRoot "job.json") -Text $text
    $menuPath = "Market/Debug/Shader Vision/Run Job"
}

$reportPath = Join-Path $visionRoot "$outputName\report.json"
$previousStamp = if (Test-Path -LiteralPath $reportPath) { (Get-Item -LiteralPath $reportPath).LastWriteTimeUtc } else { [datetime]::MinValue }

Write-Host "Shader Vision -> $menuPath (out: $outputName)"

# Pass the params through a file: PowerShell 5.1 mangles inline JSON quoting when it hands
# arguments to a native executable, so unity-ws-call's '@file' form is the reliable path.
New-Item -ItemType Directory -Force -Path $visionRoot | Out-Null
$paramsFile = Join-Path $visionRoot "menu-params.json"
Write-Utf8NoBom -Path $paramsFile -Text (@{ menuPath = $menuPath } | ConvertTo-Json -Compress)
$response = & node $wsCall "execute_menu_item" "@Artifacts/ShaderVision/menu-params.json" --timeout ([string]($TimeoutSec * 1000))
if ($LASTEXITCODE -ne 0) {
    Write-Host $response
    throw "Unity did not execute the menu item. Run .\.claude\tools\mcp-doctor.ps1."
}

$deadline = (Get-Date).AddSeconds($TimeoutSec)
$report = $null
while ((Get-Date) -lt $deadline) {
    if (Test-Path -LiteralPath $reportPath) {
        $item = Get-Item -LiteralPath $reportPath
        if ($item.LastWriteTimeUtc -gt $previousStamp) {
            try {
                $candidate = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
                if ([string]::IsNullOrEmpty($runId) -or $candidate.runId -eq $runId) {
                    $report = $candidate
                    break
                }
            }
            catch {
                # Half-written file; try again on the next tick.
            }
        }
    }

    Start-Sleep -Milliseconds 400
}

if ($null -eq $report) {
    throw "No fresh report at $reportPath. Check the Unity console for [ShaderVision] errors."
}

Write-Host ""
Write-Host "status : $($report.status)"
if ($report.error) { Write-Host "error  : $($report.error)" }
if ($report.scene) { Write-Host "scene  : $($report.scene)" }
foreach ($warning in $report.warnings) { Write-Host "warn   : $warning" }
Write-Host ""

foreach ($shot in $report.shots) {
    $line = "{0,-22} lum {1:0.000} (p05 {2:0.00} p50 {3:0.00} p95 {4:0.00}) sd {5:0.000} detail {6:0.0000} black {7:0.0}% clip {8:0.0}%" -f `
        $shot.label, $shot.luminanceMean, $shot.luminanceP05, $shot.luminanceP50, $shot.luminanceP95, `
        $shot.luminanceStdDev, $shot.detail, $shot.blackPct, $shot.clippedPct
    Write-Host $line
    if ($shot.nonFinitePct -gt 0) { Write-Host ("  !! NaN/Inf pixels: {0:0.00}%" -f $shot.nonFinitePct) -ForegroundColor Red }
    if ($shot.magentaPct -gt 0.5) { Write-Host ("  !! magenta (error shader?) pixels: {0:0.00}%" -f $shot.magentaPct) -ForegroundColor Red }
    if ($shot.compared) {
        Write-Host ("  vs baseline: mean {0:0.0000}  max {1:0.000}  changed {2:0.0}%  -> {3}" -f `
            $shot.meanAbsDiff, $shot.maxAbsDiff, $shot.changedPct, $shot.diffFile)
    }
}

if ($report.sheet) {
    Write-Host ""
    Write-Host "sheet  : $(Join-Path $projectRoot ($report.sheet -replace '/', '\'))"
}

if ($report.status -ne "ok") { exit 1 }
