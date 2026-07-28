using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TimberbornHD;

public sealed class GraphicsEnhancer : MonoBehaviour
{
    private const int MaximumAnisotropicLevel = 16;
    private const float SceneLoadDelaySeconds = 1.0f;

    public static GraphicsEnhancer? Instance { get; private set; }

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
}

