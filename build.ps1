param([switch]$Portable, [switch]$LiveCheck)
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
$localDotnetRoot = Join-Path $PSScriptRoot '.tools/dotnet'
$dotnetExe = Join-Path $localDotnetRoot 'dotnet.exe'
if (Test-Path -LiteralPath $dotnetExe) {
    # Some folder-customization tools create desktop.ini inside SDK import folders.
    # MSBuild treats every file in those folders as an XML import.
    $resolvedSdkRoot = (Resolve-Path -LiteralPath $localDotnetRoot).Path
    Get-ChildItem -LiteralPath $resolvedSdkRoot -Filter desktop.ini -Recurse -Force | ForEach-Object {
        if ($_.FullName.StartsWith($resolvedSdkRoot + '\', [StringComparison]::OrdinalIgnoreCase)) {
            Remove-Item -LiteralPath $_.FullName -Force
        }
    }
    $env:DOTNET_ROOT = $resolvedSdkRoot
} else {
    $dotnetExe = (Get-Command dotnet -ErrorAction Stop).Source
}
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
& $dotnetExe build src/LimitWidget/LimitWidget.csproj -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
& $dotnetExe run --project tests/LimitWidget.Checks
if ($LASTEXITCODE -ne 0) { throw 'Checks failed' }
if ($LiveCheck) {
    & $dotnetExe run --project tests/LimitWidget.Checks -- --live
    if ($LASTEXITCODE -ne 0) { throw 'Live check failed' }
}
if ($Portable) {
    & $dotnetExe publish src/LimitWidget/LimitWidget.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -o artifacts/LimitWidget-win-x64 --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
    Copy-Item -LiteralPath README.md -Destination artifacts/LimitWidget-win-x64/README.md
    Compress-Archive -Path artifacts/LimitWidget-win-x64/LimitWidget.exe,artifacts/LimitWidget-win-x64/README.md -DestinationPath artifacts/LimitWidget-win-x64.zip -Force
}
