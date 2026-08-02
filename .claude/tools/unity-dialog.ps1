<#
.SYNOPSIS
    Lists or dismisses the modal dialogs that freeze the Unity Editor (and with it the MCP bridge).

.DESCRIPTION
    Unity draws its dialogs with IMGUI, so they are invisible to UI Automation - there is no button
    element to invoke. They do respond to the keyboard, which is what this script sends.

    Symptom this solves: every MCP call times out while the Unity process burns no CPU. That is a
    modal dialog waiting for a click, not a dead bridge. The usual cause is an asset or scene file
    edited on disk while the Editor has it open ("The open scene(s) have been modified externally").

.PARAMETER Action
    List    - report the visible Unity windows and whether a dialog is blocking (default).
    Accept  - send Enter, the dialog's default button (Reload / OK / Yes).
    Cancel  - send Escape, the dialog's cancel button (Ignore / Cancel / No).

.PARAMETER TitlePattern
    Only act when the blocking dialog's title matches this regex. Use it to make an automated
    dismissal specific instead of blind.

.EXAMPLE
    powershell -File .claude/tools/unity-dialog.ps1
    powershell -File .claude/tools/unity-dialog.ps1 -Action Accept -TitlePattern 'modified externally'
#>
[CmdletBinding()]
param(
    [ValidateSet('List', 'Accept', 'Cancel')]
    [string]$Action = 'List',
    [string]$TitlePattern = ''
)

$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Windows.Forms
# AppActivate, not SetForegroundWindow: Windows' foreground lock lets the call succeed while the
# keystroke goes nowhere, which is exactly how this looked when it silently did nothing.
Add-Type -AssemblyName Microsoft.VisualBasic
Add-Type -TypeDefinition @'
using System;
using System.Text;
using System.Runtime.InteropServices;

public static class UnityDialogNative
{
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);
}
'@

function Get-UnityWindow {
    $editor = Get-Process Unity -ErrorAction SilentlyContinue |
        Where-Object { $_.MainWindowTitle -ne '' } |
        Select-Object -First 1

    if ($null -eq $editor) {
        return @()
    }

    $found = New-Object System.Collections.ArrayList
    $callback = [UnityDialogNative+EnumWindowsProc] {
        param($handle, $lParam)

        $owner = 0
        [UnityDialogNative]::GetWindowThreadProcessId($handle, [ref]$owner) | Out-Null
        if ($owner -ne $editor.Id -or -not [UnityDialogNative]::IsWindowVisible($handle)) {
            return $true
        }

        $buffer = New-Object System.Text.StringBuilder 512
        [UnityDialogNative]::GetWindowText($handle, $buffer, 512) | Out-Null
        if ($buffer.Length -gt 0) {
            $null = $found.Add([pscustomobject]@{
                Handle   = $handle
                Title    = $buffer.ToString()
                IsEditor = $buffer.ToString() -eq $editor.MainWindowTitle
            })
        }

        return $true
    }

    [UnityDialogNative]::EnumWindows($callback, [IntPtr]::Zero) | Out-Null
    return $found
}

function Test-EditorBlocked {
    param([System.Diagnostics.Process]$Editor)

    # A blocking modal leaves the Editor pumping no work at all; a busy Editor still burns CPU.
    $before = $Editor.CPU
    Start-Sleep -Milliseconds 1500
    $Editor.Refresh()
    return ($Editor.CPU - $before) -lt 0.02
}

$windows = Get-UnityWindow
if ($windows.Count -eq 0) {
    Write-Host 'Unity Editor is not running (or has no window).'
    exit 2
}

$dialogs = @($windows | Where-Object { -not $_.IsEditor })
$editorProcess = Get-Process Unity -ErrorAction SilentlyContinue |
    Where-Object { $_.MainWindowTitle -ne '' } |
    Select-Object -First 1

if ($Action -eq 'List') {
    foreach ($window in $windows) {
        $kind = if ($window.IsEditor) { 'editor' } else { 'DIALOG' }
        Write-Host ("{0,-7} {1}" -f $kind, $window.Title)
    }

    if ($dialogs.Count -eq 0) {
        Write-Host 'No dialog is open.'
        exit 0
    }

    if (Test-EditorBlocked -Editor $editorProcess) {
        Write-Host 'Editor is blocked by the dialog above (flat CPU) - MCP calls will time out.'
        exit 1
    }

    Write-Host 'Editor is still running; the window above is not blocking it.'
    exit 0
}

if ($dialogs.Count -eq 0) {
    Write-Host 'No dialog to dismiss.'
    exit 0
}

$target = $dialogs[0]
if ($TitlePattern -ne '' -and $target.Title -notmatch $TitlePattern) {
    Write-Host ("Dialog '{0}' does not match pattern '{1}' - refusing to dismiss it." -f $target.Title, $TitlePattern)
    exit 3
}

[UnityDialogNative]::SetForegroundWindow($target.Handle) | Out-Null
[Microsoft.VisualBasic.Interaction]::AppActivate($editorProcess.Id)
Start-Sleep -Milliseconds 400

$key = if ($Action -eq 'Accept') { '{ENTER}' } else { '{ESC}' }
[System.Windows.Forms.SendKeys]::SendWait($key)
Start-Sleep -Milliseconds 750

$remaining = @(Get-UnityWindow | Where-Object { -not $_.IsEditor -and $_.Title -eq $target.Title })
if ($remaining.Count -gt 0) {
    Write-Host ("Dialog '{0}' is still open after sending {1}." -f $target.Title, $Action)
    exit 4
}

Write-Host ("Dismissed '{0}' with {1}." -f $target.Title, $Action)
exit 0
