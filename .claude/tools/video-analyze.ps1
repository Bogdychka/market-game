param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$VideoPath,

    [string]$OutputDirectory,

    [ValidateRange(1, 100)]
    [int]$Samples = 12,

    [ValidateRange(1, 10)]
    [int]$Columns = 4
)

$ErrorActionPreference = "Stop"
$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Resolve-Path (Join-Path $scriptDirectory "..\..")
$resolvedVideo = Resolve-Path -LiteralPath $VideoPath
$pythonCandidates = @(
    $env:CODEX_PYTHON,
    "C:\Users\bogre\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe",
    (Get-Command python.exe -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty Source)
) | Where-Object { $_ -and (Test-Path -LiteralPath $_) }

if ($pythonCandidates.Count -eq 0) {
    throw "Python 3 was not found. Set CODEX_PYTHON to a Python executable."
}

$python = $pythonCandidates[0]
$dependencyDirectory = Join-Path $env:LOCALAPPDATA "CodexVideoTools\python"
New-Item -ItemType Directory -Force -Path $dependencyDirectory | Out-Null
$env:PYTHONPATH = $dependencyDirectory

$importExitCode = 0
try {
    & $python -c "import cv2" 2>&1 | Out-Null
    $importExitCode = $LASTEXITCODE
}
catch {
    $importExitCode = 1
}

if ($importExitCode -ne 0) {
    Write-Host "Installing the local video decoder into $dependencyDirectory ..."
    & $python -m pip install --disable-pip-version-check --target $dependencyDirectory opencv-python-headless
    if ($LASTEXITCODE -ne 0) {
        throw "Could not install opencv-python-headless."
    }
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $videoName = [System.IO.Path]::GetFileNameWithoutExtension($resolvedVideo.Path)
    $safeName = $videoName -replace '[^A-Za-z0-9._-]', '_'
    $OutputDirectory = Join-Path $projectRoot "Artifacts\VideoAnalysis\$safeName"
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$analyzer = Join-Path $scriptDirectory "video_analyze.py"
& $python $analyzer $resolvedVideo.Path --output $OutputDirectory --samples $Samples --columns $Columns
if ($LASTEXITCODE -ne 0) {
    throw "Video analysis failed with exit code $LASTEXITCODE."
}
