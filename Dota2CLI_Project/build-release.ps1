$ErrorActionPreference = "Stop"

$ProjectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$OutputRoot = Join-Path $ProjectRoot "release"
$Runtime = "win-x64"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "Не знайдено .NET CLI. Встановіть .NET 8 SDK."
}

Write-Host "Перевірка .NET 8 SDK..."
$InstalledSdks = dotnet --list-sdks 2>&1
if ($LASTEXITCODE -ne 0 -or -not ($InstalledSdks | Where-Object { $_ -match '^\s*8\.' })) {
    throw "Не знайдено .NET 8 SDK. Встановіть .NET 8 SDK і повторіть запуск."
}

Set-Location $ProjectRoot

if (Test-Path $OutputRoot) {
    Remove-Item $OutputRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $OutputRoot | Out-Null

$Archive = Join-Path $ProjectRoot "Dota2CLI_Project_Windows_x64_Release.zip"
if (Test-Path $Archive) {
    Remove-Item $Archive -Force
}

Write-Host "Відновлення залежностей..."
dotnet restore ".\Gui\Dota2GUI.csproj"
if ($LASTEXITCODE -ne 0) { throw "Не вдалося відновити залежності GUI." }

dotnet restore ".\Dota2CLI.csproj"
if ($LASTEXITCODE -ne 0) { throw "Не вдалося відновити залежності CLI." }

Write-Host "Запуск автоматизованих тестів..."
dotnet run --project ".\Tests\Dota2CLI.Tests.csproj" --configuration Release
if ($LASTEXITCODE -ne 0) { throw "Автоматизовані тести завершилися з помилкою. Публікацію зупинено." }

$CommonPublishArgs = @(
    "--configuration", "Release",
    "--runtime", $Runtime,
    "--self-contained", "true",
    "-p:PublishSingleFile=true",
    "-p:IncludeNativeLibrariesForSelfExtract=true",
    "-p:EnableCompressionInSingleFile=true",
    "-p:DebugType=None",
    "-p:DebugSymbols=false"
)

Write-Host "Публікація графічної версії..."
$GuiOut = Join-Path $OutputRoot "Dota2GUI"
dotnet publish ".\Gui\Dota2GUI.csproj" @CommonPublishArgs --output $GuiOut
if ($LASTEXITCODE -ne 0) { throw "Не вдалося зібрати графічну версію." }

Write-Host "Публікація консольної версії..."
$CliOut = Join-Path $OutputRoot "Dota2CLI"
dotnet publish ".\Dota2CLI.csproj" @CommonPublishArgs --output $CliOut
if ($LASTEXITCODE -ne 0) { throw "Не вдалося зібрати консольну версію." }

$GuiExe = Join-Path $GuiOut "Dota2GUI.exe"
$CliExe = Join-Path $CliOut "Dota2CLI.exe"
if (-not (Test-Path $GuiExe) -or -not (Test-Path $CliExe)) {
    throw "Публікація завершилася без очікуваних EXE-файлів."
}

@"
Dota2 application release
Platform: Windows x64
Main application: Dota2GUI.exe
Console application: Dota2CLI.exe

Both applications are self-contained .NET 8 single-file releases.
The .NET Runtime does not need to be installed separately.
Windows x64 is required. Internet access is used to refresh the hero catalogue.
Local data is stored in %LOCALAPPDATA%\Dota2CLI.
"@ | Set-Content -Path (Join-Path $OutputRoot "RUN-ME.txt") -Encoding UTF8

Compress-Archive -Path (Join-Path $OutputRoot "*") -DestinationPath $Archive -CompressionLevel Optimal

Write-Host ""
Write-Host "Збірку завершено."
Write-Host "GUI: $GuiExe"
Write-Host "CLI: $CliExe"
Write-Host "Архів: $Archive"
