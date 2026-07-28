using Timberborn.ModManagerScene;
using UnityEngine;

namespace TimberbornHD;

public sealed class TimberbornHdModStarter : IModStarter
{
    public void StartMod(IModEnvironment modEnvironment)
    {
        if (GraphicsEnhancer.Instance != null)
        {
            return;
        }

        var host = new GameObject("TimberbornHD");
        Object.DontDestroyOnLoad(host);
        host.AddComponent<GraphicsEnhancer>();

        Debug.Log($"[Timberborn HD] Loaded from {modEnvironment.ModPath}");
    }
}

