using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace DrawIO.MCP.STDIO.Tests
{
    public class SimplifiedMcpTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly string _tempDiagramsDir;
        private readonly McpRequestDispatcher _dispatcher;
        private readonly StringWriter _logWriter;

        public SimplifiedMcpTests(ITestOutputHelper output)
        {
            _output = output;
            _tempDiagramsDir = Path.Combine(Path.GetTempPath(), $"mcp-tests-{Guid.NewGuid()}");
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

        [Fact]
        public async Task Initialize_ShouldReturnValidMcpResponse()
        {
            // Arrange
            var request = new McpRequest
            {
                Id = "test-1",
                JsonRpc = "2.0",
                Method = "mcp/initialize",
                Params = JsonDocument.Parse("{\"clientIdentifier\":\"test-client\"}").RootElement
            };

            // Act
            var response = await _dispatcher.DispatchRequestAsync(request);

            // Assert
            Assert.Equal("test-1", response.Id);
            Assert.Equal("2.0", response.JsonRpc);
            Assert.NotNull(response.Result);
            Assert.Null(response.Error);

            var resultJson = JsonSerializer.Serialize(response.Result);
            _output.WriteLine($"Response: {resultJson}");

            // Verify the response contains required MCP initialization fields
            var resultObj = JsonDocument.Parse(resultJson).RootElement;
            Assert.True(resultObj.TryGetProperty("protocolVersion", out _));
            Assert.True(resultObj.TryGetProperty("capabilities", out var capabilities));
            Assert.True(capabilities.TryGetProperty("tools", out _));
            Assert.True(resultObj.TryGetProperty("serverInfo", out _));
        }

        [Fact]
        public async Task ListTools_ShouldReturnValidMcpResponse()
        {
            // Arrange
            var request = new McpRequest
            {
                Id = "test-2",
                JsonRpc = "2.0",
                Method = "tools/list",
                Params = JsonDocument.Parse("{}").RootElement
            };

            // Act
            var response = await _dispatcher.DispatchRequestAsync(request);

            // Assert
            Assert.Equal("test-2", response.Id);
            Assert.Equal("2.0", response.JsonRpc);
            Assert.NotNull(response.Result);
            Assert.Null(response.Error);

            var resultJson = JsonSerializer.Serialize(response.Result);
            _output.WriteLine($"Response: {resultJson}");

            // Verify the response contains tools array
            var resultObj = JsonDocument.Parse(resultJson).RootElement;
            Assert.True(resultObj.TryGetProperty("tools", out var tools));
            Assert.True(tools.ValueKind == JsonValueKind.Array);
        }

        [Fact]
        public async Task ListResources_ShouldReturnValidMcpResponse()
        {
            // Arrange
            var request = new McpRequest
            {
                Id = "test-3",
                JsonRpc = "2.0",
                Method = "resources/list",
                Params = JsonDocument.Parse("{}").RootElement
            };

            // Act
            var response = await _dispatcher.DispatchRequestAsync(request);

            // Assert
            Assert.Equal("test-3", response.Id);
            Assert.Equal("2.0", response.JsonRpc);
            Assert.NotNull(response.Result);
            Assert.Null(response.Error);

            var resultJson = JsonSerializer.Serialize(response.Result);
            _output.WriteLine($"Response: {resultJson}");

            // Verify the response contains resources array
            var resultObj = JsonDocument.Parse(resultJson).RootElement;
            Assert.True(resultObj.TryGetProperty("resources", out var resources));
            Assert.True(resources.ValueKind == JsonValueKind.Array);
        }

        [Fact]
        public async Task ExecuteTool_WithInvalidTool_ShouldReturnErrorDictionary()
        {
            // Arrange
            var request = new McpRequest
            {
                Id = "test-4",
                JsonRpc = "2.0",
                Method = "tools/execute",
                Params = JsonDocument.Parse("{\"tool\":\"non_existent_tool\",\"parameters\":{}}").RootElement
            };

            // Act
            var response = await _dispatcher.DispatchRequestAsync(request);

            // Assert
            Assert.Equal("test-4", response.Id);
            Assert.Equal("2.0", response.JsonRpc);
            
            // In the current implementation, the dispatcher returns:
            // - Either a Result as a Dictionary with error information
            // - Or an Error if the dispatcher itself fails before the tool execution
            // For invalid tools, we get a Result with an error key, not a null Result
            Assert.NotNull(response.Result);
            
            var resultJson = JsonSerializer.Serialize(response.Result);
            _output.WriteLine($"Error Response: {resultJson}");
            
            // The result should contain error information inside the result
            var resultObj = JsonDocument.Parse(resultJson).RootElement;
            
            // For an invalid tool, the response should contain isError=true
            bool hasIsErrorProperty = resultObj.TryGetProperty("isError", out var isErrorEl);
            Assert.True(hasIsErrorProperty, "Response should contain an 'isError' property");
            Assert.True(isErrorEl.GetBoolean(), "isError should be true");
            
            // Check if the content array contains information about the invalid tool
            Assert.True(resultObj.TryGetProperty("content", out var contentEl), 
                "Response should have a content array");
            Assert.True(contentEl.GetArrayLength() > 0, "Content array shouldn't be empty");
            
            // At least one content item should contain text with the error message
            bool foundErrorMessage = false;
            foreach (var item in contentEl.EnumerateArray())
            {
                if (item.TryGetProperty("text", out var textEl))
                {
                    string? text = textEl.GetString();
                    if (text != null && text.Contains("Unknown tool", StringComparison.OrdinalIgnoreCase))
                    {
                        foundErrorMessage = true;
                        break;
                    }
                }
            }
            
            Assert.True(foundErrorMessage, "Content should mention 'Unknown tool'");
        }

        [Fact]
        public async Task CreateDiagram_ShouldCreateFileAndReturnValidResponse()
        {
            // Arrange
            string diagramName = $"test-diagram-{Guid.NewGuid()}.drawio";
            var request = new McpRequest
            {
                Id = "test-5",
                JsonRpc = "2.0",
                Method = "tools/execute",
                Params = JsonDocument.Parse($"{{\"tool\":\"create_new_diagram\",\"parameters\":{{\"name\":\"{diagramName}\"}}}}").RootElement
            };

            // Act
            var response = await _dispatcher.DispatchRequestAsync(request);

            // Assert
            Assert.Equal("test-5", response.Id);
            Assert.Equal("2.0", response.JsonRpc);
            Assert.NotNull(response.Result);
            Assert.Null(response.Error);

            var resultJson = JsonSerializer.Serialize(response.Result);
            _output.WriteLine($"Response: {resultJson}");

            // Verify file was created
            string filePath = Path.Combine(_tempDiagramsDir, diagramName);
            Assert.True(File.Exists(filePath), $"Diagram file was not created at: {filePath}");
        }

        [Fact]
        public async Task InvalidMethod_ShouldReturnMethodNotFoundError()
        {
            // Arrange
            var request = new McpRequest
            {
                Id = "test-6",
                JsonRpc = "2.0",
                Method = "invalid/method",
                Params = JsonDocument.Parse("{}").RootElement
            };

            // Act
            var response = await _dispatcher.DispatchRequestAsync(request);

            // Assert
            Assert.Equal("test-6", response.Id);
            Assert.Equal("2.0", response.JsonRpc);
            Assert.Null(response.Result);
            Assert.NotNull(response.Error);
            Assert.Equal(-32601, response.Error.Code); // Method not found error code
        }

        [Fact]
        public async Task ValidateMcpResponseStructure()
        {
            // This test ensures that our MCP responses have the right structure for the protocol.
            
            // Arrange
            var request = new McpRequest
            {
                Id = "test-validate",
                JsonRpc = "2.0",
                Method = "mcp/initialize",
                Params = JsonDocument.Parse("{\"clientIdentifier\":\"test-client\"}").RootElement
            };

            // Act
            var response = await _dispatcher.DispatchRequestAsync(request);

            // Assert
            Assert.Equal("test-validate", response.Id);
            Assert.Equal("2.0", response.JsonRpc);
            Assert.NotNull(response.Result);
            Assert.Null(response.Error);

            var resultJson = JsonSerializer.Serialize(response.Result);
            _output.WriteLine($"Response: {resultJson}");

            // Basic validation of response structure
            var resultObj = JsonDocument.Parse(resultJson).RootElement;
            
            // MCP responses should have these top-level properties
            Assert.True(resultObj.TryGetProperty("protocolVersion", out var versionProp));
            Assert.Equal(JsonValueKind.String, versionProp.ValueKind);
            
            Assert.True(resultObj.TryGetProperty("capabilities", out var capabilitiesProp));
            Assert.Equal(JsonValueKind.Object, capabilitiesProp.ValueKind);
            
            Assert.True(resultObj.TryGetProperty("serverInfo", out var serverInfoProp));
            Assert.Equal(JsonValueKind.Object, serverInfoProp.ValueKind);
            
            // Capabilities should include tools
            Assert.True(capabilitiesProp.TryGetProperty("tools", out var toolsProp));
            Assert.Equal(JsonValueKind.Object, toolsProp.ValueKind);
        }
        
        [Fact]
        public async Task GroupShapes_ShouldCreateGroupAndReturnGroupId()
        {
            // Arrange - Create a diagram and two shapes to group
            string diagramName = $"group-test-{Guid.NewGuid()}.drawio";
            
            // Create diagram
            var createRequest = new McpRequest
            {
                Id = "create-diagram",
                JsonRpc = "2.0",
                Method = "tools/execute",
                Params = JsonDocument.Parse($"{{\"tool\":\"create_new_diagram\",\"parameters\":{{\"name\":\"{diagramName}\"}}}}").RootElement
            };
            
            await _dispatcher.DispatchRequestAsync(createRequest);
            
            // Add first shape
            var addShape1Request = new McpRequest
            {
                Id = "add-shape-1",
                JsonRpc = "2.0",
                Method = "tools/execute",
                Params = JsonDocument.Parse($"{{\"tool\":\"add_shape\",\"parameters\":{{\"diagram\":\"{diagramName}\",\"value\":\"Shape 1\",\"x\":100,\"y\":100,\"width\":120,\"height\":60,\"shape\":\"rectangle\"}}}}").RootElement
            };
            
            var shape1Response = await _dispatcher.DispatchRequestAsync(addShape1Request);
            var shape1Json = JsonSerializer.Serialize(shape1Response.Result);
            var shape1Id = JsonDocument.Parse(shape1Json).RootElement.GetProperty("elementId").GetString();
            
            // Add second shape
            var addShape2Request = new McpRequest
            {
                Id = "add-shape-2",
                JsonRpc = "2.0",
                Method = "tools/execute",
                Params = JsonDocument.Parse($"{{\"tool\":\"add_shape\",\"parameters\":{{\"diagram\":\"{diagramName}\",\"value\":\"Shape 2\",\"x\":250,\"y\":100,\"width\":120,\"height\":60,\"shape\":\"rectangle\"}}}}").RootElement
            };
            
            var shape2Response = await _dispatcher.DispatchRequestAsync(addShape2Request);
            var shape2Json = JsonSerializer.Serialize(shape2Response.Result);
            var shape2Id = JsonDocument.Parse(shape2Json).RootElement.GetProperty("elementId").GetString();
            
            // Act - Group the shapes
            var groupRequest = new McpRequest
            {
                Id = "group-shapes",
                JsonRpc = "2.0",
                Method = "tools/execute",
                Params = JsonDocument.Parse($"{{\"tool\":\"group_shapes\",\"parameters\":{{\"diagram\":\"{diagramName}\",\"shape_ids\":[\"{shape1Id}\",\"{shape2Id}\"]}}}}").RootElement
            };
            
            var groupResponse = await _dispatcher.DispatchRequestAsync(groupRequest);
            
            // Assert
            Assert.Equal("group-shapes", groupResponse.Id);
            Assert.Equal("2.0", groupResponse.JsonRpc);
            Assert.NotNull(groupResponse.Result);
            Assert.Null(groupResponse.Error);
            
            var resultJson = JsonSerializer.Serialize(groupResponse.Result);
            _output.WriteLine($"Group Response: {resultJson}");
            
            // Verify the response contains a groupId
            var resultObj = JsonDocument.Parse(resultJson).RootElement;
            Assert.True(resultObj.TryGetProperty("groupId", out var groupIdProp));
            Assert.Equal(JsonValueKind.String, groupIdProp.ValueKind);
            
            // Verify the status
            Assert.True(resultObj.TryGetProperty("status", out var statusProp));
            Assert.Equal("success", statusProp.GetString());
            
            // Save the group ID for the next test
            string groupId = groupIdProp.GetString()!;
            Assert.False(string.IsNullOrEmpty(groupId));
        }
        
        [Fact]
        public async Task UngroupShapes_ShouldSuccessfullyUngroupElements()
        {
            // Arrange - Create a diagram, add shapes, and group them
            string diagramName = $"ungroup-test-{Guid.NewGuid()}.drawio";
            
            // Create diagram
            var createRequest = new McpRequest
            {
                Id = "create-diagram",
                JsonRpc = "2.0",
                Method = "tools/execute",
                Params = JsonDocument.Parse($"{{\"tool\":\"create_new_diagram\",\"parameters\":{{\"name\":\"{diagramName}\"}}}}").RootElement
            };
            
            await _dispatcher.DispatchRequestAsync(createRequest);
            
            // Add first shape
            var addShape1Request = new McpRequest
            {
                Id = "add-shape-1",
                JsonRpc = "2.0",
                Method = "tools/execute",
                Params = JsonDocument.Parse($"{{\"tool\":\"add_shape\",\"parameters\":{{\"diagram\":\"{diagramName}\",\"value\":\"Shape 1\",\"x\":100,\"y\":100,\"width\":120,\"height\":60,\"shape\":\"rectangle\"}}}}").RootElement
            };
            
            var shape1Response = await _dispatcher.DispatchRequestAsync(addShape1Request);
            var shape1Json = JsonSerializer.Serialize(shape1Response.Result);
            var shape1Id = JsonDocument.Parse(shape1Json).RootElement.GetProperty("elementId").GetString();
            
            // Add second shape
            var addShape2Request = new McpRequest
            {
                Id = "add-shape-2",
                JsonRpc = "2.0",
                Method = "tools/execute",
                Params = JsonDocument.Parse($"{{\"tool\":\"add_shape\",\"parameters\":{{\"diagram\":\"{diagramName}\",\"value\":\"Shape 2\",\"x\":250,\"y\":100,\"width\":120,\"height\":60,\"shape\":\"rectangle\"}}}}").RootElement
            };
            
            var shape2Response = await _dispatcher.DispatchRequestAsync(addShape2Request);
            var shape2Json = JsonSerializer.Serialize(shape2Response.Result);
            var shape2Id = JsonDocument.Parse(shape2Json).RootElement.GetProperty("elementId").GetString();
            
            // Group the shapes
            var groupRequest = new McpRequest
            {
                Id = "group-shapes",
                JsonRpc = "2.0",
                Method = "tools/execute",
                Params = JsonDocument.Parse($"{{\"tool\":\"group_shapes\",\"parameters\":{{\"diagram\":\"{diagramName}\",\"shape_ids\":[\"{shape1Id}\",\"{shape2Id}\"]}}}}").RootElement
            };
            
            var groupResponse = await _dispatcher.DispatchRequestAsync(groupRequest);
            var groupJson = JsonSerializer.Serialize(groupResponse.Result);
            var groupId = JsonDocument.Parse(groupJson).RootElement.GetProperty("groupId").GetString();
            
            // Act - Ungroup the shapes
            var ungroupRequest = new McpRequest
            {
                Id = "ungroup-shapes",
                JsonRpc = "2.0",
                Method = "tools/execute",
                Params = JsonDocument.Parse($"{{\"tool\":\"ungroup_shapes\",\"parameters\":{{\"diagram\":\"{diagramName}\",\"group_id\":\"{groupId}\"}}}}").RootElement
            };
            
            var ungroupResponse = await _dispatcher.DispatchRequestAsync(ungroupRequest);
            
            // Assert
            Assert.Equal("ungroup-shapes", ungroupResponse.Id);
            Assert.Equal("2.0", ungroupResponse.JsonRpc);
            Assert.NotNull(ungroupResponse.Result);
            Assert.Null(ungroupResponse.Error);
            
            var resultJson = JsonSerializer.Serialize(ungroupResponse.Result);
            _output.WriteLine($"Ungroup Response: {resultJson}");
            
            // Verify the status
            var resultObj = JsonDocument.Parse(resultJson).RootElement;
            Assert.True(resultObj.TryGetProperty("status", out var statusProp));
            Assert.Equal("success", statusProp.GetString());
            
            // Verify content array is present
            Assert.True(resultObj.TryGetProperty("content", out var contentProp));
            Assert.Equal(JsonValueKind.Array, contentProp.ValueKind);
        }
    }
} 