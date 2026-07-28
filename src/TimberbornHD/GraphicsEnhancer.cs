using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TimberbornHD;

public sealed class GraphicsEnhancer : MonoBehaviour
{
    private const int MaximumAnisotropicLevel = 16;
    private const float SceneLoadDelaySeconds = 1.0f;
    private const string TextureInventoryFileName = "TimberbornHD-textures.csv";
    private const string MaterialInventoryFileName = "TimberbornHD-materials.csv";
    private const string DirtShaderName = "Shader Graphs/DirtURP";
    private const string DirtTextureProperty = "_MainTex";
    private const string OriginalDirtTextureName = "Dirt";
    private const string SoilAlbedoRelativePath = "Textures/Soil/soil-neutral-albedo.png";

    public static GraphicsEnhancer? Instance { get; private set; }
    private static string? _modPath;
    private static Texture2D? _soilAlbedo;

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
        ApplyTextureQuality();
        ApplyMaterialOverrides();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode loadSceneMode)
    {
        StartCoroutine(ApplyAfterSceneLoad());
    }

    private static IEnumerator ApplyAfterSceneLoad()
    {
        yield return new WaitForSecondsRealtime(SceneLoadDelaySeconds);
        ApplyTextureQuality();
        ApplyMaterialOverrides();
        WriteTextureInventory();
        WriteMaterialInventory();
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
        if (string.IsNullOrWhiteSpace(_modPath) || _soilAlbedo != null)
        {
            return;
        }

        try
        {
            var texturePath = Path.Combine(_modPath, SoilAlbedoRelativePath);
            var textureBytes = File.ReadAllBytes(texturePath);
            var texture = new Texture2D(2, 2, TextureFormat.RGB24, true, false)
            {
                name = "TimberbornHD_SoilNeutral_Albedo",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = MaximumAnisotropicLevel
            };

            if (!ImageConversion.LoadImage(texture, textureBytes, false))
            {
                Object.Destroy(texture);
                throw new InvalidDataException($"Unity could not decode {texturePath}.");
            }

            _soilAlbedo = texture;
            Debug.Log($"[Timberborn HD] Loaded 2K soil albedo from {texturePath}");
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning($"[Timberborn HD] Could not load the soil albedo: {exception.Message}");
        }
    }

    private static void ApplyMaterialOverrides()
    {
        if (_soilAlbedo == null)
        {
            return;
        }

        var changedMaterials = 0;
        var materials = Resources.FindObjectsOfTypeAll<Material>();

        foreach (var material in materials)
        {
            if (material == null
                || material.shader == null
                || material.shader.name != DirtShaderName
                || !material.HasProperty(DirtTextureProperty))
            {
                continue;
            }

            var currentTexture = material.GetTexture(DirtTextureProperty);
            if (currentTexture == _soilAlbedo || currentTexture == null || currentTexture.name != OriginalDirtTextureName)
            {
                continue;
            }

            material.SetTexture(DirtTextureProperty, _soilAlbedo);
            changedMaterials++;
        }

        Debug.Log($"[Timberborn HD] Applied the 2K soil albedo to {changedMaterials} DirtURP materials.");
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
