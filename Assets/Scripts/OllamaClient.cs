using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

public sealed class OllamaClient
{
    [Serializable]
    private sealed class GenerateRequest
    {
        public string model;
        public string prompt;
        public bool stream;
    }

    [Serializable]
    private sealed class GenerateResponse
    {
        public string response = string.Empty;
        public string error = string.Empty;
    }

    private readonly string endpoint;
    private readonly string model;
    private readonly int timeoutSeconds;

    public OllamaClient(string endpoint, string model, int timeoutSeconds)
    {
        this.endpoint = endpoint;
        this.model = model;
        this.timeoutSeconds = Mathf.Max(1, timeoutSeconds);
    }

    public async Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken)
    {
        var requestBody = new GenerateRequest
        {
            model = model,
            prompt = prompt,
            stream = false
        };

        using var request = new UnityWebRequest(endpoint, UnityWebRequest.kHttpVerbPOST);
        byte[] body = Encoding.UTF8.GetBytes(JsonUtility.ToJson(requestBody));
        request.uploadHandler = new UploadHandlerRaw(body);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.timeout = timeoutSeconds;
        request.SetRequestHeader("Content-Type", "application/json");

        UnityWebRequestAsyncOperation operation = request.SendWebRequest();

        while (!operation.isDone)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
        }

        cancellationToken.ThrowIfCancellationRequested();

        string responseText = request.downloadHandler.text;

        if (request.result != UnityWebRequest.Result.Success)
        {
            string message = ExtractError(responseText);
            if (string.IsNullOrWhiteSpace(message))
            {
                message = request.error;
            }

            throw new InvalidOperationException($"Ollama request failed: {message}");
        }

        var response = JsonUtility.FromJson<GenerateResponse>(responseText);
        if (!string.IsNullOrWhiteSpace(response.error))
        {
            throw new InvalidOperationException($"Ollama returned an error: {response.error}");
        }

        return response.response;
    }

    private static string ExtractError(string responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText))
        {
            return string.Empty;
        }

        try
        {
            return JsonUtility.FromJson<GenerateResponse>(responseText).error;
        }
        catch
        {
            return responseText;
        }
    }
}
