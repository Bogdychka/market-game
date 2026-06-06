$ErrorActionPreference = "Stop"

$projectRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$embeddedPackagePath = Join-Path $projectRoot "Packages\com.gamelovers.mcp-unity\Server~\build\index.js"
$packagePattern = Join-Path $projectRoot "Library\PackageCache\com.gamelovers.mcp-unity@*\Server~\build\index.js"

$serverPath = $null

if (Test-Path -LiteralPath $embeddedPackagePath) {
    $serverPath = Get-Item -LiteralPath $embeddedPackagePath
}

if ($null -eq $serverPath) {
    $serverPath = Get-ChildItem -Path $packagePattern -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
}

if ($null -eq $serverPath) {
    Write-Error "MCP Unity server was not found. Open Unity once so Package Manager resolves com.gamelovers.mcp-unity, then use Tools > MCP Unity > Server Window > Force Install Server if needed."
    exit 1
}

$nodeCommand = Get-Command node -ErrorAction SilentlyContinue
if ($null -eq $nodeCommand) {
    Write-Error "Node.js was not found on PATH. Install Node.js or set up Unity MCP from Tools > MCP Unity > Server Window."
    exit 1
}

# mcpUnity.js reads ProjectSettings/McpUnitySettings.json from process.cwd().
Set-Location -LiteralPath $projectRoot

$env:UNITY_HOST = "127.0.0.1"

& $nodeCommand.Source $serverPath.FullName
exit $LASTEXITCODE
