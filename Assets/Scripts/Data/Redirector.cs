using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

// Sends the player back to Survey B. Application.OpenURL would open a new (blocked) tab on WebGL,
// so the redirect goes through Redirect.jslib. In the Editor the URL is only logged.
public static class Redirector
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern void RedirectTo(string url);
#endif

    static bool redirected;

    // Static state survives play mode sessions when domain reload is disabled.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetState() => redirected = false;

    // Replaces {key} placeholders in the template with the URL-encoded field values.
    public static string BuildUrl(string template, IReadOnlyList<SessionData.Field> fields)
    {
        string url = template ?? "";
        foreach (var f in fields)
            url = url.Replace("{" + f.Key + "}", Uri.EscapeDataString(f.Value));
        return url;
    }

    // Returns false if there is no URL to go to. Only the first call redirects.
    public static bool Go(string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            Debug.LogError("No returnUrlTemplate configured, cannot redirect.");
            return false;
        }
        if (redirected)
            return true;
        redirected = true;

#if UNITY_WEBGL && !UNITY_EDITOR
        RedirectTo(url);
#else
        Debug.Log("Redirect (Editor): " + url);
#endif
        return true;
    }
}
