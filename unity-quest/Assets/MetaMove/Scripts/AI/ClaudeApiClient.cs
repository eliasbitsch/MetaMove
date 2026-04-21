using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace MetaMove.AI
{
    // Thin Unity client for the Anthropic Messages API with tool-use support.
    // Designed around GoHolo's task-primitive JSON schema (move_to, pick, place, home, ...).
    //
    // Not a full SDK — just enough surface to round-trip a user utterance into a typed tool call
    // that the task executor can dispatch.
    public class ClaudeApiClient : MonoBehaviour
    {
        [Header("API")]
        [Tooltip("Anthropic API key. Prefer loading from StreamingAssets or env var, NOT hard-coding.")]
        public string apiKey = "";
        public string model = "claude-opus-4-7";
        public string apiVersion = "2023-06-01";
        public string endpoint = "https://api.anthropic.com/v1/messages";

        [Header("Behavior")]
        [Range(1, 8192)] public int maxTokens = 1024;
        public float temperature = 0.0f;
        public string systemPrompt =
            "You control an ABB GoFa 6-DOF cobot via a tool-use interface. " +
            "Prefer calling a single tool per user turn. Refuse unsafe requests. " +
            "Units: meters for positions, degrees for angles. Frames: robot_base unless noted.";

        public List<Tool> tools = new List<Tool>();

        [Serializable]
        public class Tool
        {
            public string name;
            public string description;
            // JSON schema for the input_schema field (stored as raw JSON string so callers
            // can paste schemas from the tool-use spec verbatim).
            [TextArea(3, 12)] public string inputSchemaJson;
        }

        public class ToolCall
        {
            public string id;
            public string name;
            public string inputJson;
            public override string ToString() => $"{name}({inputJson})";
        }

        public class Response
        {
            public string stopReason;
            public string text;
            public readonly List<ToolCall> toolCalls = new List<ToolCall>();
            public string rawJson;
            public string error;
            public bool Ok => string.IsNullOrEmpty(error);
        }

        public IEnumerator SendMessage(string userMessage, Action<Response> onDone)
        {
            string body = BuildRequest(userMessage);
            using var req = new UnityWebRequest(endpoint, "POST");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("x-api-key", apiKey);
            req.SetRequestHeader("anthropic-version", apiVersion);

            yield return req.SendWebRequest();

            var resp = new Response { rawJson = req.downloadHandler?.text };
            if (req.result != UnityWebRequest.Result.Success)
            {
                resp.error = $"HTTP {req.responseCode}: {req.error}\n{resp.rawJson}";
                onDone?.Invoke(resp);
                yield break;
            }

            try { ParseResponse(resp.rawJson, resp); }
            catch (Exception e) { resp.error = $"parse: {e.Message}"; }

            onDone?.Invoke(resp);
        }

        string BuildRequest(string userMessage)
        {
            var sb = new StringBuilder(512);
            sb.Append('{');
            sb.Append("\"model\":").Append(JsonString(model)).Append(',');
            sb.Append("\"max_tokens\":").Append(maxTokens).Append(',');
            sb.Append("\"temperature\":").Append(temperature.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(',');
            if (!string.IsNullOrEmpty(systemPrompt))
                sb.Append("\"system\":").Append(JsonString(systemPrompt)).Append(',');
            if (tools != null && tools.Count > 0)
            {
                sb.Append("\"tools\":[");
                for (int i = 0; i < tools.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    var t = tools[i];
                    sb.Append('{');
                    sb.Append("\"name\":").Append(JsonString(t.name)).Append(',');
                    sb.Append("\"description\":").Append(JsonString(t.description ?? "")).Append(',');
                    sb.Append("\"input_schema\":").Append(string.IsNullOrEmpty(t.inputSchemaJson) ? "{}" : t.inputSchemaJson);
                    sb.Append('}');
                }
                sb.Append("],");
            }
            sb.Append("\"messages\":[{\"role\":\"user\",\"content\":").Append(JsonString(userMessage)).Append("}]");
            sb.Append('}');
            return sb.ToString();
        }

        static string JsonString(string s)
        {
            var sb = new StringBuilder(s.Length + 2);
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.AppendFormat("\\u{0:X4}", (int)c);
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }

        static void ParseResponse(string json, Response resp)
        {
            if (string.IsNullOrEmpty(json)) { resp.error = "empty body"; return; }
            resp.stopReason = ExtractStringField(json, "\"stop_reason\"");

            int contentStart = json.IndexOf("\"content\":[", StringComparison.Ordinal);
            if (contentStart < 0) return;
            int i = contentStart + "\"content\":[".Length;
            int depth = 1;
            int blockStart = -1;
            while (i < json.Length && depth > 0)
            {
                char c = json[i];
                if (c == '{') { if (depth == 1) blockStart = i; depth++; }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 1 && blockStart >= 0)
                    {
                        string block = json.Substring(blockStart, i - blockStart + 1);
                        ParseContentBlock(block, resp);
                        blockStart = -1;
                    }
                }
                else if (c == ']' && depth == 1) depth = 0;
                i++;
            }
        }

        static void ParseContentBlock(string block, Response resp)
        {
            string type = ExtractStringField(block, "\"type\"");
            if (type == "text")
            {
                string text = ExtractStringField(block, "\"text\"");
                if (!string.IsNullOrEmpty(text))
                    resp.text = string.IsNullOrEmpty(resp.text) ? text : resp.text + "\n" + text;
            }
            else if (type == "tool_use")
            {
                var tc = new ToolCall
                {
                    id = ExtractStringField(block, "\"id\""),
                    name = ExtractStringField(block, "\"name\""),
                    inputJson = ExtractRawField(block, "\"input\"")
                };
                resp.toolCalls.Add(tc);
            }
        }

        static string ExtractStringField(string s, string key)
        {
            int k = s.IndexOf(key, StringComparison.Ordinal);
            if (k < 0) return null;
            int colon = s.IndexOf(':', k); if (colon < 0) return null;
            int q = s.IndexOf('"', colon + 1); if (q < 0) return null;
            var sb = new StringBuilder();
            int i = q + 1;
            while (i < s.Length)
            {
                char c = s[i];
                if (c == '\\' && i + 1 < s.Length)
                {
                    char n = s[i + 1];
                    switch (n)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        default: sb.Append(n); break;
                    }
                    i += 2;
                }
                else if (c == '"') return sb.ToString();
                else { sb.Append(c); i++; }
            }
            return sb.ToString();
        }

        static string ExtractRawField(string s, string key)
        {
            int k = s.IndexOf(key, StringComparison.Ordinal);
            if (k < 0) return null;
            int colon = s.IndexOf(':', k); if (colon < 0) return null;
            int i = colon + 1;
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
            if (i >= s.Length) return null;
            char open = s[i], close;
            if (open == '{') close = '}';
            else if (open == '[') close = ']';
            else return ExtractStringField(s, key);
            int depth = 0, start = i;
            bool inString = false;
            for (; i < s.Length; i++)
            {
                char c = s[i];
                if (inString) { if (c == '\\') i++; else if (c == '"') inString = false; continue; }
                if (c == '"') inString = true;
                else if (c == open) depth++;
                else if (c == close) { depth--; if (depth == 0) return s.Substring(start, i - start + 1); }
            }
            return null;
        }
    }
}
