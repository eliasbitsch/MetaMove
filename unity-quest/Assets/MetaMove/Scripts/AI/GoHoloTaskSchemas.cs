namespace MetaMove.AI
{
    // Canonical tool schemas for the GoHolo task-primitive interface.
    // Keep JSON-schema literals here so they're version-controlled next to the code
    // that dispatches them. Feed these into ClaudeApiClient.tools at runtime.
    public static class GoHoloTaskSchemas
    {
        public const string MoveTo = @"{
            ""type"": ""object"",
            ""properties"": {
                ""frame"": { ""type"": ""string"", ""enum"": [""robot_base"", ""world"", ""tool""], ""default"": ""robot_base"" },
                ""position_m"": { ""type"": ""array"", ""items"": {""type"": ""number""}, ""minItems"": 3, ""maxItems"": 3 },
                ""orientation_rpy_deg"": { ""type"": ""array"", ""items"": {""type"": ""number""}, ""minItems"": 3, ""maxItems"": 3 },
                ""speed_mm_s"": { ""type"": ""number"", ""minimum"": 1, ""maximum"": 1000, ""default"": 100 },
                ""require_confirm"": { ""type"": ""boolean"", ""default"": true }
            },
            ""required"": [""position_m""]
        }";

        public const string Pick = @"{
            ""type"": ""object"",
            ""properties"": {
                ""object_id"": { ""type"": ""string"", ""description"": ""Grounding id from perception tier (ArUco marker, gaze-ray hit, or DINO detection).""},
                ""approach_offset_m"": { ""type"": ""number"", ""default"": 0.08 },
                ""grasp_width_m"": { ""type"": ""number"", ""default"": 0.04 }
            },
            ""required"": [""object_id""]
        }";

        public const string Place = @"{
            ""type"": ""object"",
            ""properties"": {
                ""target_frame"": { ""type"": ""string"" },
                ""position_m"": { ""type"": ""array"", ""items"": {""type"": ""number""}, ""minItems"": 3, ""maxItems"": 3 },
                ""release_height_m"": { ""type"": ""number"", ""default"": 0.02 }
            },
            ""required"": [""position_m""]
        }";

        public const string Home = @"{ ""type"": ""object"", ""properties"": {} }";
        public const string Stop = @"{ ""type"": ""object"", ""properties"": { ""reason"": {""type"":""string""} } }";

        public const string DescribeScene = @"{
            ""type"": ""object"",
            ""properties"": {
                ""detail"": { ""type"": ""string"", ""enum"": [""brief"",""full""], ""default"": ""brief"" }
            }
        }";
    }
}
