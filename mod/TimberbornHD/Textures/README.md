# Replacement textures

Place original replacement textures here while developing the high-resolution
material pack. Preserve Timberborn resource-relative paths when using direct
image overrides.

Each texture can have a sibling metadata file such as:

```text
Terrain/MyTexture.png
Terrain/MyTexture.png.meta.json
```

Recommended color-texture metadata:

```json
{
  "isSprite": false,
  "isNormalMap": false,
  "linear": false,
  "generateMipmap": true,
  "filterMode": "Trilinear",
  "wrapMode": "Repeat",
  "textureFormat": "RGBA32",
  "anisoLevel": 16
}
```

Do not commit textures extracted from the game.
