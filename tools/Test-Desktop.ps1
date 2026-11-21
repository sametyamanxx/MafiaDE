param([string]$Executable = '', [string]$Screenshot = '')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if (!$Executable) { $Executable = Join-Path $root 'portable\MafiaDE.exe' }
if (!$Screenshot) { $Screenshot = Join-Path $env:LOCALAPPDATA 'Temp\opencode\MafiaDE-preview.png' }
Add-Type -AssemblyName System.Drawing
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class DesktopTest {
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr window, IntPtr dc, uint flags);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] public static extern void keybd_event(byte key, byte scan, uint flags, UIntPtr extra);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr window, uint message, IntPtr wp, IntPtr lp);
    public struct Rect { public int Left, Top, Right, Bottom; }
}
'@
$process = Start-Process $Executable -PassThru
try {
    if (!$process.WaitForInputIdle(20000)) { throw 'UI did not become idle.' }
    Start-Sleep -Milliseconds 800
    $process.Refresh()
    $window = $process.MainWindowHandle
    if ($window -eq [IntPtr]::Zero) { throw 'Window not found.' }
    if ($process.MainWindowTitle -ne 'MafiaDE') { throw 'Wrong program name.' }
    if (![DesktopTest]::IsWindowVisible($window)) { throw 'Initial window is hidden.' }
    [DesktopTest]::SetForegroundWindow($window) | Out-Null
    $rect = New-Object DesktopTest+Rect
    [DesktopTest]::GetWindowRect($window, [ref]$rect) | Out-Null
    $bitmap = New-Object Drawing.Bitmap(($rect.Right - $rect.Left), ($rect.Bottom - $rect.Top))
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    $dc = $graphics.GetHdc()
    try { [DesktopTest]::PrintWindow($window, $dc, 2) | Out-Null }
    finally { $graphics.ReleaseHdc($dc); $graphics.Dispose() }
    try { $bitmap.Save($Screenshot, [Drawing.Imaging.ImageFormat]::Png) }
    finally { $bitmap.Dispose() }
    Write-Output "PASS: title, startup and window capture ($Screenshot)"
    foreach ($expectedVisible in @($false, $true)) {
        [DesktopTest]::keybd_event(0x2D, 0x52, 0, [UIntPtr]::Zero)
        Start-Sleep -Milliseconds 120
        [DesktopTest]::keybd_event(0x2D, 0x52, 2, [UIntPtr]::Zero)
        Start-Sleep -Milliseconds 250
        if ([DesktopTest]::IsWindowVisible($window) -ne $expectedVisible) {
            throw "Insert toggle failed. Expected visible: $expectedVisible"
        }
        Write-Output "PASS: Insert -> visible=$expectedVisible"
    }
    [DesktopTest]::PostMessage($window, 0x10, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
    if (!$process.WaitForExit(10000)) { throw 'Window did not close.' }
    if ($process.ExitCode -ne 0) { throw "UI exit code: $($process.ExitCode)" }
    Write-Output 'PASS: clean shutdown'
} finally {
    if (!$process.HasExited) {
        [DesktopTest]::PostMessage($window, 0x10, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
    }
    $process.Dispose()
}
