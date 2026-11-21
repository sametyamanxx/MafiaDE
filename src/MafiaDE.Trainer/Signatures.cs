namespace MafiaDE.Trainer;

internal sealed record FeatureDefinition(string Name, byte[] Signature, int OverwriteLength, byte[] Effect)
{
    // Instruction locations documented by l0wb1t's Mafia DE table v0.1b.
    // Only these two features are implemented; no downloaded scripts are executed.
    public static readonly FeatureDefinition Ammo = new("Sınırsız Mühimmat",
        [0x41, 0x8B, 0x40, 0x04, 0x41, 0x3B, 0xC2, 0x76, 0x0B, 0x41, 0x2B, 0xC2, 0x41, 0x89, 0x40, 0x04],
        7, [0x41, 0xC7, 0x40, 0x04, 0x63, 0x00, 0x00, 0x00]); // mov dword [r8+4],99

    public static readonly FeatureDefinition Health = new("Sınırsız Sağlık",
        [0xF3, 0x0F, 0x10, 0x4B, 0x0C, 0x0F, 0x28, 0xE6, 0xF3, 0x0F, 0x5E, 0x4B, 0x04],
        5, [0x50, 0x8B, 0x43, 0x04, 0x89, 0x43, 0x0C, 0x58]); // current health = maximum; preserve RAX

    public static IEnumerable<int> Find(byte[] bytes, byte[] pattern)
    {
        if (pattern.Length == 0) throw new ArgumentException("Boş imza kullanılamaz.");
        for (int i = 0; i <= bytes.Length - pattern.Length; i++)
        {
            if (bytes[i] != pattern[0]) continue;
            if (bytes.AsSpan(i, pattern.Length).SequenceEqual(pattern)) yield return i;
        }
    }
}

internal static class Trampoline
{
    public const int FlagOffset = 0x100;

    public static byte[] Build(nint cave, nint target, FeatureDefinition definition)
    {
        List<byte> code = [0x9C, 0x80, 0x3D]; // pushfq; cmp byte [rip+flag],1
        code.AddRange(BitConverter.GetBytes(checked((int)((long)cave + FlagOffset - ((long)cave + 8)))));
        code.Add(0x01);
        code.Add(0x75); // jne: skip effect
        code.Add(checked((byte)definition.Effect.Length));
        code.AddRange(definition.Effect);
        code.Add(0x9D); // popfq; replay complete original instructions
        code.AddRange(definition.Signature.AsSpan(0, definition.OverwriteLength).ToArray());
        code.AddRange(Jump(cave + code.Count, target + definition.OverwriteLength, 5));
        return code.ToArray();
    }

    public static byte[] Jump(nint from, nint to, int length)
    {
        if (length < 5) throw new ArgumentOutOfRangeException(nameof(length));
        byte[] bytes = Enumerable.Repeat((byte)0x90, length).ToArray();
        bytes[0] = 0xE9;
        BitConverter.GetBytes(checked((int)((long)to - ((long)from + 5)))).CopyTo(bytes, 1);
        return bytes;
    }
}
