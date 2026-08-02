param(
    [string]$RuntimeRoot = ""
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($RuntimeRoot)) {
    $userRoot = [Environment]::GetFolderPath("UserProfile")
    $RuntimeRoot = Join-Path $userRoot ".cache\wave-video-analysis\python"
}

function Resolve-Python {
    $userRoot = [Environment]::GetFolderPath("UserProfile")
    $bundled = Join-Path $userRoot ".cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe"
    if (Test-Path -LiteralPath $bundled) {
        return $bundled
    }

    foreach ($name in @("python", "py")) {
        $command = Get-Command $name -ErrorAction SilentlyContinue
        if ($null -ne $command) {
            return $command.Source
        }
    }

    throw "Python 3.10 or newer is required. Install Python or run this from Codex Desktop."
}

$python = Resolve-Python
New-Item -ItemType Directory -Force -Path $RuntimeRoot | Out-Null

& $python -m pip install --disable-pip-version-check --upgrade --target $RuntimeRoot `
    "numpy==2.5.1" `
    "opencv-python-headless==5.0.0.93" `
    "imageio-ffmpeg==0.6.0" `
    "PyYAML==6.0.3"

if ($LASTEXITCODE -ne 0) {
    throw "Wave video analysis runtime installation failed."
}

Write-Output "Wave video analysis runtime installed at: $RuntimeRoot"
