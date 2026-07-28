using System.Collections;
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
    private const string InventoryFileName = "TimberbornHD-textures.csv";

    public static GraphicsEnhancer? Instance { get; private set; }
    private static string? _modPath;

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
        SceneManager.sceneLoaded += OnSceneLoaded;
        ApplyTextureQuality();
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
        WriteTextureInventory();
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

            var inventoryPath = Path.Combine(_modPath, InventoryFileName);
            File.WriteAllText(inventoryPath, csv.ToString());
            Debug.Log($"[Timberborn HD] Wrote {textures.Length} texture records to {inventoryPath}");
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning($"[Timberborn HD] Could not write the texture inventory: {exception.Message}");
        }
    }

    private static string EscapeCsv(string value)
    {
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
