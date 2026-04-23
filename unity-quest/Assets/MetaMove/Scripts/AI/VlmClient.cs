using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace MetaMove.AI
{
    // Unity client for the local Gemma 3 VLM service (ai-services/vlm-gemma).
    // Plan step 13c4 — natural-language object description.
    //
    // Contract: POST http://<host>:<port>/describe
    //   body:  { "image_b64": "<base64 jpg/png>", "prompt": "<optional>", "max_tokens": 128 }
    //   resp:  { "text": "...", "latency_ms": 1234, "model": "gemma3:4b" }
    //
    // Frame source: Quest 3 Passthrough Camera API (PCA, v74+). This script
    // does not fetch frames itself — callers pass an encoded JPEG byte[] to
    // keep the camera-permission bookkeeping in one place (PassthroughFrameSource).
    public class VlmClient : MonoBehaviour
    {
        [Header("Server")]
        [Tooltip("Full URL to the /describe endpoint of vlm-gemma service.")]
        public string endpoint = "http://192.168.125.80:8770/describe";
        [Range(2f, 60f)] public float timeoutSeconds = 20f;

        [Header("Defaults")]
        [TextArea(2, 4)]
        public string defaultPrompt =
            "Describe what is in the image in one or two short sentences. " +
            "If a person is holding something, name the object and its likely purpose.";
        [Range(32, 512)] public int maxTokens = 128;

        [Header("Debug")]
        public bool logRequests = true;

        [Serializable]
        class Request
        {
            public string image_b64;
            public string prompt;
            public int max_tokens;
        }

        [Serializable]
        class Response
        {
            public string text;
            public int latency_ms;
            public string model;
        }

        // Public API. Caller supplies raw JPEG/PNG bytes (from PassthroughCamera
        // capture or a fallback stub) and an optional prompt override. The
        // onResult callback receives the model's text, or null on failure.
        public void Describe(byte[] imageBytes, string prompt, Action<string> onResult) =>
            StartCoroutine(DescribeRoutine(imageBytes, prompt, onResult));

        IEnumerator DescribeRoutine(byte[] imageBytes, string prompt, Action<string> onResult)
        {
            if (imageBytes == null || imageBytes.Length == 0)
            {
                Debug.LogWarning("[VlmClient] empty image bytes");
                onResult?.Invoke(null);
                yield break;
            }

            var req = new Request
            {
                image_b64 = Convert.ToBase64String(imageBytes),
                prompt = string.IsNullOrEmpty(prompt) ? defaultPrompt : prompt,
                max_tokens = maxTokens,
            };
            string json = JsonUtility.ToJson(req);

            using var uwr = new UnityWebRequest(endpoint, "POST");
            uwr.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            uwr.downloadHandler = new DownloadHandlerBuffer();
            uwr.SetRequestHeader("Content-Type", "application/json");
            uwr.timeout = Mathf.CeilToInt(timeoutSeconds);

            if (logRequests) Debug.Log($"[VlmClient] → {endpoint} ({imageBytes.Length} bytes)");

            yield return uwr.SendWebRequest();

            if (uwr.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[VlmClient] request failed: {uwr.error}");
                onResult?.Invoke(null);
                yield break;
            }

            Response resp;
            try { resp = JsonUtility.FromJson<Response>(uwr.downloadHandler.text); }
            catch (Exception e)
            {
                Debug.LogWarning($"[VlmClient] JSON parse failed: {e.Message}");
                onResult?.Invoke(null);
                yield break;
            }

            if (logRequests) Debug.Log($"[VlmClient] ← {resp.model} ({resp.latency_ms} ms): {resp.text}");
            onResult?.Invoke(resp.text);
        }

        // Convenience for Spatial Pinch + voice flow: point at an object, speak
        // "what is this?", caller builds byte[] from the current Passthrough
        // frame and passes it in.
        public void WhatIsThis(byte[] imageBytes, Action<string> onResult) =>
            Describe(imageBytes, "What is this object? Identify it in one short sentence.", onResult);

        public void WhatAmIHolding(byte[] imageBytes, Action<string> onResult) =>
            Describe(imageBytes, "What am I holding? Identify the object in one short sentence.", onResult);
    }
}
