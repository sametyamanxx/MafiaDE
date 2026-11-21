param([Parameter(Mandatory = $true)][string]$GamePath)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.Runtime.InteropServices;
public static class OriginalGameIcon {
    private delegate bool EnumName(IntPtr module, IntPtr type, IntPtr name, IntPtr param);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadLibraryEx(string path, IntPtr file, uint flags);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool EnumResourceNames(IntPtr module, IntPtr type, EnumName callback, IntPtr param);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr FindResource(IntPtr module, IntPtr name, IntPtr type);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LoadResource(IntPtr module, IntPtr resource);
    [DllImport("kernel32.dll")]
    private static extern uint SizeofResource(IntPtr module, IntPtr resource);
    [DllImport("kernel32.dll")]
    private static extern IntPtr LockResource(IntPtr resource);
    [DllImport("kernel32.dll")]
    private static extern bool FreeLibrary(IntPtr module);
    private static byte[] Read(IntPtr module, IntPtr name, int type) {
        IntPtr resource = FindResource(module, name, (IntPtr)type);
        if (resource == IntPtr.Zero) throw new InvalidOperationException("Icon resource not found.");
        byte[] bytes = new byte[SizeofResource(module, resource)];
        IntPtr data = LockResource(LoadResource(module, resource));
        if (data == IntPtr.Zero) throw new InvalidOperationException("Icon resource cannot be read.");
        Marshal.Copy(data, bytes, 0, bytes.Length);
        return bytes;
    }
    public static int Export(string source, string output) {
        // LOAD_LIBRARY_AS_DATAFILE: read resources without executing the game's code.
        IntPtr module = LoadLibraryEx(source, IntPtr.Zero, 2);
        if (module == IntPtr.Zero) throw new InvalidOperationException("Game resources cannot be opened.");
        try {
            byte[] group = null;
            EnumName callback = delegate(IntPtr m, IntPtr t, IntPtr n, IntPtr p) {
                if (group == null) group = Read(m, n, 14);
                return true;
            };
            EnumResourceNames(module, (IntPtr)14, callback, IntPtr.Zero);
            GC.KeepAlive(callback);
            if (group == null) throw new InvalidOperationException("Original game icon not found.");
            int count = BitConverter.ToUInt16(group, 4);
            byte[][] images = new byte[count][];
            using (var file = File.Create(output))
            using (var writer = new BinaryWriter(file)) {
                writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)count);
                int offset = 6 + count * 16;
                for (int i = 0; i < count; i++) {
                    int entry = 6 + i * 14;
                    images[i] = Read(module, (IntPtr)BitConverter.ToUInt16(group, entry + 12), 3);
                    writer.Write(group, entry, 8);
                    writer.Write(images[i].Length);
                    writer.Write(offset);
                    offset += images[i].Length;
                }
                foreach (byte[] image in images) writer.Write(image);
            }
            return count;
        } finally { FreeLibrary(module); }
    }
}
'@
$assets = Join-Path $PSScriptRoot '..\src\MafiaDE.Trainer\Assets'
[IO.Directory]::CreateDirectory($assets) | Out-Null
$iconPath = Join-Path $assets 'mafia.ico'
$count = [OriginalGameIcon]::Export($GamePath, $iconPath)
$stream = [IO.File]::OpenRead($iconPath)
try {
    $icon = [Drawing.Icon]::new($stream, [Drawing.Size]::new(256, 256))
    try {
        $bitmap = $icon.ToBitmap()
        try { $bitmap.Save((Join-Path $assets 'mafia.png'), [Drawing.Imaging.ImageFormat]::Png) }
        finally { $bitmap.Dispose() }
    } finally { $icon.Dispose() }
} finally { $stream.Dispose() }
Write-Output "Original game icon extracted with $count embedded sizes."
