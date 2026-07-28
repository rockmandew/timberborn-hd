# Timberborn HD

Timberborn HD is an open-source graphics overhaul for Timberborn. The first
milestone improves texture sampling without replacing the game's art:

- full-resolution texture mipmaps;
- trilinear filtering for mipmapped world textures;
- up to 16× anisotropic filtering;
- automatic reapplication when Timberborn changes scenes.
- an original seamless 2K soil material with albedo, normal, and roughness maps.
- original seamless 2K grass and dry-ground materials.
- targeted runtime replacement of DirtURP and TerrainURP texture bindings.

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

To regenerate any game-ready 2K texture set from an original source:

```powershell
.\tools\Prepare-SoilTexture.ps1
.\tools\Prepare-SoilTexture.ps1 `
  -InputPath .\assets\source\grass-natural-source.png `
  -OutputDirectory .\mod\TimberbornHD\Textures\Terrain `
  -BaseName grass-natural
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

After entering a map, the mod writes deduplicated `TimberbornHD-textures.csv` and
`TimberbornHD-materials.csv` into its installed mod directory. These inventory
live textures, materials, shaders, and texture-property bindings so replacement
materials can target exact game assets without unsafe guesses.

Version 0.5 also exports the loaded tree atlases once to
`TextureDumps/Trees` inside the installed mod directory. These local reference
files preserve Timberborn's exact UV layout for the upcoming 2K tree pass and
are excluded from this repository and release packages.

Timberborn's DirtURP shader exposes dirt and dry field surfaces through
`_MainTex` but does not expose normal or roughness texture properties. The
TerrainURP shader exposes grass through `_BaseAlbedoTex` and a `_Normalmap`;
Timberborn HD replaces those bindings selectively while retaining unused PBR
maps for future shaders that support them.

Planned work:

1. Validate grass and dry-ground scale in multiple lighting and moisture conditions.
2. Export tree texture atlases locally so enhanced replacements preserve UV layouts.
3. Add ambient-occlusion and packed mask maps where Timberborn's shaders support them.
4. Add quality presets and a settings UI.
5. Profile VRAM use before offering optional 4K variants.

## Legal

Timberborn and its original assets are owned by Mechanistry. This repository
contains original mod code only and is not affiliated with Mechanistry.
