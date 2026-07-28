# Timberborn HD

Timberborn HD is an open-source graphics overhaul for Timberborn. The first
milestone improves texture sampling without replacing the game's art:

- full-resolution texture mipmaps;
- trilinear filtering for mipmapped world textures;
- up to 16× anisotropic filtering;
- automatic reapplication when Timberborn changes scenes.

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

The `mod/TimberbornHD/Textures` directory is reserved for original replacement
art. Do not commit assets extracted from Timberborn.

Planned work:

1. Inventory terrain and environment texture names and paths.
2. Replace one tiling terrain surface with an original 2K PBR material.
3. Add normal, roughness, and ambient-occlusion maps.
4. Add quality presets and a settings UI.
5. Profile VRAM use before offering optional 4K variants.

## Legal

Timberborn and its original assets are owned by Mechanistry. This repository
contains original mod code only and is not affiliated with Mechanistry.

