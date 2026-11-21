using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace MafiaDE.Trainer;

internal sealed class GameMemory : IDisposable
{
    public Process Process { get; }
    private readonly SafeProcessHandle handle;
    public bool Alive
    {
        get { try { return !Process.HasExited; } catch { return false; } }
    }

    public GameMemory(Process process)
    {
        Process = process;
        handle = Native.OpenProcess(Native.ProcessAccess, false, process.Id);
        if (handle.IsInvalid)
        {
            handle.Dispose();
            throw Native.Error("Oyuna erişilemedi. Oyun yönetici olarak açıksa MafiaDE'yi de yönetici olarak aç.");
        }
    }

    public byte[] Read(nint address, int count)
    {
        byte[] bytes = new byte[count];
        if (!Native.ReadProcessMemory(handle, address, bytes, (nuint)count, out var read) || read != (nuint)count)
            throw Native.Error("Oyun belleği okunamadı.");
        return bytes;
    }

    public void Write(nint address, byte[] bytes)
    {
        if (!Native.WriteProcessMemory(handle, address, bytes, (nuint)bytes.Length, out var written)
            || written != (nuint)bytes.Length)
            throw Native.Error("Oyun belleğine yazılamadı.");
    }

    public void WriteCode(nint address, byte[] bytes)
    {
        if (!Native.VirtualProtectEx(handle, address, (nuint)bytes.Length, Native.ExecuteReadWrite, out uint old))
            throw Native.Error("Bellek koruması değiştirilemedi.");
        try
        {
            Write(address, bytes);
            if (!Native.FlushInstructionCache(handle, address, (nuint)bytes.Length))
                throw Native.Error("İşlemci komut önbelleği temizlenemedi.");
        }
        finally
        {
            if (!Native.VirtualProtectEx(handle, address, (nuint)bytes.Length, old, out _))
                throw Native.Error("Bellek koruması geri yüklenemedi.");
        }
    }

    public List<nint> Scan(nint start, int size, byte[] pattern)
    {
        const int chunkSize = 1024 * 1024;
        long end = checked((long)start + size), cursor = (long)start;
        HashSet<nint> hits = [];
        while (cursor < end)
        {
            if (Native.VirtualQueryEx(handle, (nint)cursor, out var info,
                (nuint)Marshal.SizeOf<Native.MemoryInfo>()) == 0)
                throw Native.Error("Bellek bölgeleri okunamadı.");
            long regionEnd = Math.Min(end, checked((long)info.BaseAddress + (long)info.RegionSize));
            if (regionEnd <= cursor) break;
            bool executable = (info.Protect & 0xF0) != 0;
            if (info.State == 0x1000 && executable && (info.Protect & 0x101) == 0)
            {
                for (long address = cursor; address < regionEnd;)
                {
                    int count = (int)Math.Min(chunkSize + pattern.Length - 1, regionEnd - address);
                    byte[] bytes = Read((nint)address, count);
                    foreach (int offset in FeatureDefinition.Find(bytes, pattern))
                        hits.Add((nint)(address + offset));
                    address += Math.Min(chunkSize, regionEnd - address);
                }
            }
            cursor = regionEnd;
        }
        return hits.OrderBy(x => (long)x).ToList();
    }

    public nint AllocateNear(nint target)
    {
        const long granularity = 0x10000;
        long center = (long)target & ~(granularity - 1);
        for (long distance = granularity; distance < 0x70000000; distance += granularity)
        {
            foreach (long candidate in new[] { center - distance, center + distance })
            {
                if (candidate < granularity) continue;
                if (Native.VirtualQueryEx(handle, (nint)candidate, out var info,
                    (nuint)Marshal.SizeOf<Native.MemoryInfo>()) == 0 || info.State != 0x10000) continue;
                nint result = Native.VirtualAllocEx(handle, (nint)candidate, 4096,
                    Native.CommitReserve, Native.ExecuteReadWrite);
                if (result != 0) return result;
            }
        }
        throw new InvalidOperationException("Güvenli yakın bellek alanı ayrılamadı; özellik açılmadı.");
    }

    public void FreeUnused(nint address) => Native.VirtualFreeEx(handle, address, 0, 0x8000);

    public void PatchSafely(nint target, byte[] expected, byte[] replacement)
    {
        // Pause threads so no CPU can execute a half-written jump. Never patch while
        // an instruction pointer is in the middle of the overwritten instructions.
        for (int attempt = 0; attempt < 30; attempt++)
        {
            List<nint> suspended = [];
            bool busy = false;
            try
            {
                Process.Refresh();
                foreach (ProcessThread thread in Process.Threads)
                {
                    nint h = Native.OpenThread(0x0002 | 0x0008 | 0x0040, false, (uint)thread.Id);
                    if (h == 0) throw Native.Error("Oyun iş parçacığına erişilemedi.");
                    if (Native.SuspendThread(h) == uint.MaxValue)
                    {
                        Native.CloseHandle(h);
                        throw Native.Error("Güvenli bellek yaması için oyun duraklatılamadı.");
                    }
                    suspended.Add(h);
                    nint raw = Marshal.AllocHGlobal(1232 + 16);
                    try
                    {
                        nint context = (nint)(((long)raw + 15) & ~15L);
                        Marshal.Copy(new byte[1232], 0, context, 1232);
                        Marshal.WriteInt32(context, 48, 0x00100001); // AMD64 CONTEXT_CONTROL
                        if (!Native.GetThreadContext(h, context)) throw Native.Error("İş parçacığı konumu okunamadı.");
                        long ip = Marshal.ReadInt64(context, 248);
                        if (ip > (long)target && ip < (long)target + replacement.Length) busy = true;
                    }
                    finally { Marshal.FreeHGlobal(raw); }
                }
                if (!busy)
                {
                    if (!Read(target, expected.Length).SequenceEqual(expected))
                        throw new InvalidOperationException("Kod başka bir araç tarafından değiştirildi. Bellek yazımı iptal edildi.");
                    try { WriteCode(target, replacement); }
                    catch
                    {
                        // Roll back a possible partial write before resuming the game.
                        WriteCode(target, expected);
                        throw;
                    }
                    return;
                }
            }
            finally
            {
                foreach (nint h in suspended.AsEnumerable().Reverse())
                {
                    Native.ResumeThread(h);
                    Native.CloseHandle(h);
                }
            }
            Thread.Sleep(2);
        }
        throw new InvalidOperationException("Oyun kodu meşgul; özellik değiştirilmedi. Tekrar dene.");
    }

    public void Dispose() { handle.Dispose(); Process.Dispose(); }
}
