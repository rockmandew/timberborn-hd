using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace TimberbornHD;

public sealed class GraphicsEnhancer : MonoBehaviour
{
    private const int MaximumAnisotropicLevel = 16;
    private const float SceneLoadDelaySeconds = 1.0f;
    private const string TextureInventoryFileName = "TimberbornHD-textures.csv";
    private const string MaterialInventoryFileName = "TimberbornHD-materials.csv";
    private const string DryGroundDiagnosticsFileName = "TimberbornHD-dryfield-global.txt";
    private const string DirtShaderName = "Shader Graphs/DirtURP";
    private const string DirtTextureProperty = "_MainTex";
    private const string OriginalDirtTextureName = "Dirt";
    private const string SoilAlbedoRelativePath = "Textures/Soil/soil-neutral-albedo.png";
    private const string TerrainShaderName = "Shader Graphs/TerrainURP";
    private const string SlopeShaderName = "Shader Graphs/SlopeURP";
    private const string TerrainBaseAlbedoProperty = "_BaseAlbedoTex";
    private const string TerrainNormalProperty = "_Normalmap";
    private const string TerrainSplatAlbedoProperty = "_SplatAlbedoTex";
    private const string OriginalGrassTextureName = "Grass";
    private const string GrassAlbedoRelativePath = "Textures/Terrain/grass-natural-albedo.png";
    private const string GrassNormalRelativePath = "Textures/Terrain/grass-natural-normal.png";
    private const string OriginalCliffDetailTextureName = "CliffDetail";
    private const string OriginalCliffNormalTextureName = "Cliff_N";
    private const string CliffAlbedoRelativePath = "Textures/Terrain/cliff-shale-albedo.png";
    private const string CliffNormalRelativePath = "Textures/Terrain/cliff-shale-normal.png";
    private const string OriginalGroundTextureName = "Ground";
    private const string OriginalDryFieldTextureName = "DryField";
    private const string DryGroundAlbedoRelativePath = "Textures/Terrain/ground-dry-albedo.png";
    private const string VegetationShaderName = "Shader Graphs/VegetationURP";
    private const string VegetationAlbedoProperty = "_MainTex";
    private const string VegetationNormalProperty = "_BumpMap";
    private const string TreeDumpDirectoryName = "TextureDumps/Trees";
    private const string TerrainDumpDirectoryName = "TextureDumps/Terrain";

    private static readonly TreeTextureSpecification[] TreeTextureSpecifications =
    {
        new("Birch_D", "TimberbornHD_Birch_Albedo_2K", false, true, false),
        new("Birch_N", "TimberbornHD_Birch_Normal_2K", true, false, true),
        new("ChestnutTree_D", "TimberbornHD_Chestnut_Albedo_2K", false, true, false),
        new("ChestnutTree_N", "TimberbornHD_Chestnut_Normal_2K", true, false, true),
        new("Maple_D", "TimberbornHD_Maple_Albedo_2K", false, true, false),
        new("Maple_N", "TimberbornHD_Maple_Normal_2K", true, false, true),
        new("Oak_D", "TimberbornHD_Oak_Albedo_2K", false, true, false),
        new("Oak_N", "TimberbornHD_Oak_Normal_2K", true, false, true),
        new("Pine_D", "TimberbornHD_Pine_Albedo_2K", false, true, false),
        new("Pine_N", "TimberbornHD_Pine_Normal_2K", true, false, true)
    };

    private static readonly HashSet<string> TreeTextureNames = new()
    {
        "Birch_D",
        "Birch_N",
        "ChestnutTree_D",
        "ChestnutTree_N",
        "Chestnut_Detail",
        "Maple_D",
        "Maple_N",
        "Maple_Detail",
        "Oak_D",
        "Oak_N",
        "Pine_D",
        "Pine_N",
        "Pine_Detail"
    };

    private static readonly HashSet<string> TerrainTextureNames = new()
    {
        "CliffDetail",
        "CliffMask",
        "Cliff_N",
        "DryField",
        "GrassDetailsAlbedo",
        "Ground",
        "SlopeDetailAlbedo",
        "SlopeSand"
    };

    public static GraphicsEnhancer? Instance { get; private set; }
    private static string? _modPath;
    private static Texture2D? _soilAlbedo;
    private static Texture2D? _grassAlbedo;
    private static Texture2D? _grassNormal;
    private static Texture2D? _cliffAlbedo;
    private static Texture2D? _cliffNormal;
    private static Texture2D? _dryGroundAlbedo;
    private static readonly Dictionary<string, Texture2D> TreeTextureOverrides = new();
    private static readonly HashSet<int> OverriddenDryFieldTextureIds = new();
    private static int _desertShaderPropertyId = -1;
    private static int _dryFieldShaderPropertyId = -1;
    private static bool _terrainShaderPropertyLookupAttempted;
    private static bool _dryGroundRenderDiagnosticWritten;

    public static void Configure(string modPath)
    {
        _modPath = modPath;
    }

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
        QualitySettings.globalTextureMipmapLimit = 0;
        LoadReplacementTextures();
        SceneManager.sceneLoaded += OnSceneLoaded;
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        ApplyTextureQuality();
        ApplyMaterialOverrides();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            Instance = null;
        }
    }

    private void LateUpdate()
    {
        ApplyDryGroundGlobalOverrides();
    }

    private static void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        ApplyDryGroundGlobalOverrides(writeDiagnostics: true, cameraName: camera.name);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode loadSceneMode)
    {
        StartCoroutine(ApplyAfterSceneLoad());
    }

    private static IEnumerator ApplyAfterSceneLoad()
    {
        yield return new WaitForSecondsRealtime(SceneLoadDelaySeconds);
        yield return PrepareTreeTextureOverrides();
        ApplyTextureQuality();
        ApplyMaterialOverrides();
        WriteTextureInventory();
        WriteMaterialInventory();
        WriteTreeTextureDumps();
        WriteTerrainTextureDumps();
    }

    private static void ApplyTextureQuality()
    {
        var changedTextures = 0;
        var textures = Resources.FindObjectsOfTypeAll<Texture2D>();

        foreach (var texture in textures)
        {
            if (texture == null || texture.mipmapCount <= 1 || texture.wrapMode == TextureWrapMode.Clamp)
            {
                continue;
            }

            texture.filterMode = FilterMode.Trilinear;
            texture.anisoLevel = MaximumAnisotropicLevel;
            changedTextures++;
        }

        Debug.Log($"[Timberborn HD] Enhanced {changedTextures} mipmapped world textures.");
    }

    private static void LoadReplacementTextures()
    {
        if (string.IsNullOrWhiteSpace(_modPath))
        {
            return;
        }

        _soilAlbedo ??= LoadTexture(
            SoilAlbedoRelativePath,
            "TimberbornHD_SoilNeutral_Albedo",
            linear: false);
        _grassAlbedo ??= LoadTexture(
            GrassAlbedoRelativePath,
            "TimberbornHD_GrassNatural_Albedo",
            linear: false);
        _grassNormal ??= LoadTexture(
            GrassNormalRelativePath,
            "TimberbornHD_GrassNatural_Normal",
            linear: true);
        _cliffAlbedo ??= LoadTexture(
            CliffAlbedoRelativePath,
            "TimberbornHD_CliffShale_Albedo",
            linear: false);
        _cliffNormal ??= LoadTexture(
            CliffNormalRelativePath,
            "TimberbornHD_CliffShale_Normal",
            linear: true);
        _dryGroundAlbedo ??= LoadTexture(
            DryGroundAlbedoRelativePath,
            "TimberbornHD_GroundDry_Albedo",
            linear: false);
    }

    private static Texture2D? LoadTexture(string relativePath, string textureName, bool linear)
    {
        try
        {
            var texturePath = Path.Combine(_modPath!, relativePath);
            var textureBytes = File.ReadAllBytes(texturePath);
            var texture = new Texture2D(2, 2, TextureFormat.RGB24, true, linear)
            {
                name = textureName,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = MaximumAnisotropicLevel
            };

            if (!ImageConversion.LoadImage(texture, textureBytes, false))
            {
                Object.Destroy(texture);
                throw new InvalidDataException($"Unity could not decode {texturePath}.");
            }

            Debug.Log($"[Timberborn HD] Loaded 2K texture {textureName} from {texturePath}");
            return texture;
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning($"[Timberborn HD] Could not load {relativePath}: {exception.Message}");
            return null;
        }
    }

    private static void ApplyMaterialOverrides()
    {
        ApplyDryFieldTextureOverride();
        var changedMaterials = 0;
        var materials = Resources.FindObjectsOfTypeAll<Material>();

        foreach (var material in materials)
        {
            if (material == null || material.shader == null)
            {
                continue;
            }

            if (material.shader.name == DirtShaderName
                && material.HasProperty(DirtTextureProperty))
            {
                var currentTexture = material.GetTexture(DirtTextureProperty);
                if (_soilAlbedo != null
                    && currentTexture != _soilAlbedo
                    && currentTexture != null
                    && currentTexture.name == OriginalDirtTextureName)
                {
                    material.SetTexture(DirtTextureProperty, _soilAlbedo);
                    changedMaterials++;
                }

                if (_dryGroundAlbedo != null
                    && currentTexture != _dryGroundAlbedo
                    && currentTexture != null
                    && currentTexture.name == OriginalGroundTextureName)
                {
                    material.SetTexture(DirtTextureProperty, _dryGroundAlbedo);
                    changedMaterials++;
                }
            }

            if (material.shader.name == TerrainShaderName
                && material.HasProperty(TerrainBaseAlbedoProperty))
            {
                var materialName = material.name.Replace(" (Instance)", string.Empty);
                var currentBaseAlbedo = material.GetTexture(TerrainBaseAlbedoProperty);
                if (_grassAlbedo != null
                    && currentBaseAlbedo != _grassAlbedo
                    && currentBaseAlbedo != null
                    && currentBaseAlbedo.name == OriginalGrassTextureName)
                {
                    material.SetTexture(TerrainBaseAlbedoProperty, _grassAlbedo);
                    changedMaterials++;
                }

                if (_grassNormal != null
                    && materialName == "Grass"
                    && material.HasProperty(TerrainNormalProperty)
                    && material.GetTexture(TerrainNormalProperty) != _grassNormal)
                {
                    material.SetTexture(TerrainNormalProperty, _grassNormal);
                    changedMaterials++;
                }

            }

            if (material.shader.name == TerrainShaderName || material.shader.name == SlopeShaderName)
            {
                if (_cliffAlbedo != null && material.HasProperty(TerrainSplatAlbedoProperty))
                {
                    var currentSplatAlbedo = material.GetTexture(TerrainSplatAlbedoProperty);
                    if (currentSplatAlbedo != _cliffAlbedo
                        && currentSplatAlbedo != null
                        && currentSplatAlbedo.name == OriginalCliffDetailTextureName)
                    {
                        material.SetTexture(TerrainSplatAlbedoProperty, _cliffAlbedo);
                        changedMaterials++;
                    }
                }

                if (_cliffNormal != null && material.HasProperty(TerrainNormalProperty))
                {
                    var currentNormal = material.GetTexture(TerrainNormalProperty);
                    if (currentNormal != _cliffNormal
                        && currentNormal != null
                        && currentNormal.name == OriginalCliffNormalTextureName)
                    {
                        material.SetTexture(TerrainNormalProperty, _cliffNormal);
                        changedMaterials++;
                    }
                }
            }

            if (material.shader.name == VegetationShaderName)
            {
                changedMaterials += ApplyTreeTextureOverride(material, VegetationAlbedoProperty);
                changedMaterials += ApplyTreeTextureOverride(material, VegetationNormalProperty);
            }
        }

        Debug.Log($"[Timberborn HD] Applied HD terrain textures to {changedMaterials} materials.");
    }

    private static void ApplyDryFieldTextureOverride()
    {
        if (string.IsNullOrWhiteSpace(_modPath))
        {
            return;
        }

        try
        {
            var candidates = Resources.FindObjectsOfTypeAll<Texture2D>()
                .Where(texture => texture != null
                                  && texture.name == OriginalDryFieldTextureName
                                  && !OverriddenDryFieldTextureIds.Contains(texture.GetInstanceID()))
                .ToArray();
            if (candidates.Length == 0)
            {
                return;
            }

            var texturePath = Path.Combine(_modPath, DryGroundAlbedoRelativePath);
            var textureBytes = File.ReadAllBytes(texturePath);
            var changedTextures = 0;

            foreach (var texture in candidates)
            {
                if (!ImageConversion.LoadImage(texture, textureBytes, false))
                {
                    continue;
                }

                texture.name = OriginalDryFieldTextureName;
                texture.wrapMode = TextureWrapMode.Repeat;
                texture.filterMode = FilterMode.Trilinear;
                texture.anisoLevel = MaximumAnisotropicLevel;
                OverriddenDryFieldTextureIds.Add(texture.GetInstanceID());
                changedTextures++;
            }

            Debug.Log($"[Timberborn HD] Replaced {changedTextures} DryField texture instances in place.");
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning($"[Timberborn HD] Could not replace DryField in place: {exception.Message}");
        }
    }

    private static void ApplyDryGroundGlobalOverrides(
        bool writeDiagnostics = false,
        string cameraName = "")
    {
        if (_dryGroundAlbedo == null)
        {
            return;
        }

        if (!_terrainShaderPropertyLookupAttempted)
        {
            ResolveTerrainShaderPropertyIds();
        }

        Texture? desertTextureBeforeBinding = null;
        Texture? dryFieldTextureBeforeBinding = null;

        if (_desertShaderPropertyId >= 0)
        {
            desertTextureBeforeBinding = Shader.GetGlobalTexture(_desertShaderPropertyId);
            Shader.SetGlobalTexture(_desertShaderPropertyId, _dryGroundAlbedo);
        }

        if (_dryFieldShaderPropertyId >= 0)
        {
            dryFieldTextureBeforeBinding = Shader.GetGlobalTexture(_dryFieldShaderPropertyId);
            Shader.SetGlobalTexture(_dryFieldShaderPropertyId, _dryGroundAlbedo);
        }

        if (writeDiagnostics && !_dryGroundRenderDiagnosticWritten)
        {
            WriteDryGroundGlobalDiagnostics(
                desertTextureBeforeBinding,
                dryFieldTextureBeforeBinding,
                cameraName);
        }
    }

    private static void ResolveTerrainShaderPropertyIds()
    {
        _terrainShaderPropertyLookupAttempted = true;

        try
        {
            var terrainAssembly = System.AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(assembly => assembly.GetName().Name == "Timberborn.TerrainSystemRendering");
            var terrainMaterialMapType = terrainAssembly?.GetType(
                "Timberborn.TerrainSystemRendering.TerrainMaterialMap");
            _desertShaderPropertyId = ResolveShaderPropertyId(
                terrainMaterialMapType,
                "DesertTextureProperty");
            _dryFieldShaderPropertyId = ResolveShaderPropertyId(
                terrainMaterialMapType,
                "DryFieldTextureProperty");

            Debug.Log(
                $"[Timberborn HD] Resolved terrain shader properties: "
                + $"Desert={_desertShaderPropertyId}, DryField={_dryFieldShaderPropertyId}.");
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning(
                $"[Timberborn HD] Could not inspect terrain shader properties: {exception.Message}");
        }
    }

    private static int ResolveShaderPropertyId(System.Type? terrainMaterialMapType, string fieldName)
    {
        var propertyField = terrainMaterialMapType?.GetField(
            fieldName,
            System.Reflection.BindingFlags.Static
            | System.Reflection.BindingFlags.Public
            | System.Reflection.BindingFlags.NonPublic);
        return propertyField?.GetValue(null) is int propertyId ? propertyId : -1;
    }

    private static void WriteDryGroundGlobalDiagnostics(
        Texture? desertTextureBeforeBinding,
        Texture? dryFieldTextureBeforeBinding,
        string cameraName)
    {
        if (string.IsNullOrWhiteSpace(_modPath)
            || _dryGroundAlbedo == null
            || (_desertShaderPropertyId < 0 && _dryFieldShaderPropertyId < 0))
        {
            return;
        }

        try
        {
            var desertTextureAfterBinding = _desertShaderPropertyId >= 0
                ? Shader.GetGlobalTexture(_desertShaderPropertyId)
                : null;
            var dryFieldTextureAfterBinding = _dryFieldShaderPropertyId >= 0
                ? Shader.GetGlobalTexture(_dryFieldShaderPropertyId)
                : null;
            var diagnostic = new StringBuilder();
            diagnostic.Append("Camera=").AppendLine(cameraName);
            diagnostic.Append("Requested=").AppendLine(DescribeTexture(_dryGroundAlbedo));
            diagnostic.Append("DesertPropertyId=").AppendLine(_desertShaderPropertyId.ToString());
            diagnostic.Append("DesertBefore=").AppendLine(DescribeTexture(desertTextureBeforeBinding));
            diagnostic.Append("DesertAfter=").AppendLine(DescribeTexture(desertTextureAfterBinding));
            diagnostic.Append("DesertBoundImmediatelyBeforeRender=")
                .AppendLine((desertTextureAfterBinding == _dryGroundAlbedo).ToString());
            diagnostic.Append("DryFieldPropertyId=").AppendLine(_dryFieldShaderPropertyId.ToString());
            diagnostic.Append("DryFieldBefore=").AppendLine(DescribeTexture(dryFieldTextureBeforeBinding));
            diagnostic.Append("DryFieldAfter=").AppendLine(DescribeTexture(dryFieldTextureAfterBinding));
            diagnostic.Append("DryFieldBoundImmediatelyBeforeRender=")
                .AppendLine((dryFieldTextureAfterBinding == _dryGroundAlbedo).ToString());

            File.WriteAllText(
                Path.Combine(_modPath, DryGroundDiagnosticsFileName),
                diagnostic.ToString());
            _dryGroundRenderDiagnosticWritten = true;
            Debug.Log(
                "[Timberborn HD] Reasserted the HD desert and dry-field textures before camera rendering.");
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning(
                $"[Timberborn HD] Could not write dry-ground render diagnostics: {exception.Message}");
        }
    }

    private static string DescribeTexture(Texture? texture)
    {
        return texture == null
            ? "<null>"
            : $"{texture.name}|{texture.width}x{texture.height}|InstanceId={texture.GetInstanceID()}";
    }

    private static int ApplyTreeTextureOverride(Material material, string propertyName)
    {
        if (!material.HasProperty(propertyName))
        {
            return 0;
        }

        var currentTexture = material.GetTexture(propertyName);
        if (currentTexture == null
            || !TreeTextureOverrides.TryGetValue(currentTexture.name, out var replacement)
            || currentTexture == replacement)
        {
            return 0;
        }

        material.SetTexture(propertyName, replacement);
        return 1;
    }

    private static IEnumerator PrepareTreeTextureOverrides()
    {
        var texturesByName = Resources.FindObjectsOfTypeAll<Texture2D>()
            .Where(texture => texture != null && !string.IsNullOrWhiteSpace(texture.name))
            .GroupBy(texture => texture.name)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(texture => texture.width * texture.height).First());
        var preparedTextures = 0;

        foreach (var specification in TreeTextureSpecifications)
        {
            if (TreeTextureOverrides.ContainsKey(specification.SourceName)
                || !texturesByName.TryGetValue(specification.SourceName, out var source))
            {
                continue;
            }

            var replacement = CreateUpscaledTexture(
                source,
                specification.ReplacementName,
                specification.Linear,
                specification.SharpenAlbedo,
                specification.NormalizeNormals);
            TreeTextureOverrides.Add(specification.SourceName, replacement);
            preparedTextures++;
            yield return null;
        }

        Debug.Log($"[Timberborn HD] Prepared {preparedTextures} runtime 2K tree atlases.");
    }

    private static Texture2D CreateUpscaledTexture(
        Texture2D source,
        string textureName,
        bool linear,
        bool sharpenAlbedo,
        bool normalizeNormals)
    {
        const int targetSize = 2048;
        var previousActive = RenderTexture.active;
        var renderTexture = RenderTexture.GetTemporary(
            targetSize,
            targetSize,
            0,
            RenderTextureFormat.ARGB32,
            linear ? RenderTextureReadWrite.Linear : RenderTextureReadWrite.sRGB);
        Texture2D? result = null;

        try
        {
            Graphics.Blit(source, renderTexture);
            RenderTexture.active = renderTexture;
            result = new Texture2D(targetSize, targetSize, TextureFormat.RGBA32, true, linear)
            {
                name = textureName,
                wrapMode = source.wrapMode,
                filterMode = FilterMode.Trilinear,
                anisoLevel = MaximumAnisotropicLevel
            };
            result.ReadPixels(new Rect(0, 0, targetSize, targetSize), 0, 0, false);

            var pixels = result.GetPixels32();
            if (sharpenAlbedo)
            {
                SharpenColorPixels(pixels, targetSize, targetSize);
            }

            if (normalizeNormals)
            {
                NormalizeNormalPixels(pixels);
            }

            result.SetPixels32(pixels);
            result.Apply(true, false);
            return result;
        }
        catch
        {
            if (result != null)
            {
                Object.Destroy(result);
            }

            throw;
        }
        finally
        {
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(renderTexture);
        }
    }

    private static void SharpenColorPixels(Color32[] pixels, int width, int height)
    {
        const float amount = 0.28f;
        var source = (Color32[])pixels.Clone();

        for (var y = 1; y < height - 1; y++)
        {
            var row = y * width;
            for (var x = 1; x < width - 1; x++)
            {
                var index = row + x;
                var center = source[index];
                if (center.a == 0)
                {
                    continue;
                }

                var left = source[index - 1];
                var right = source[index + 1];
                var down = source[index - width];
                var up = source[index + width];
                pixels[index] = new Color32(
                    SharpenChannel(center.r, left.r, right.r, down.r, up.r, amount),
                    SharpenChannel(center.g, left.g, right.g, down.g, up.g, amount),
                    SharpenChannel(center.b, left.b, right.b, down.b, up.b, amount),
                    center.a);
            }
        }
    }

    private static byte SharpenChannel(
        byte center,
        byte left,
        byte right,
        byte down,
        byte up,
        float amount)
    {
        var neighborAverage = (left + right + down + up) * 0.25f;
        return (byte)Mathf.Clamp(Mathf.RoundToInt(center + (center - neighborAverage) * amount), 0, 255);
    }

    private static void NormalizeNormalPixels(Color32[] pixels)
    {
        for (var index = 0; index < pixels.Length; index++)
        {
            var pixel = pixels[index];
            var x = pixel.r / 127.5f - 1f;
            var y = pixel.g / 127.5f - 1f;
            var z = pixel.b / 127.5f - 1f;
            var length = Mathf.Sqrt(x * x + y * y + z * z);
            if (length < 0.0001f)
            {
                pixels[index] = new Color32(128, 128, 255, pixel.a);
                continue;
            }

            pixels[index] = new Color32(
                (byte)Mathf.Clamp(Mathf.RoundToInt((x / length * 0.5f + 0.5f) * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt((y / length * 0.5f + 0.5f) * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt((z / length * 0.5f + 0.5f) * 255f), 0, 255),
                pixel.a);
        }
    }

    private readonly struct TreeTextureSpecification
    {
        public TreeTextureSpecification(
            string sourceName,
            string replacementName,
            bool linear,
            bool sharpenAlbedo,
            bool normalizeNormals)
        {
            SourceName = sourceName;
            ReplacementName = replacementName;
            Linear = linear;
            SharpenAlbedo = sharpenAlbedo;
            NormalizeNormals = normalizeNormals;
        }

        public string SourceName { get; }
        public string ReplacementName { get; }
        public bool Linear { get; }
        public bool SharpenAlbedo { get; }
        public bool NormalizeNormals { get; }
    }

    private static void WriteTreeTextureDumps()
    {
        if (string.IsNullOrWhiteSpace(_modPath))
        {
            return;
        }

        try
        {
            var dumpDirectory = Path.Combine(_modPath, TreeDumpDirectoryName);
            Directory.CreateDirectory(dumpDirectory);
            var dumpedTextures = 0;
            var textures = Resources.FindObjectsOfTypeAll<Texture2D>()
                .Where(texture => texture != null && TreeTextureNames.Contains(texture.name))
                .GroupBy(texture => texture.name)
                .Select(group => group.OrderByDescending(texture => texture.width * texture.height).First());

            foreach (var texture in textures)
            {
                var dumpPath = Path.Combine(dumpDirectory, $"{texture.name}.png");
                if (File.Exists(dumpPath))
                {
                    continue;
                }

                WriteTextureAsPng(texture, dumpPath);
                dumpedTextures++;
            }

            Debug.Log($"[Timberborn HD] Exported {dumpedTextures} tree reference textures to {dumpDirectory}");
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning($"[Timberborn HD] Could not export tree reference textures: {exception.Message}");
        }
    }

    private static void WriteTerrainTextureDumps()
    {
        if (string.IsNullOrWhiteSpace(_modPath))
        {
            return;
        }

        try
        {
            var dumpDirectory = Path.Combine(_modPath, TerrainDumpDirectoryName);
            Directory.CreateDirectory(dumpDirectory);
            var dumpedTextures = 0;
            var textures = Resources.FindObjectsOfTypeAll<Texture2D>()
                .Where(texture => texture != null && TerrainTextureNames.Contains(texture.name))
                .GroupBy(texture => texture.name)
                .Select(group => group.OrderByDescending(texture => texture.width * texture.height).First());

            foreach (var texture in textures)
            {
                var dumpPath = Path.Combine(dumpDirectory, $"{texture.name}.png");
                if (File.Exists(dumpPath))
                {
                    continue;
                }

                WriteTextureAsPng(texture, dumpPath);
                dumpedTextures++;
            }

            Debug.Log($"[Timberborn HD] Exported {dumpedTextures} terrain reference textures to {dumpDirectory}");
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning($"[Timberborn HD] Could not export terrain reference textures: {exception.Message}");
        }
    }

    private static void WriteTextureAsPng(Texture2D source, string outputPath)
    {
        var previousActive = RenderTexture.active;
        var renderTexture = RenderTexture.GetTemporary(
            source.width,
            source.height,
            0,
            RenderTextureFormat.ARGB32,
            RenderTextureReadWrite.Default);
        Texture2D? readableTexture = null;

        try
        {
            Graphics.Blit(source, renderTexture);
            RenderTexture.active = renderTexture;
            readableTexture = new Texture2D(
                source.width,
                source.height,
                TextureFormat.RGBA32,
                false,
                false);
            readableTexture.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0, false);
            readableTexture.Apply(false, false);
            File.WriteAllBytes(outputPath, ImageConversion.EncodeToPNG(readableTexture));
        }
        finally
        {
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(renderTexture);
            if (readableTexture != null)
            {
                Object.Destroy(readableTexture);
            }
        }
    }

    private static void WriteTextureInventory()
    {
        if (string.IsNullOrWhiteSpace(_modPath))
        {
            return;
        }

        try
        {
            var textures = Resources.FindObjectsOfTypeAll<Texture2D>()
                .Where(texture => texture != null && !string.IsNullOrWhiteSpace(texture.name))
                .GroupBy(texture => texture.name)
                .Select(group => group.OrderByDescending(texture => texture.width * texture.height).First())
                .OrderBy(texture => texture.name)
                .ToArray();

            var csv = new StringBuilder();
            csv.AppendLine("Name,Width,Height,Format,WrapMode,FilterMode,AnisoLevel");

            foreach (var texture in textures)
            {
                csv.Append(EscapeCsv(texture.name)).Append(',');
                csv.Append(texture.width).Append(',');
                csv.Append(texture.height).Append(',');
                csv.Append(texture.format).Append(',');
                csv.Append(texture.wrapMode).Append(',');
                csv.Append(texture.filterMode).Append(',');
                csv.Append(texture.anisoLevel).AppendLine();
            }

            var inventoryPath = Path.Combine(_modPath, TextureInventoryFileName);
            File.WriteAllText(inventoryPath, csv.ToString());
            Debug.Log($"[Timberborn HD] Wrote {textures.Length} texture records to {inventoryPath}");
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning($"[Timberborn HD] Could not write the texture inventory: {exception.Message}");
        }
    }

    private static void WriteMaterialInventory()
    {
        if (string.IsNullOrWhiteSpace(_modPath))
        {
            return;
        }

        try
        {
            var materials = Resources.FindObjectsOfTypeAll<Material>()
                .Where(material => material != null && !string.IsNullOrWhiteSpace(material.name))
                .OrderBy(material => material.name)
                .ThenBy(material => material.shader != null ? material.shader.name : string.Empty)
                .ToArray();

            var csv = new StringBuilder();
            csv.AppendLine("Material,Shader,Property,Texture,Width,Height");
            var uniqueBindings = new HashSet<string>();
            var bindingCount = 0;

            foreach (var material in materials)
            {
                var shaderName = material.shader != null ? material.shader.name : string.Empty;
                var materialName = material.name.Replace(" (Instance)", string.Empty);
                var propertyNames = material.GetTexturePropertyNames();

                if (propertyNames.Length == 0)
                {
                    var emptyKey = $"{materialName}\u001f{shaderName}";
                    if (!uniqueBindings.Add(emptyKey))
                    {
                        continue;
                    }

                    csv.Append(EscapeCsv(materialName)).Append(',');
                    csv.Append(EscapeCsv(shaderName)).AppendLine(",,,,");
                    continue;
                }

                foreach (var propertyName in propertyNames)
                {
                    var texture = material.GetTexture(propertyName);
                    var textureName = texture != null ? texture.name : string.Empty;
                    var bindingKey = $"{materialName}\u001f{shaderName}\u001f{propertyName}\u001f{textureName}";
                    if (!uniqueBindings.Add(bindingKey))
                    {
                        continue;
                    }

                    csv.Append(EscapeCsv(materialName)).Append(',');
                    csv.Append(EscapeCsv(shaderName)).Append(',');
                    csv.Append(EscapeCsv(propertyName)).Append(',');
                    csv.Append(EscapeCsv(textureName)).Append(',');
                    csv.Append(texture != null ? texture.width : 0).Append(',');
                    csv.Append(texture != null ? texture.height : 0).AppendLine();
                    bindingCount++;
                }
            }

            var inventoryPath = Path.Combine(_modPath, MaterialInventoryFileName);
            File.WriteAllText(inventoryPath, csv.ToString());
            Debug.Log(
                $"[Timberborn HD] Wrote {bindingCount} unique material texture bindings to {inventoryPath}");
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning($"[Timberborn HD] Could not write the material inventory: {exception.Message}");
        }
    }

    private static string EscapeCsv(string value)
    {
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
