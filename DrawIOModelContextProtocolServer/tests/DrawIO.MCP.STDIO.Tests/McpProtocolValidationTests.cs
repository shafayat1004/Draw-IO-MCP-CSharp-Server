using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace DrawIO.MCP.STDIO.Tests
{
    public class McpProtocolValidationTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly string _tempDiagramsDir;
        private readonly McpRequestDispatcher _dispatcher;
        private readonly StringWriter _logWriter;

        public McpProtocolValidationTests(ITestOutputHelper output)
        {
            _output = output;
            _tempDiagramsDir = Path.Combine(Path.GetTempPath(), $"mcp-proto-tests-{Guid.NewGuid()}");
            Directory.CreateDirectory(_tempDiagramsDir);
            _logWriter = new StringWriter();
            _dispatcher = new McpRequestDispatcher(_tempDiagramsDir, true, _logWriter);
        }

        public void Dispose()
        {
            _logWriter.Dispose();
            if (Directory.Exists(_tempDiagramsDir))
            {
                try
                {
                    Directory.Delete(_tempDiagramsDir, true);
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"Error cleaning up temp directory: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Helper method to validate JSON-RPC response format according to the MCP specification.
        /// Note: This handles both traditional error responses and tool execution error dictionaries.
        /// </summary>
        private void ValidateJsonRpcResponse(McpResponse response, string expectedId, bool expectError = false)
        {
            // Check required JSON-RPC 2.0 fields
            Assert.Equal("2.0", response.JsonRpc);
            Assert.Equal(expectedId, response.Id);
            
            if (expectError)
            {
                // Error responses can be in two forms in our implementation:
                // 1. Standard JSON-RPC error: Error property set, Result is null
                // 2. Tool execution error: Result contains a dictionary with error info
                
                if (response.Error != null)
                {
                    // Case 1: Standard JSON-RPC error 
                    Assert.Null(response.Result);
                    Assert.True(response.Error.Code != 0);
                    Assert.False(string.IsNullOrEmpty(response.Error.Message));
                    _output.WriteLine($"Found standard JSON-RPC error: {response.Error.Message}");
                }
                else 
                {
                    // Case 2: Tool execution error in Result dictionary
                    Assert.NotNull(response.Result);
                    
                    var resultJson = JsonSerializer.Serialize(response.Result);
                    _output.WriteLine($"Testing result for error dictionary: {resultJson}");
                    
                    var resultObj = JsonDocument.Parse(resultJson).RootElement;
                    Assert.True(resultObj.TryGetProperty("error", out _), 
                        "Error response should contain 'error' property in result");
                }
            }
            else
            {
                // For success responses, Result should not be null and Error should be null
                Assert.NotNull(response.Result);
                Assert.Null(response.Error);
            }
        }
        
        [Fact]
        public async Task ProtocolVersion_NegotiationConflict_ShouldFallbackToDefault()
        {
            // Arrange - Request an unsupported protocol version
            var request = new McpRequest
            {
                Id = "protocol-1",
                JsonRpc = "2.0",
                Method = "mcp/initialize",
                Params = JsonDocument.Parse("{\"protocolVersion\":\"1.0.0\"}").RootElement
            };

            // Act
            var response = await _dispatcher.DispatchRequestAsync(request);

            // Assert
            ValidateJsonRpcResponse(response, "protocol-1");
            
            // Check that we got a supported protocol version back (not the unsupported one we requested)
            var resultJson = JsonSerializer.Serialize(response.Result);
            var resultObj = JsonDocument.Parse(resultJson).RootElement;
            Assert.True(resultObj.TryGetProperty("protocolVersion", out var protocolVersion));
            string? version = protocolVersion.GetString();
            Assert.NotNull(version);
            
            _output.WriteLine($"Negotiated protocol version: {version}");
            Assert.NotEqual("1.0.0", version);
            
            // Should be one of our supported versions
            Assert.Contains(version, new[] { "2024-11-05", "2025-03-26" });
        }
        
        [Fact]
        public async Task ProtocolVersion_SupportedVersion_ShouldBeAccepted()
        {
            // Arrange - Request a supported protocol version
            var request = new McpRequest
            {
                Id = "protocol-2",
                JsonRpc = "2.0",
                Method = "mcp/initialize",
                Params = JsonDocument.Parse("{\"protocolVersion\":\"2024-11-05\"}").RootElement
            };

            // Act
            var response = await _dispatcher.DispatchRequestAsync(request);

            // Assert
            ValidateJsonRpcResponse(response, "protocol-2");
            
            // Check that we got the protocol version we requested
            var resultJson = JsonSerializer.Serialize(response.Result);
            var resultObj = JsonDocument.Parse(resultJson).RootElement;
            Assert.True(resultObj.TryGetProperty("protocolVersion", out var protocolVersion));
            string? version = protocolVersion.GetString();
            Assert.NotNull(version);
            
            _output.WriteLine($"Negotiated protocol version: {version}");
            Assert.Equal("2024-11-05", version);
        }
        
        [Fact]
        public async Task JsonRpc_ErrorResponses_ShouldFollowSpec()
        {
            // Arrange - Create a request with an invalid method
            var request = new McpRequest
            {
                Id = "err-test",
                JsonRpc = "2.0",
                Method = "invalid/method",
                Params = JsonDocument.Parse("{}").RootElement
            };

            // Act
            var response = await _dispatcher.DispatchRequestAsync(request);

            // Assert
            ValidateJsonRpcResponse(response, "err-test", expectError: true);
            
            // Check error code and message format according to JSON-RPC spec
            Assert.Equal(-32601, response.Error.Code);  // Method not found error code
            Assert.Contains("not found", response.Error.Message, StringComparison.OrdinalIgnoreCase);
            
            string errorJson = JsonSerializer.Serialize(response);
            _output.WriteLine($"Error response: {errorJson}");
            
            // Check the serialized JSON format
            var jsonDoc = JsonDocument.Parse(errorJson);
            var root = jsonDoc.RootElement;
            
            Assert.True(root.TryGetProperty("jsonrpc", out _));
            Assert.True(root.TryGetProperty("id", out _));
            Assert.True(root.TryGetProperty("error", out var error));
            Assert.True(error.TryGetProperty("code", out _));
            Assert.True(error.TryGetProperty("message", out _));
        }
        
        [Fact]
        public async Task ToolExecution_MissingParameters_ShouldReturnError()
        {
            // Arrange - Call a tool without required parameters
            var request = new McpRequest
            {
                Id = "missing-params",
                JsonRpc = "2.0",
                Method = "tools/execute",
                Params = JsonDocument.Parse("{\"tool\":\"create_new_diagram\",\"parameters\":{}}").RootElement
            };

            // Act
            var response = await _dispatcher.DispatchRequestAsync(request);

            // Assert - This should be an error response 
            ValidateJsonRpcResponse(response, "missing-params", expectError: true);
            _output.WriteLine($"Full response: {JsonSerializer.Serialize(response)}");
            
            // Tool errors return in the Result field, not the Error field
            Assert.NotNull(response.Result);
            
            // Serialize and check it contains error information
            var resultJson = JsonSerializer.Serialize(response.Result);
            _output.WriteLine($"Result: {resultJson}");
            
            var resultObj = JsonDocument.Parse(resultJson).RootElement;
            Assert.True(resultObj.TryGetProperty("error", out var errorEl), 
                "Response should contain an 'error' property in result");
            
            // Error should mention the missing parameter
            string? errorMsg = errorEl.GetString();
            Assert.NotNull(errorMsg);
            Assert.Contains("name", errorMsg, StringComparison.OrdinalIgnoreCase);
        }
        
        [Fact]
        public void McpCreatedResponseObjects_ShouldMatchJsonRpcSpec()
        {
            // Test static factory methods for response creation
            
            // Success response with result
            var successResp = McpResponse.CreateResult("test-id", new { value = "test" });
            Assert.Equal("2.0", successResp.JsonRpc);
            Assert.Equal("test-id", successResp.Id);
            Assert.NotNull(successResp.Result);
            Assert.Null(successResp.Error);
            
            // Error response
            var errorResp = McpResponse.CreateError("test-id", -32000, "Test error", new { details = "error info" });
            Assert.Equal("2.0", errorResp.JsonRpc);
            Assert.Equal("test-id", errorResp.Id);
            Assert.Null(errorResp.Result);
            Assert.NotNull(errorResp.Error);
            Assert.Equal(-32000, errorResp.Error.Code);
            Assert.Equal("Test error", errorResp.Error.Message);
            Assert.NotNull(errorResp.Error.Details);
            
            // Notification (no ID)
            var notification = McpResponse.CreateNotification();
            Assert.Equal("2.0", notification.JsonRpc);
            Assert.Null(notification.Id);
        }
        
        [Fact]
        public async Task ListTools_ResponseFormat_ShouldFollowMcpSpec()
        {
            // Arrange
            var request = new McpRequest
            {
                Id = "tools-format",
                JsonRpc = "2.0",
                Method = "tools/list",
                Params = JsonDocument.Parse("{}").RootElement
            };

            // Act
            var response = await _dispatcher.DispatchRequestAsync(request);

            // Assert
            ValidateJsonRpcResponse(response, "tools-format");
            
            var resultJson = JsonSerializer.Serialize(response.Result);
            _output.WriteLine($"Tools response: {resultJson}");
            
            // Check response format according to MCP spec
            var resultObj = JsonDocument.Parse(resultJson).RootElement;
            Assert.True(resultObj.TryGetProperty("tools", out var tools));
            Assert.True(tools.ValueKind == JsonValueKind.Array);
            
            // Check individual tool format if tools exist
            if (tools.GetArrayLength() > 0)
            {
                var firstTool = tools[0];
                Assert.True(firstTool.TryGetProperty("name", out _));
                Assert.True(firstTool.TryGetProperty("description", out _));
                Assert.True(firstTool.TryGetProperty("inputSchema", out var schema));
                
                // Input schema should follow JSON Schema format
                Assert.True(schema.TryGetProperty("type", out var type));
                Assert.Equal("object", type.GetString());
                Assert.True(schema.TryGetProperty("properties", out _));
            }
        }
    }
} 