using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

// Posts the results to the Google Apps Script web app.
// Content-Type text/plain keeps it a "simple" request, so the browser sends no CORS preflight
// (Apps Script does not answer OPTIONS).
public class ResultSubmitter : IResultSubmitter
{
    readonly string url;

    public ResultSubmitter(string url)
    {
        this.url = url;
    }

    public async Awaitable<bool> Submit(IReadOnlyList<SessionData.Field> fields, float timeoutSeconds)
    {
        if (string.IsNullOrEmpty(url))
        {
            Debug.LogWarning("No appsScriptUrl configured, skipping submit.");
            return false;
        }

        // A failed backup must never block the redirect, e.g. SendWebRequest throws for http:// URLs.
        try
        {
            return await Post(fields, timeoutSeconds);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Submit failed: {e.Message}");
            return false;
        }
    }

    async Awaitable<bool> Post(IReadOnlyList<SessionData.Field> fields, float timeoutSeconds)
    {
        string json = ResultJson.Build(fields);
        using var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
        request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "text/plain;charset=utf-8");

        // Own timeout instead of UnityWebRequest.timeout, which is not reliable on WebGL.
        var operation = request.SendWebRequest();
        double deadline = Time.realtimeSinceStartupAsDouble + timeoutSeconds;
        while (!operation.isDone && Time.realtimeSinceStartupAsDouble < deadline)
            await Awaitable.NextFrameAsync();

        if (!operation.isDone)
        {
            request.Abort();
            Debug.LogWarning($"Submit timed out after {timeoutSeconds} s.");
            return false;
        }
        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"Submit failed: {request.error}");
            return false;
        }
        return true;
    }
}
