param(
    [ValidateSet('capture','click','seek','dragseek','dragwindow','double','right','zoom','shift','space')][string]$Action = 'capture',
    [string]$Output = '',
    [int]$ProcessId = 0,
    [int]$ClickX = 0,
    [int]$ClickY = 0,
    [ValidateRange(1,2)][int]$ClickCount = 1
)

if ([string]::IsNullOrWhiteSpace($Output)) { $Output = Join-Path $PSScriptRoot '..\Logs\ui-probe.png' }
$Output = [IO.Path]::GetFullPath($Output)
$null = New-Item -ItemType Directory -Force -Path ([IO.Path]::GetDirectoryName($Output))
Add-Type -AssemblyName System.Drawing
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class UiProbeNative {
    [StructLayout(LayoutKind.Sequential)] public struct Pt { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] public struct Rc { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int command);
    [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr h, ref Pt point);
    [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h, out Rc rect);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(int flags, int dx, int dy, int data, IntPtr extra);
    [DllImport("user32.dll")] public static extern void keybd_event(byte key, byte scan, int flags, IntPtr extra);
}
'@

$process = if ($ProcessId -gt 0) { Get-Process -Id $ProcessId -ErrorAction Stop } else { Get-Process TimelineVideoStudio -ErrorAction Stop | Select-Object -First 1 }
$handle = $process.MainWindowHandle
if ($handle -eq [IntPtr]::Zero) { throw 'No app window' }
Start-Sleep -Milliseconds 300
$origin = [UiProbeNative+Pt]::new()
[UiProbeNative]::ClientToScreen($handle, [ref]$origin) | Out-Null
$rect = [UiProbeNative+Rc]::new()
[UiProbeNative]::GetClientRect($handle, [ref]$rect) | Out-Null
$width = $rect.Right - $rect.Left
$height = $rect.Bottom - $rect.Top

function Move-Click([int]$x, [int]$y, [int]$count, [int]$button) {
    $position = [IntPtr](($y -shl 16) -bor ($x -band 0xffff))
    $down = if ($button -eq 8) { 0x204 } else { 0x201 }
    $up = if ($button -eq 8) { 0x205 } else { 0x202 }
    for ($i = 0; $i -lt $count; $i++) {
        $currentDown = if ($button -eq 2 -and $i -eq 1) { 0x203 } else { $down }
        [UiProbeNative]::PostMessage($handle, $currentDown, [IntPtr]::Zero, $position) | Out-Null
        Start-Sleep -Milliseconds 40
        [UiProbeNative]::PostMessage($handle, $up, [IntPtr]::Zero, $position) | Out-Null
        Start-Sleep -Milliseconds 80
    }
}

switch ($Action) {
    'click' { Move-Click $ClickX $ClickY $ClickCount 2 }
    'seek' { Move-Click 130 ($height - 252) 1 2 }
    'dragseek' {
        $y = $height - 252
        $start = [IntPtr](($y -shl 16) -bor 130)
        $end = [IntPtr](($y -shl 16) -bor 260)
        [UiProbeNative]::PostMessage($handle, 0x201, [IntPtr]1, $start) | Out-Null
        Start-Sleep -Milliseconds 80
        [UiProbeNative]::PostMessage($handle, 0x200, [IntPtr]1, $end) | Out-Null
        Start-Sleep -Milliseconds 80
        [UiProbeNative]::PostMessage($handle, 0x202, [IntPtr]::Zero, $end) | Out-Null
    }
    'dragwindow' {
        $start = [IntPtr]((115 -shl 16) -bor ($width - 710))
        $end = [IntPtr]((245 -shl 16) -bor ($width - 1010))
        [UiProbeNative]::PostMessage($handle, 0x201, [IntPtr]1, $start) | Out-Null
        Start-Sleep -Milliseconds 90
        [UiProbeNative]::PostMessage($handle, 0x200, [IntPtr]1, $end) | Out-Null
        Start-Sleep -Milliseconds 90
        [UiProbeNative]::PostMessage($handle, 0x202, [IntPtr]::Zero, $end) | Out-Null
    }
    'double' { Move-Click 110 ($height - 220) 2 2 }
    'right' { Move-Click 310 ($height - 190) 1 8 }
    'zoom' {
        $position = [IntPtr]((($origin.Y + $height - 250) -shl 16) -bor (($origin.X + 500) -band 0xffff))
        [UiProbeNative]::PostMessage($handle, 0x20A, [IntPtr]((120 -shl 16) -bor 8), $position) | Out-Null
    }
    'shift' {
        $position = [IntPtr]((($origin.Y + $height - 250) -shl 16) -bor (($origin.X + 500) -band 0xffff))
        [UiProbeNative]::PostMessage($handle, 0x20A, [IntPtr](((-120 -band 0xffff) -shl 16) -bor 4), $position) | Out-Null
    }
    'space' {
        [UiProbeNative]::PostMessage($handle, 0x100, [IntPtr]0x20, [IntPtr]::Zero) | Out-Null
        [UiProbeNative]::PostMessage($handle, 0x101, [IntPtr]0x20, [IntPtr]::Zero) | Out-Null
    }
}

Start-Sleep -Milliseconds 450
$bitmap = [System.Drawing.Bitmap]::new($width, $height)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
if ($Action -eq 'capture') {
    $dc = $graphics.GetHdc()
    try { [UiProbeNative]::PrintWindow($handle, $dc, 2) | Out-Null }
    finally { $graphics.ReleaseHdc($dc) }
} else {
    $graphics.CopyFromScreen($origin.X, $origin.Y, 0, 0, $bitmap.Size)
}
$bitmap.Save($Output)
$graphics.Dispose()
$bitmap.Dispose()
Write-Output $Output
