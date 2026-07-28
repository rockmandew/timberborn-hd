# Timberborn HD

Timberborn HD is an open-source graphics overhaul for Timberborn. The first
milestone improves texture sampling without replacing the game's art:

- full-resolution texture mipmaps;
- trilinear filtering for mipmapped world textures;
- up to 16× anisotropic filtering;
- automatic reapplication when Timberborn changes scenes.
- an original seamless 2K soil material with albedo, normal, and roughness maps.

This is the foundation for later high-resolution PBR texture packs, material
overrides, lighting controls, and selective model improvements.

## Compatibility

- Timberborn Stable 1.0
- Tested against `1.0.13.1-b769e88-sw`
- Unity `6000.3.6f1`
- No Harmony or TimberUI dependency for the initial milestone

## Build

Requirements:

- .NET SDK
- A Windows Timberborn installation

From PowerShell:

```powershell
.\build.ps1
```

To use a non-default installation:

```powershell
.\build.ps1 -GamePath 'D:\SteamLibrary\steamapps\common\Timberborn'
```

The packaged mod is written to `dist/TimberbornHD.zip`. The build also leaves
an unpacked copy under `mod/TimberbornHD`.

To regenerate the game-ready 2K soil maps from the original source:

```powershell
.\tools\Prepare-SoilTexture.ps1
```

## Install

Extract `TimberbornHD.zip` into:

```text
Documents\Timberborn\Mods
```

The final path should contain:

```text
Documents\Timberborn\Mods\TimberbornHD\manifest.json
Documents\Timberborn\Mods\TimberbornHD\TimberbornHD.dll
```

## Texture replacement roadmap

The `mod/TimberbornHD/Textures` directory contains original replacement art.
Do not commit assets extracted from Timberborn.

After entering a map, the mod writes `TimberbornHD-textures.csv` and
`TimberbornHD-materials.csv` into its installed mod directory. These inventory
live textures, materials, shaders, and texture-property bindings so replacement
materials can target exact game assets without unsafe guesses.

Planned work:

1. Run the in-game texture inventory and identify the terrain targets.
2. Bind the included 2K soil material to the correct terrain surface.
3. Add ambient-occlusion and packed mask maps where Timberborn's shader supports them.
4. Add quality presets and a settings UI.
5. Profile VRAM use before offering optional 4K variants.

## Legal

Timberborn and its original assets are owned by Mechanistry. This repository
contains original mod code only and is not affiliated with Mechanistry.
