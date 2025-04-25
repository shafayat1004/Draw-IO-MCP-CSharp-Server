using System;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DrawIO.MCP.STDIO.Tests
{
    public static class TestHelpers
    {
        /// <summary>
        /// Extracts a shape ID from the result of an add_shape tool call
        /// </summary>
        public static string ExtractShapeId(object result)
        {
            var jsonStr = result.ToString();
            var jsonDoc = JsonDocument.Parse(jsonStr);
            
            // Try to extract from message text
            if (jsonDoc.RootElement.TryGetProperty("content", out var content))
            {
                foreach (var item in content.EnumerateArray())
                {
                    if (item.TryGetProperty("text", out var text))
                    {
                        var match = Regex.Match(text.GetString(), @"Added shape with ID ([a-zA-Z0-9_-]+)");
                        if (match.Success)
                        {
                            return match.Groups[1].Value;
                        }
                    }
                }
            }
            
            // Try to extract from shape_id property
            if (jsonDoc.RootElement.TryGetProperty("shape_id", out var shapeId))
            {
                return shapeId.GetString();
            }
            
            throw new InvalidOperationException("Could not extract shape ID from result");
        }
        
        /// <summary>
        /// Extracts a connector ID from the result of a connect_shapes tool call
        /// </summary>
        public static string ExtractConnectorId(object result)
        {
            var jsonStr = result.ToString();
            var jsonDoc = JsonDocument.Parse(jsonStr);
            
            // Try to extract from message text
            if (jsonDoc.RootElement.TryGetProperty("content", out var content))
            {
                foreach (var item in content.EnumerateArray())
                {
                    if (item.TryGetProperty("text", out var text))
                    {
                        var match = Regex.Match(text.GetString(), @"Connected shapes with connector ID ([a-zA-Z0-9_-]+)");
                        if (match.Success)
                        {
                            return match.Groups[1].Value;
                        }
                    }
                }
            }
            
            // Try to extract from edge_id property
            if (jsonDoc.RootElement.TryGetProperty("edge_id", out var edgeId))
            {
                return edgeId.GetString();
            }
            
            throw new InvalidOperationException("Could not extract connector ID from result");
        }
    }
} 