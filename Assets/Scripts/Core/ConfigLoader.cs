using System;
using UnityEngine;
using UnityEngine.Networking;

// WebGL cannot read StreamingAssets with File IO, so the config is fetched via UnityWebRequest.
public static class ConfigLoader
{
    const string FileName = "config.json";
    const int TimeoutSeconds = 10;

    public static async Awaitable<Config> LoadAsync()
    {
        string url = BuildUrl(Application.streamingAssetsPath);

        using var request = UnityWebRequest.Get(url);
        request.timeout = TimeoutSeconds;
        await request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"Config load failed ({request.error}), using defaults.");
            return new Config();
        }

        return Config.FromJson(request.downloadHandler.text);
    }

    // On WebGL streamingAssetsPath is already a URL, in the Editor it is a local path.
    static string BuildUrl(string streamingAssetsPath)
    {
        string path = $"{streamingAssetsPath}/{FileName}";
        return path.Contains("://") ? path : new Uri(path).AbsoluteUri;
    }
}
