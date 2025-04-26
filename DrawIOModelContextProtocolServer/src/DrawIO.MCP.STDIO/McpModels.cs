using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DrawIO.MCP.STDIO
{
    // MCP JSON-RPC models
    public class McpRequest
    {
        [JsonPropertyName("jsonrpc")]
        public string JsonRpc { get; set; } = "2.0";
        
        [JsonPropertyName("id")]
        [JsonConverter(typeof(RequestIdConverter))]
        public string Id { get; set; }
        
        [JsonPropertyName("method")]
        public string Method { get; set; }
        
        [JsonPropertyName("params")]
        public JsonElement? Params { get; set; }
    }

    public class McpResponse
    {
        [JsonPropertyName("jsonrpc")]
        public string JsonRpc { get; set; } = "2.0";
        
        [JsonPropertyName("id")]
        [JsonConverter(typeof(RequestIdConverter))]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Id { get; set; }
        
        [JsonPropertyName("result")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public object Result { get; set; }
        
        [JsonPropertyName("error")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public McpErrorDetail Error { get; set; }
        
        public static McpResponse CreateResult(string id, object result)
        {
            if (result == null)
            {
                // For null results, return an empty object to satisfy JSON-RPC spec
                result = new { };
            }
            
            return new McpResponse
            {
                Id = id ?? "null",
                JsonRpc = "2.0",
                Result = result,
                Error = null
            };
        }
        
        public static McpResponse CreateError(string id, int code, string message, object details = null)
        {
            return new McpResponse
            {
                Id = id ?? "null",
                JsonRpc = "2.0",
                Result = null,
                Error = new McpErrorDetail
                {
                    Code = code,
                    Message = message,
                    Details = details
                }
            };
        }
        
        public static McpResponse CreateNotification()
        {
            // For notifications, only include jsonrpc version
            return new McpResponse
            {
                JsonRpc = "2.0"
            };
        }
    }

    public class McpErrorDetail
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }
        
        [JsonPropertyName("message")]
        public string Message { get; set; }
        
        [JsonPropertyName("data")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public object Details { get; set; }
    }

    // Tool Parameters Model
    public class ToolParameterInfo
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string Description { get; set; }
        public bool Required { get; set; }
    }
    
    // MCP Tool definition for ListToolsAsync
    public class McpToolDefinition
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }
        
        [JsonPropertyName("description")]
        public string Description { get; set; }
        
        [JsonPropertyName("inputSchema")]
        public object InputSchema => new
        {
            type = "object",
            properties = SchemaInputs,
            required = SchemaInputs.Where(p => p.Value.Required).Select(p => p.Key).ToArray()
        };
        
        // Not directly serialized, used to build InputSchema
        [JsonIgnore]
        public Dictionary<string, McpParameterDefinition> SchemaInputs { get; set; }
    }

    // MCP Parameter definition for tool inputs
    public class McpParameterDefinition
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }
        
        [JsonPropertyName("description")]
        public string Description { get; set; }
        
        [JsonIgnore] // We handle required at the parent level in the InputSchema property
        public bool Required { get; set; }

        [JsonPropertyName("items")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] // Only include 'items' for array types
        public McpParameterDefinition Items { get; set; }
    }
    
    // Custom JSON converter to handle both string and integer IDs
    public class RequestIdConverter : JsonConverter<string>
    {
        public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Number)
            {
                return reader.GetInt64().ToString();
            }

            if (reader.TokenType == JsonTokenType.String)
            {
                return reader.GetString();
            }

            throw new JsonException($"Unexpected token type when converting request ID: {reader.TokenType}");
        }

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value);
        }
    }
} 