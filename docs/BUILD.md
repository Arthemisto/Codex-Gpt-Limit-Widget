# Build and package on Windows

## Requirements

- Windows x64, PowerShell, Git.
- [.NET SDK 10](https://dotnet.microsoft.com/download/dotnet/10.0). A local SDK under `.tools/dotnet` is also supported.
- [Inno Setup 7](https://jrsoftware.org/isdl.php) for the setup EXE, only on the build machine.
- Codex CLI and a ChatGPT-backed Codex sign-in for live tests. The desktop Codex app is needed for Launch Codex.

## Compile from source

```powershell
git clone https://github.com/Arthemisto/Codex-Gpt-Limit-Widget.git
cd Codex-Gpt-Limit-Widget
.\build.ps1
.\build.ps1 -LiveCheck
dotnet run --project src/LimitWidget
```

The first build runs Release compilation and deterministic checks. LiveCheck additionally queries account quotas without sending agent turns. Exit an executable running from the target output directory before replacing it.

## Portable package

```powershell
.\build.ps1 -Portable
```

Outputs: `artifacts/LimitWidget-win-x64/LimitWidget.exe` and `artifacts/LimitWidget-win-x64.zip`. The .NET desktop runtime is bundled. Codex itself is not bundled.

## Setup EXE

Install Inno Setup 7 from its official website, then:

```powershell
.\build.ps1 -Installer
# Custom compiler location:
.\build.ps1 -Installer -IsccPath 'C:\Tools\Inno Setup 7\ISCC.exe'
```

The script checks `ISCC_PATH`, PATH, standard installation folders, and `.tools/innosetup/ISCC.exe`. The application version is read from the project file.

Outputs:

- `artifacts/installer/CodexLimitWidget-<version>-setup-x64.exe`
- `artifacts/LimitWidget-win-x64.zip`
- `artifacts/SHA256SUMS.txt`

The installer payload is built separately under `artifacts/installer-payload`, without replacing the running portable copy. Setup installs for the current user under `%LocalAppData%/Programs/Codex Limit Widget`, creates a Start menu shortcut, offers an optional desktop shortcut, and registers an uninstaller. No administrator rights are requested. Supported target: Windows x64, Windows 10 build 19045 or newer.

Exit the widget using its tray menu before installation, upgrade or uninstall. Setup checks the application mutex and asks the user to close it. Re-running setup updates the same installation. Preferences are retained. An already-enabled startup entry is updated to the installed EXE; startup remains off if it was off.

Uninstall removes installed program files and its own startup entry. Preferences and account credentials are retained; use Disconnect widget first to remove the widget's saved login if desired.

## Validate a candidate

Test installation, start, sign-in, countdown, saved settings, shortcuts, upgrade and uninstall. Optional UI checks:

```powershell
.\artifacts\installer-payload\LimitWidget.exe --smoke-test artifacts/smoke
Get-Content artifacts/smoke/smoke-result.json
Get-FileHash artifacts/installer/CodexLimitWidget-0.3.1-setup-x64.exe -Algorithm SHA256
```

Candidates are provided for user testing before a final release. The EXEs are currently unsigned; code signing is not configured. Vulnerability review is a separate step and is not implied by successful compilation or functional checks.
