param([switch]$Portable, [switch]$Installer, [switch]$LiveCheck, [string]$IsccPath)
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
if ($Portable -or $Installer) {
    $publishDirectory = if ($Installer) { 'artifacts/installer-payload' } else { 'artifacts/LimitWidget-win-x64' }
    & $dotnetExe publish src/LimitWidget/LimitWidget.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -o $publishDirectory --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
    Copy-Item -LiteralPath README.md -Destination (Join-Path $publishDirectory 'README.md')
    Compress-Archive -Path (Join-Path $publishDirectory 'LimitWidget.exe'),(Join-Path $publishDirectory 'README.md') -DestinationPath artifacts/LimitWidget-win-x64.zip -Force
}
if ($Installer) {
    $compilerCandidates = @($IsccPath, $env:ISCC_PATH, (Join-Path $PSScriptRoot '.tools/innosetup/ISCC.exe'),
        "${env:ProgramFiles}\Inno Setup 7\ISCC.exe", "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe")
    $pathCompiler = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($pathCompiler) { $compilerCandidates += $pathCompiler.Source }
    $compiler = $compilerCandidates | Where-Object { $_ -and (Test-Path -LiteralPath $_ -PathType Leaf) } | Select-Object -First 1
    if (-not $compiler) { throw 'Install Inno Setup 7, add ISCC.exe to PATH, or pass -IsccPath.' }
    $version = ([xml](Get-Content -LiteralPath src/LimitWidget/LimitWidget.csproj -Raw)).Project.PropertyGroup.Version
    & $compiler "/DAppVersion=$version" "/DProjectRoot=$PSScriptRoot" "/DPublishDir=$((Resolve-Path -LiteralPath $publishDirectory).Path)" installer/LimitWidget.iss
    if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed' }
    $releaseFiles = @((Join-Path $PSScriptRoot "artifacts/installer/CodexLimitWidget-$version-setup-x64.exe"),
        (Join-Path $PSScriptRoot 'artifacts/LimitWidget-win-x64.zip'))
    $hashLines = foreach ($releaseFile in $releaseFiles) {
        $hash = Get-FileHash -LiteralPath $releaseFile -Algorithm SHA256
        "$($hash.Hash.ToLowerInvariant())  $([IO.Path]::GetFileName($releaseFile))"
    }
    [IO.File]::WriteAllLines((Join-Path $PSScriptRoot 'artifacts/SHA256SUMS.txt'), [string[]]$hashLines)
}
