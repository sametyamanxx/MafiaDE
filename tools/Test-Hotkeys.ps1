param([string]$Executable = '')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if (!$Executable) { $Executable = Join-Path $root 'portable\MafiaDE.exe' }
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Windows.Forms, System.Drawing -TypeDefinition @'
using System;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Runtime.InteropServices;
public static class HotkeyTest {
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr window, int command);
    [DllImport("user32.dll")] public static extern void keybd_event(byte key, byte scan, uint flags, UIntPtr extra);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr window, uint message, IntPtr wp, IntPtr lp);
    [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr window, int id);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumCallback callback, IntPtr param);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out int pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr window, StringBuilder title, int max);
    private delegate bool EnumCallback(IntPtr window, IntPtr param);
    private static Form fixture;
    private static Thread fixtureThread;
    public static IntPtr FindWindow(int processId) {
        IntPtr result = IntPtr.Zero;
        EnumWindows(delegate(IntPtr h, IntPtr p) {
            int pid; GetWindowThreadProcessId(h, out pid);
            var title = new StringBuilder(256);
            GetWindowText(h, title, title.Capacity);
            if (pid == processId && title.ToString() == "MafiaDE") {
                result = h; return false;
            }
            return true;
        }, IntPtr.Zero);
        return result;
    }
    public static IntPtr StartFixture() {
        var ready = new ManualResetEvent(false);
        IntPtr handle = IntPtr.Zero;
        fixtureThread = new Thread(delegate() {
            fixture = new Form {
                Text = "MafiaDE borderless full-screen TEST fixture",
                FormBorderStyle = FormBorderStyle.None,
                Bounds = Screen.PrimaryScreen.Bounds,
                BackColor = System.Drawing.Color.FromArgb(18, 19, 20),
                TopMost = true, ShowInTaskbar = false
            };
            fixture.Shown += delegate { handle = fixture.Handle; ready.Set(); };
            Application.Run(fixture);
        });
        fixtureThread.IsBackground = true;
        fixtureThread.SetApartmentState(ApartmentState.STA);
        fixtureThread.Start();
        if (!ready.WaitOne(10000)) throw new Exception("Full-screen test fixture did not start.");
        ready.Dispose();
        return handle;
    }
    public static bool OccupyInsert() {
        return (bool)fixture.Invoke(new Func<bool>(delegate {
            return RegisterHotKey(fixture.Handle, 0x5445, 0x4000, 0x2D);
        }));
    }
    public static void ReleaseInsert() {
        fixture.Invoke(new Action(delegate { UnregisterHotKey(fixture.Handle, 0x5445); }));
    }
    public static void FocusFixture() {
        fixture.Invoke(new Action(delegate {
            fixture.Show(); fixture.Activate(); SetForegroundWindow(fixture.Handle);
        }));
    }
    public static void StopFixture() {
        if (fixture != null && !fixture.IsDisposed)
            fixture.BeginInvoke(new Action(delegate { fixture.Close(); }));
        if (fixtureThread != null) fixtureThread.Join(5000);
    }
}
'@
function Wait-Until([scriptblock]$Condition, [string]$Message) {
    $deadline = [DateTime]::UtcNow.AddSeconds(8)
    while ([DateTime]::UtcNow -lt $deadline) {
        if (& $Condition) { return }
        Start-Sleep -Milliseconds 30
    }
    throw $Message
}
function Press-Insert([int]$Repeat = 0) {
    [HotkeyTest]::keybd_event(0x2D, 0x52, 1, [UIntPtr]::Zero)
    try {
        Start-Sleep -Milliseconds 120
        for ($i = 0; $i -lt $Repeat; $i++) {
            [HotkeyTest]::keybd_event(0x2D, 0x52, 1, [UIntPtr]::Zero)
            Start-Sleep -Milliseconds 50
        }
    } finally { [HotkeyTest]::keybd_event(0x2D, 0x52, 3, [UIntPtr]::Zero) }
    Start-Sleep -Milliseconds 180
}
function Focus-Fixture {
    # A background test runner is subject to Windows' foreground lock. Simulate
    # the user's Alt input before focusing the test app; never attach input queues.
    [HotkeyTest]::keybd_event(0x12, 0x38, 0, [UIntPtr]::Zero)
    [HotkeyTest]::keybd_event(0x12, 0x38, 2, [UIntPtr]::Zero)
    [HotkeyTest]::FocusFixture()
    Wait-Until { [HotkeyTest]::GetForegroundWindow() -eq $fixture } 'Test fixture did not get focus.'
}
function Start-Trainer([switch]$Hidden) {
    if ($Hidden) { $p = Start-Process $Executable -ArgumentList '--hidden' -PassThru }
    else { $p = Start-Process $Executable -PassThru }
    if (!$p.WaitForInputIdle(15000)) { throw 'Trainer did not become idle.' }
    Wait-Until { [HotkeyTest]::FindWindow($p.Id) -ne [IntPtr]::Zero } 'Trainer window not found.'
    return $p
}
function Stop-Trainer($Process, [IntPtr]$Window) {
    if ($Process -and !$Process.HasExited) {
        [HotkeyTest]::PostMessage($Window, 0x10, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
        if (!$Process.WaitForExit(10000)) { throw 'Trainer did not shut down.' }
    }
}
$process = $null
$window = [IntPtr]::Zero
$occupied = $false
$fixture = [HotkeyTest]::StartFixture()
try {
    $process = Start-Trainer
    $window = [HotkeyTest]::FindWindow($process.Id)
    # Regression: the trainer is WS_VISIBLE but covered by another full-screen app.
    Focus-Fixture
    if (![HotkeyTest]::IsWindowVisible($window)) { throw 'Initial trainer window must be visible.' }
    Press-Insert
    Wait-Until { [HotkeyTest]::IsWindowVisible($window) -and
        [HotkeyTest]::GetForegroundWindow() -eq $window } 'REGRESSION: Insert hid a covered window or failed to bring it forward.'
    Write-Output 'PASS: Insert brings a covered visible trainer above the full-screen fixture.'
    Press-Insert -Repeat 5
    Wait-Until { ![HotkeyTest]::IsWindowVisible($window) } 'Held/repeated Insert toggled more than once.'
    Write-Output 'PASS: held/repeated Insert toggles only once.'
    Focus-Fixture
    Press-Insert
    Wait-Until { [HotkeyTest]::IsWindowVisible($window) -and
        [HotkeyTest]::GetForegroundWindow() -eq $window } 'Insert did not show a hidden trainer from another foreground app.'
    Write-Output 'PASS: global Insert shows and activates the hidden trainer.'
    [HotkeyTest]::ShowWindow($window, 6) | Out-Null
    Focus-Fixture
    Press-Insert
    Wait-Until { ![HotkeyTest]::IsIconic($window) -and
        [HotkeyTest]::GetForegroundWindow() -eq $window } 'Insert did not restore a minimized trainer.'
    Write-Output 'PASS: Insert restores a minimized trainer.'
    [HotkeyTest]::ShowWindow($window, 0) | Out-Null
    $second = Start-Process $Executable -PassThru
    try {
        if (!$second.WaitForExit(10000)) { throw 'Second launch did not signal the existing instance.' }
        Wait-Until { [HotkeyTest]::IsWindowVisible($window) -and
            [HotkeyTest]::GetForegroundWindow() -eq $window } 'Second launch did not reveal the existing trainer.'
    } finally { $second.Dispose() }
    Write-Output 'PASS: launching the EXE again reveals the existing window.'
    Stop-Trainer $process $window
    $process.Dispose(); $process = $null

    $process = Start-Trainer -Hidden
    $window = [HotkeyTest]::FindWindow($process.Id)
    Wait-Until { ![HotkeyTest]::IsWindowVisible($window) } '--hidden startup failed.'
    Focus-Fixture
    Press-Insert
    Wait-Until { [HotkeyTest]::IsWindowVisible($window) -and
        [HotkeyTest]::GetForegroundWindow() -eq $window } 'Insert did not show a --hidden startup.'
    Write-Output 'PASS: global Insert works after --hidden startup and re-registers after shutdown.'
    Stop-Trainer $process $window
    $process.Dispose(); $process = $null

    $occupied = [HotkeyTest]::OccupyInsert()
    if (!$occupied) { throw 'Could not reserve Insert for the registration-conflict test.' }
    $process = Start-Trainer -Hidden
    $window = [HotkeyTest]::FindWindow($process.Id)
    Focus-Fixture
    Press-Insert
    Wait-Until { [HotkeyTest]::IsWindowVisible($window) -and
        [HotkeyTest]::GetForegroundWindow() -eq $window } 'Polling fallback failed when RegisterHotKey was unavailable.'
    Write-Output 'PASS: polling fallback works when another app has registered Insert.'
    Write-Output 'All hotkey integration tests passed. Actual exclusive-fullscreen Mafia gameplay was not tested.'
} finally {
    if ($process) { Stop-Trainer $process $window; $process.Dispose() }
    if ($occupied) { [HotkeyTest]::ReleaseInsert() }
    [HotkeyTest]::StopFixture()
}
