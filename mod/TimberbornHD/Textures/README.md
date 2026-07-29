# Replacement textures

Only textures consumed by the production mod are packaged here. Development
maps and metadata live under `assets/source-textures` so releases stay compact.

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
