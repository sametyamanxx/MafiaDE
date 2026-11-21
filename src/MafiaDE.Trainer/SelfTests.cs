using System.Diagnostics;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace MafiaDE.Trainer;

internal static class SelfTests
{
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate int AmmoFunction(nint inventory, int amount);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate float HealthFunction(nint health);

    public static int Run(string[] args)
    {
        List<string> results = [];
        int exit = 0;
        try
        {
            foreach (bool visible in new[] { false, true })
                foreach (bool minimized in new[] { false, true })
                    foreach (bool foreground in new[] { false, true })
                        Assert(WindowVisibility.ShouldHide(visible, minimized, foreground) ==
                            (visible && !minimized && foreground),
                            $"Insert visibility: visible={visible}, minimized={minimized}, foreground={foreground}", results);
            Assert(!WindowVisibility.ShouldHide(true, false, false),
                "Insert: covered visible window must be brought forward, not hidden", results);
            Assert(FeatureDefinition.Find([1, 2, 1, 2, 1], [1, 2, 1]).SequenceEqual([0, 2]),
                "Overlapping signature matches", results);
            Assert(!FeatureDefinition.Find([1], [1, 2]).Any(), "Short buffers", results);
            Assert(FeatureDefinition.Find([1, 2, 3], [3]).Single() == 2, "Last-byte match", results);
            foreach (var feature in new[] { FeatureDefinition.Ammo, FeatureDefinition.Health })
            {
                nint cave = 0x10000000, target = 0x10010000;
                byte[] code = Trampoline.Build(cave, target, feature);
                int flagDelta = BitConverter.ToInt32(code, 3);
                Assert(8 + flagDelta == Trampoline.FlagOffset, $"{feature.Name}: flag RIP-relative address", results);
                int end = code.Length;
                Assert((long)cave + end + BitConverter.ToInt32(code, end - 4) ==
                    (long)target + feature.OverwriteLength, $"{feature.Name}: return jump", results);
                byte[] jump = Trampoline.Jump(target, cave, feature.OverwriteLength);
                Assert((long)target + 5 + BitConverter.ToInt32(jump, 1) == (long)cave,
                    $"{feature.Name}: entry jump", results);
                Assert(code[9] == feature.Effect.Length, $"{feature.Name}: conditional branch", results);
            }
            TestNativeExecution(results);
            int gameIndex = Array.IndexOf(args, "--game");
            if (gameIndex >= 0 && gameIndex + 1 < args.Length) TestGameFile(args[gameIndex + 1], results);
            results.Add("ALL TESTS PASSED. In-game gameplay has NOT been tested.");
        }
        catch (Exception ex) { results.Add($"FAILED: {ex}"); exit = 1; }
        int reportIndex = Array.IndexOf(args, "--test-report");
        if (reportIndex >= 0 && reportIndex + 1 < args.Length)
            File.WriteAllLines(args[reportIndex + 1], results);
        return exit;
    }

    private static void TestNativeExecution(List<string> results)
    {
        // Execute the generated x64 code only in this test process, against synthetic data.
        // This validates register preservation, branch destinations, toggles and original behavior.
        using var memory = new GameMemory(Process.GetCurrentProcess());
        nint page = memory.AllocateNear(memory.Process.MainModule!.BaseAddress);
        nint data = Marshal.AllocHGlobal(32);
        try
        {
            nint wrapper = page, target = page + 0x40, cave = page + 0x200;
            byte[] original = [0x41, 0x8B, 0x40, 0x04, 0x41, 0x3B, 0xC2, 0x76, 0x0B,
                0x41, 0x2B, 0xC2, 0x41, 0x89, 0x40, 0x04, 0x41, 0x8B, 0xC2, 0xC3,
                0x41, 0xC7, 0x40, 0x04, 0, 0, 0, 0, 0xC3];
            byte[] prefix = [0x49, 0x89, 0xC8, 0x41, 0x89, 0xD2]; // r8=rcx, r10d=edx
            memory.WriteCode(wrapper, [.. prefix, .. Trampoline.Jump(wrapper + prefix.Length, target, 5)]);
            memory.WriteCode(target, original);
            memory.WriteCode(cave, Trampoline.Build(cave, target, FeatureDefinition.Ammo));
            var ammo = Marshal.GetDelegateForFunctionPointer<AmmoFunction>(wrapper);
            Marshal.WriteInt32(data, 4, 10);
            Assert(ammo(data, 1) == 1 && Marshal.ReadInt32(data, 4) == 9, "Ammo: original behavior", results);
            memory.WriteCode(target, Trampoline.Jump(target, cave, 7));
            memory.Write(cave + Trampoline.FlagOffset, [0]);
            Marshal.WriteInt32(data, 4, 10);
            Assert(ammo(data, 1) == 1 && Marshal.ReadInt32(data, 4) == 9, "Ammo: disabled trampoline", results);
            memory.Write(cave + Trampoline.FlagOffset, [1]);
            Assert(ammo(data, 1) == 1 && Marshal.ReadInt32(data, 4) == 98, "Ammo: enabled refill", results);
            Assert(ammo(data, 1) == 1 && Marshal.ReadInt32(data, 4) == 98, "Ammo: repeated refill", results);
            memory.Write(cave + Trampoline.FlagOffset, [0]);
            Assert(ammo(data, 1) == 1 && Marshal.ReadInt32(data, 4) == 97, "Ammo: toggle off", results);

            wrapper = page + 0x400; target = page + 0x440; cave = page + 0x600;
            prefix = [0x53, 0x48, 0x89, 0xCB]; // preserve RBX, rbx=rcx
            memory.WriteCode(wrapper, [.. prefix, .. Trampoline.Jump(wrapper + prefix.Length, target, 5)]);
            memory.WriteCode(target, [.. FeatureDefinition.Health.Signature[..5], 0x0F, 0x28, 0xC1, 0x5B, 0xC3]);
            memory.WriteCode(cave, Trampoline.Build(cave, target, FeatureDefinition.Health));
            var health = Marshal.GetDelegateForFunctionPointer<HealthFunction>(wrapper);
            Marshal.WriteInt32(data, 4, BitConverter.SingleToInt32Bits(100));
            Marshal.WriteInt32(data, 12, BitConverter.SingleToInt32Bits(25));
            Assert(health(data) == 25, "Health: original behavior", results);
            memory.WriteCode(target, Trampoline.Jump(target, cave, 5));
            memory.Write(cave + Trampoline.FlagOffset, [0]);
            Assert(health(data) == 25, "Health: disabled trampoline", results);
            memory.Write(cave + Trampoline.FlagOffset, [1]);
            Assert(health(data) == 100, "Health: restored to maximum", results);
            memory.Write(cave + Trampoline.FlagOffset, [0]);
            Marshal.WriteInt32(data, 12, BitConverter.SingleToInt32Bits(42));
            Assert(health(data) == 42, "Health: toggle off", results);

            var scanned = memory.Scan(page, 4096, FeatureDefinition.Ammo.Signature[..4]);
            Assert(scanned.Contains(page + 0x200 + 19), "Live executable-memory signature scan", results);
        }
        finally { Marshal.FreeHGlobal(data); memory.FreeUnused(page); }
    }

    private static void TestGameFile(string path, List<string> results)
    {
        using var file = File.OpenRead(path);
        Assert(Convert.ToHexString(SHA256.HashData(file)) == TrainerEngine.SupportedHash,
            "Installed Epic EXE SHA-256", results);
        file.Position = 0;
        using var pe = new PEReader(file);
        foreach (var feature in new[] { FeatureDefinition.Ammo, FeatureDefinition.Health })
        {
            List<int> rvas = [];
            foreach (var section in pe.PEHeaders.SectionHeaders)
            {
                if ((section.SectionCharacteristics & SectionCharacteristics.MemExecute) == 0) continue;
                byte[] data = pe.GetSectionData(section.VirtualAddress).GetContent().ToArray();
                rvas.AddRange(FeatureDefinition.Find(data, feature.Signature).Select(x => section.VirtualAddress + x));
            }
            Assert(rvas.Count == 1, $"{feature.Name}: one match in executable PE sections", results);
            results.Add($"  RVA: 0x{rvas.Single():X}");
        }
    }

    private static void Assert(bool condition, string name, List<string> results)
    {
        if (!condition) throw new InvalidOperationException(name);
        results.Add($"PASS: {name}");
    }
}
