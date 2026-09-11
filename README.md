# PlayerSort

PlayerSort is a small client-side Valheim mod that adds a **Sort** button to the player inventory.

## Features

- Sorts the normal player inventory by item category, name, quality, and stack size.
- Leaves the hotbar untouched.
- Leaves separate gear and quick slots added by other mods untouched.
- Requires no server installation.
- Has no ChestFlow dependency.

## Requirements

- Valheim
- [BepInExPack for Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/)

BepInEx is the only runtime dependency.

## Installation

### Mod manager

Install PlayerSort and its BepInEx dependency through a Thunderstore-compatible mod manager.

### Manual

1. Install BepInEx for Valheim.
2. Copy `PlayerSort.dll` to `BepInEx/plugins/PlayerSort/`.
3. Start Valheim through BepInEx.

Install the mod on the client only. A dedicated server does not need PlayerSort.

## Building

The project targets `netstandard2.1`. Game and loader assemblies are not stored in Git.

Run the setup once from PowerShell:

```powershell
./setup.ps1
dotnet build -c Release
```

The setup script locates Steam, Valheim, and a local BepInEx or r2modman profile, then copies only the required compile-time assemblies into the ignored `libs/` directory. No environment variables are required.

If auto-detection is not suitable, pass either or both locations explicitly:

```powershell
./setup.ps1 `
    -ValheimPath 'D:\SteamLibrary\steamapps\common\Valheim' `
    -BepInExPath 'D:\Games\Valheim\BepInEx\core'
```

`-BepInExPath` may point to the BepInEx `core` directory, the `BepInEx` directory, a mod-manager profile, or the Valheim directory.

The compiled plugin is written to `bin/Release/netstandard2.1/PlayerSort.dll`. Referenced Valheim, Unity, and BepInEx assemblies are marked non-private and are not copied to the build output.

## Thunderstore package

Create the ready-to-upload archive with:

```powershell
dotnet build -c Release -t:PackageThunderstore
```

The result is `dist/PlayerSort-1.0.0.zip` and contains only the package metadata, license, icon, and `plugins/PlayerSort.dll`.

## License

PlayerSort is available under the [MIT License](LICENSE).
