using System.Diagnostics;
using System.Security.Cryptography;

namespace MafiaDE.Trainer;

internal sealed class TrainerEngine : IDisposable
{
    public const string SupportedHash = "A98A2B22FB966E6F617B9ADB69DA4F2EB75EC794750C03AA3E23B2F6F29C76E1";
    private GameMemory? memory;
    private readonly Dictionary<FeatureDefinition, Hook> hooks = [];
    private readonly Dictionary<FeatureDefinition, nint> targets = [];
    public int? ProcessId => memory?.Process.Id;
    public bool Connected => memory?.Alive == true;
    public event Action<string>? Log;

    public bool Enabled(FeatureDefinition feature) => hooks.TryGetValue(feature, out var h) && h.Enabled;

    public void Connect(Process process)
    {
        if (memory != null) throw new InvalidOperationException("Zaten bağlı.");
        GameMemory? candidate = null;
        try
        {
            var module = process.MainModule ?? throw new InvalidOperationException("Oyun modülü bulunamadı.");
            if (!string.Equals(Path.GetFileName(module.FileName), "mafiadefinitiveedition.exe",
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Beklenmeyen oyun dosyası.");
            using (var file = File.OpenRead(module.FileName))
                if (Convert.ToHexString(SHA256.HashData(file)) != SupportedHash)
                    throw new InvalidOperationException("Oyun sürümü desteklenmiyor. Güvenlik için bellek yazılmadı.");
            candidate = new GameMemory(process);
            foreach (var feature in new[] { FeatureDefinition.Ammo, FeatureDefinition.Health })
            {
                var hits = candidate.Scan(module.BaseAddress, module.ModuleMemorySize, feature.Signature);
                if (hits.Count != 1)
                    throw new InvalidOperationException($"{feature.Name}: tekil imza bulunamadı ({hits.Count}). Başka trainer'ları kapat.");
                targets.Add(feature, hits[0]);
            }
            memory = candidate;
            Log?.Invoke($"Oyun bağlandı • PID {process.Id} • Epic 1.0.0.1");
            Log?.Invoke("Dosya ve bellek imzaları doğrulandı.");
            Log?.Invoke("Oyun içi etki henüz test edilmedi.");
        }
        catch
        {
            candidate?.Dispose();
            targets.Clear();
            throw;
        }
    }

    public void Toggle(FeatureDefinition feature)
    {
        if (!Connected || memory == null)
            throw new InvalidOperationException("Önce oyunu aç; otomatik bağlantı beklenecek.");
        if (!hooks.TryGetValue(feature, out var hook))
        {
            nint target = targets[feature];
            nint cave = memory.AllocateNear(target);
            byte[] original = feature.Signature[..feature.OverwriteLength];
            byte[] patch = Trampoline.Jump(target, cave, original.Length);
            try
            {
                memory.WriteCode(cave, Trampoline.Build(cave, target, feature));
                memory.Write(cave + Trampoline.FlagOffset, [0]);
                memory.PatchSafely(target, original, patch);
            }
            catch
            {
                // Do not free the cave: a failed rollback must never leave a dangling jump.
                throw;
            }
            hook = new Hook(target, cave, original, patch);
            hooks.Add(feature, hook);
        }
        memory.Write(hook.Cave + Trampoline.FlagOffset, [hook.Enabled ? (byte)0 : (byte)1]);
        hook.Enabled = !hook.Enabled;
        Log?.Invoke($"{feature.Name}: {(hook.Enabled ? "AÇIK" : "KAPALI")}");
    }

    public void Disconnect()
    {
        if (memory == null) return;
        List<string> errors = [];
        if (memory.Alive)
        {
            foreach (var pair in hooks.ToArray())
            {
                try
                {
                    memory.Write(pair.Value.Cave + Trampoline.FlagOffset, [0]);
                    pair.Value.Enabled = false;
                    memory.PatchSafely(pair.Value.Target, pair.Value.Patch, pair.Value.Original);
                    hooks.Remove(pair.Key);
                    // Retain 4 KB until the game exits: a suspended thread may still
                    // be inside this trampoline when the original code is restored.
                }
                catch (Exception ex) { errors.Add(ex.Message); }
            }
        }
        if (errors.Count > 0)
            throw new InvalidOperationException("Yama geri alınamadı. MafiaDE'yi kapatmadan önce oyunu kapat.\n" +
                string.Join("\n", errors));
        hooks.Clear();
        targets.Clear();
        memory.Dispose();
        memory = null;
    }

    public void Dispose() => Disconnect();

    private sealed class Hook(nint target, nint cave, byte[] original, byte[] patch)
    {
        public nint Target { get; } = target;
        public nint Cave { get; } = cave;
        public byte[] Original { get; } = original;
        public byte[] Patch { get; } = patch;
        public bool Enabled { get; set; }
    }
}
