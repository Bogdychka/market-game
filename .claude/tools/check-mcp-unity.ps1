$ErrorActionPreference = "Stop"

$projectRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$serverPath = Join-Path $projectRoot "Packages\com.gamelovers.mcp-unity\Server~\build\index.js"
$callScript = Join-Path $projectRoot ".claude\tools\unity-ws-call.mjs"

Write-Host "Project: $projectRoot"

if (-not (Test-Path -LiteralPath $serverPath)) {
    Write-Error "Missing MCP Unity node server: $serverPath"
    exit 1
}
Write-Host "Node server: OK"

$nodeCommand = Get-Command node -ErrorAction SilentlyContinue
if ($null -eq $nodeCommand) {
    Write-Error "Node.js was not found on PATH."
    exit 1
}
Write-Host "Node: $($nodeCommand.Source)"

Push-Location -LiteralPath $projectRoot
try {
    $sceneInfoJson = & $nodeCommand.Source $callScript get_scene_info "{}" --timeout 10000
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Unity WebSocket probe failed."
        exit $LASTEXITCODE
    }

    $sceneInfo = $sceneInfoJson | ConvertFrom-Json
    Write-Host "Unity WebSocket: OK"
    Write-Host "Active scene: $($sceneInfo.activeScene.name) <$($sceneInfo.activeScene.path)>"
}
finally {
    Pop-Location
}
