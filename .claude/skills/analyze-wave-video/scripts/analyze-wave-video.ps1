param(
    [string]$VideoPath = "",
    [switch]$SelfTest,
    [string]$OutputDirectory = "",
    [double]$Start = 0,
    [double]$Duration = 20,
    [double]$SampleFps = 10,
    [int]$AnalysisWidth = 640,
    [ValidateRange(12, 144)]
    [int]$ReviewFrames = 72,
    [string]$Roi = "",
    [string]$Transect = "",
    [double]$MetersPerPixel = 0,
    [switch]$NoStabilization
)

$ErrorActionPreference = "Stop"
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$userRoot = [Environment]::GetFolderPath("UserProfile")
$runtimeRoot = Join-Path $userRoot ".cache\wave-video-analysis\python"
$bundledPython = Join-Path $userRoot ".cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe"

function Format-InvariantNumber([double]$Value) {
    return $Value.ToString([Globalization.CultureInfo]::InvariantCulture)
}

if (Test-Path -LiteralPath $bundledPython) {
    $python = $bundledPython
} else {
    $pythonCommand = Get-Command python -ErrorAction SilentlyContinue
    if ($null -eq $pythonCommand) {
        throw "Python was not found. Run install-runtime.ps1 from a Codex Desktop session or install Python 3.10+."
    }
    $python = $pythonCommand.Source
}

if (-not (Test-Path -LiteralPath (Join-Path $runtimeRoot "cv2"))) {
    & (Join-Path $scriptRoot "install-runtime.ps1") -RuntimeRoot $runtimeRoot
}

if (-not $SelfTest) {
    if ([string]::IsNullOrWhiteSpace($VideoPath)) {
        throw "Provide -VideoPath, or use -SelfTest to validate the analyzer against synthetic clips."
    }
    if (-not (Test-Path -LiteralPath $VideoPath)) {
        throw "Video file was not found: $VideoPath"
    }
    $resolvedVideo = (Resolve-Path -LiteralPath $VideoPath).Path
}

$oldPythonPath = $env:PYTHONPATH
$oldDontWriteBytecode = $env:PYTHONDONTWRITEBYTECODE
if ([string]::IsNullOrWhiteSpace($oldPythonPath)) {
    $env:PYTHONPATH = $runtimeRoot
} else {
    $env:PYTHONPATH = "$runtimeRoot;$oldPythonPath"
}
$env:PYTHONDONTWRITEBYTECODE = "1"

if ($SelfTest) {
    $arguments = @((Join-Path $scriptRoot "selftest_wave_analyzer.py"))
    if (-not [string]::IsNullOrWhiteSpace($OutputDirectory)) {
        $arguments += @("--keep", $OutputDirectory)
    }
} else {
    $arguments = @(
        (Join-Path $scriptRoot "analyze_wave_video.py"),
        $resolvedVideo,
        "--start", (Format-InvariantNumber $Start),
        "--duration", (Format-InvariantNumber $Duration),
        "--sample-fps", (Format-InvariantNumber $SampleFps),
        "--analysis-width", $AnalysisWidth.ToString([Globalization.CultureInfo]::InvariantCulture),
        "--review-frames", $ReviewFrames.ToString([Globalization.CultureInfo]::InvariantCulture)
    )

    if (-not [string]::IsNullOrWhiteSpace($OutputDirectory)) {
        $arguments += @("--output", $OutputDirectory)
    }
    if (-not [string]::IsNullOrWhiteSpace($Roi)) {
        $arguments += @("--roi", $Roi)
    }
    if (-not [string]::IsNullOrWhiteSpace($Transect)) {
        $arguments += @("--transect", $Transect)
    }
    if ($MetersPerPixel -gt 0) {
        $arguments += @("--meters-per-pixel", (Format-InvariantNumber $MetersPerPixel))
    }
    if ($NoStabilization) {
        $arguments += "--no-stabilization"
    }
}

try {
    & $python @arguments
    if ($LASTEXITCODE -ne 0) {
        if ($SelfTest) {
            throw "Wave analyzer self-test failed with exit code $LASTEXITCODE."
        }
        throw "Wave video analysis failed with exit code $LASTEXITCODE."
    }
} finally {
    $env:PYTHONPATH = $oldPythonPath
    $env:PYTHONDONTWRITEBYTECODE = $oldDontWriteBytecode
}
