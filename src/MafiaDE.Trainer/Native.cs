using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace MafiaDE.Trainer;

internal static class Native
{
    public const uint ProcessAccess = 0x0008 | 0x0010 | 0x0020 | 0x0400 | 0x00100000;
    public const uint CommitReserve = 0x3000, ExecuteReadWrite = 0x40;

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern SafeProcessHandle OpenProcess(uint access, bool inherit, int pid);
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool ReadProcessMemory(SafeProcessHandle process, nint address,
        [Out] byte[] buffer, nuint size, out nuint read);
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool WriteProcessMemory(SafeProcessHandle process, nint address,
        byte[] buffer, nuint size, out nuint written);
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool VirtualProtectEx(SafeProcessHandle process, nint address,
        nuint size, uint protection, out uint oldProtection);
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern nint VirtualAllocEx(SafeProcessHandle process, nint address,
        nuint size, uint allocationType, uint protection);
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool VirtualFreeEx(SafeProcessHandle process, nint address, nuint size, uint freeType);
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool FlushInstructionCache(SafeProcessHandle process, nint address, nuint size);
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern nuint VirtualQueryEx(SafeProcessHandle process, nint address,
        out MemoryInfo info, nuint size);
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern nint OpenThread(uint access, bool inherit, uint threadId);
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern uint SuspendThread(nint thread);
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern uint ResumeThread(nint thread);
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool GetThreadContext(nint thread, nint context);
    [DllImport("kernel32.dll")]
    public static extern bool CloseHandle(nint handle);
    [DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")]
    public static extern nint GetForegroundWindow();
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RegisterHotKey(nint window, int id, uint modifiers, uint key);
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnregisterHotKey(nint window, int id);
    [DllImport("user32.dll")]
    public static extern bool ShowWindow(nint window, int command);
    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(nint window);
    [DllImport("user32.dll")]
    public static extern bool AllowSetForegroundWindow(int processId);
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetWindowPos(nint window, nint insertAfter,
        int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(nint window, out int processId);
    [DllImport("user32.dll")]
    public static extern bool ReleaseCapture();
    [DllImport("user32.dll")]
    public static extern nint SendMessage(nint window, uint message, nint wParam, nint lParam);

    public static Win32Exception Error(string operation) =>
        new(Marshal.GetLastWin32Error(), operation);

    [StructLayout(LayoutKind.Sequential)]
    public struct MemoryInfo
    {
        public nint BaseAddress, AllocationBase;
        public uint AllocationProtect;
        public ushort PartitionId;
        public nuint RegionSize;
        public uint State, Protect, Type;
    }
}
