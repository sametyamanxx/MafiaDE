param([Parameter(Mandatory = $true)][string]$GamePath)
$ErrorActionPreference = 'Stop'
Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
public static class BinarySearch {
    public static long[] Find(byte[] data, byte[] pattern) {
        var hits = new List<long>();
        for (int i = 0; i <= data.Length - pattern.Length; i++) {
            if (data[i] != pattern[0]) continue;
            int j = 1;
            while (j < pattern.Length && data[i+j] == pattern[j]) j++;
            if (j == pattern.Length) hits.Add(i);
        }
        return hits.ToArray();
    }
}
'@
$bytes = [IO.File]::ReadAllBytes($GamePath)
foreach ($entry in @(
    @{ Name = 'Ammo'; Pattern = [byte[]](0x41,0x8B,0x40,0x04,0x41,0x3B,0xC2) },
    @{ Name = 'Health'; Pattern = [byte[]](0xF3,0x0F,0x10,0x4B,0x0C,0x0F,0x28,0xE6) }
)) {
    $hits = [BinarySearch]::Find($bytes, $entry.Pattern)
    Write-Output "$($entry.Name): $($hits.Count) match(es)"
    foreach ($hit in $hits) {
        $start = [Math]::Max(0, $hit - 32)
        Write-Output ('  File offset: 0x{0:X}' -f $hit)
        Write-Output ([BitConverter]::ToString($bytes, $start, 96))
    }
}
