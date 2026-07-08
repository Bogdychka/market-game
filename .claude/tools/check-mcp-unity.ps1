param(
    [int] $WaitSeconds = 20
)

$ErrorActionPreference = "Stop"

$doctorScript = Join-Path $PSScriptRoot "mcp-doctor.ps1"
& powershell -ExecutionPolicy Bypass -File $doctorScript -WaitSeconds $WaitSeconds
exit $LASTEXITCODE
