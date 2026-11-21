param([switch]$FrameworkDependent)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$project = Join-Path $root 'src\MafiaDE.Trainer\MafiaDE.Trainer.csproj'
$output = Join-Path $root 'portable'
if ($FrameworkDependent) {
    dotnet publish $project -c Release -r win-x64 --self-contained false -o $output -p:PublishSingleFile=true -p:DebugType=None -p:DebugSymbols=false
} else {
    dotnet publish $project -c Release -r win-x64 --self-contained true -o $output -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -p:DebugSymbols=false
}
if ($LASTEXITCODE -ne 0) { throw 'Derleme başarısız.' }
Write-Output "Hazır: $output\MafiaDE.exe"
