param(
    [int] $WaitSeconds = 20,
    [int] $Port = 8090
)

$ErrorActionPreference = "Stop"

$projectRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$serverPath = Join-Path $projectRoot "Packages\com.gamelovers.mcp-unity\Server~\build\index.js"
$wsModulePath = Join-Path $projectRoot "Packages\com.gamelovers.mcp-unity\Server~\node_modules\ws"
$callScript = Join-Path $projectRoot ".claude\tools\unity-ws-call.mjs"
$settingsPath = Join-Path $projectRoot "ProjectSettings\McpUnitySettings.json"

function Write-Status([string] $Name, [string] $Value) {
    Write-Host ("{0}: {1}" -f $Name, $Value)
}

Write-Status "Project" $projectRoot

if (-not (Test-Path -LiteralPath $serverPath)) {
    Write-Error "Missing MCP Unity node server: $serverPath"
    exit 1
}
Write-Status "Node server" "OK"

if (-not (Test-Path -LiteralPath $wsModulePath)) {
    Write-Error "Missing MCP Unity ws dependency: $wsModulePath. Open Unity once or run npm install in Packages/com.gamelovers.mcp-unity/Server~."
    exit 1
}
Write-Status "Node ws dependency" "OK"

if (-not (Test-Path -LiteralPath $settingsPath)) {
    Write-Error "Missing MCP Unity settings: $settingsPath"
    exit 1
}

$settings = Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json
Write-Status "Settings port" $settings.Port
Write-Status "Settings AutoStartServer" $settings.AutoStartServer

$nodeCommand = Get-Command node -ErrorAction SilentlyContinue
if ($null -eq $nodeCommand) {
    Write-Error "Node.js was not found on PATH."
    exit 1
}
Write-Status "Node" $nodeCommand.Source

$unityProcesses = Get-CimInstance Win32_Process |
    Where-Object {
        $_.Name -eq "Unity.exe" -and
        $_.CommandLine -like "*-projectpath*${projectRoot}*"
    }

if ($unityProcesses) {
    $ids = ($unityProcesses | ForEach-Object { $_.ProcessId }) -join ", "
    Write-Status "Unity Editor process" "OK ($ids)"
}
else {
    Write-Status "Unity Editor process" "not found for this project"
}

$deadline = (Get-Date).AddSeconds($WaitSeconds)
$lastError = $null

Push-Location -LiteralPath $projectRoot
try {
    do {
        $connection = Get-NetTCPConnection -LocalPort $Port -ErrorAction SilentlyContinue |
            Where-Object { $_.State -eq "Listen" } |
            Select-Object -First 1

        if ($connection) {
            Write-Status "TCP $Port" "listening (pid $($connection.OwningProcess))"
        }
        else {
            Write-Status "TCP $Port" "not listening yet"
        }

        $sceneInfoJson = & $nodeCommand.Source $callScript get_scene_info "{}" --timeout 10000 --client "Codex MCP Doctor" 2>&1
        if ($LASTEXITCODE -eq 0) {
            $sceneInfo = ($sceneInfoJson | Out-String) | ConvertFrom-Json
            Write-Status "Unity WebSocket" "OK"
            Write-Status "Active scene" "$($sceneInfo.activeScene.name) <$($sceneInfo.activeScene.path)>"
            exit 0
        }

        $lastError = ($sceneInfoJson | Out-String).Trim()
        if ((Get-Date) -lt $deadline) {
            Start-Sleep -Seconds 2
        }
    } while ((Get-Date) -lt $deadline)
}
finally {
    Pop-Location
}

Write-Error "Unity WebSocket probe failed after ${WaitSeconds}s. Last error: $lastError"
exit 1
