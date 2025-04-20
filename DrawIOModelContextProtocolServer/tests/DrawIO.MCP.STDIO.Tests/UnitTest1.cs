using System.Text.Json;
using System.IO;

namespace DrawIO.MCP.STDIO.Tests
{
    public class McpRequestDispatcherTests
    {
        private readonly McpRequestDispatcher _dispatcher;
        private readonly StringWriter _testLogWriter;

        public McpRequestDispatcherTests()
        {
            // Create a temporary directory for testing
            string tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString());
            System.IO.Directory.CreateDirectory(tempDir);
            
            // Create a string writer for capturing logs during tests
            _testLogWriter = new StringWriter();
            
            _dispatcher = new McpRequestDispatcher(tempDir, false, _testLogWriter);
        }

        [Fact]
        public async Task ListTools_ReturnsValidToolsList()
        {
            // Arrange
            var request = new McpRequest
            {
                Id = "1",
                JsonRpc = "2.0",
                Method = "tools/list",
                Params = null
            };

            // Act
            var response = await _dispatcher.DispatchRequestAsync(request);

            // Assert
            Assert.NotNull(response);
            Assert.NotNull(response.Result);
            Assert.Null(response.Error);
            
            // Verify the response has Tools property
            var resultJson = JsonSerializer.Serialize(response.Result);
            var resultObj = JsonSerializer.Deserialize<JsonElement>(resultJson);
            
            Assert.True(resultObj.TryGetProperty("tools", out var toolsElement));
            Assert.Equal(JsonValueKind.Array, toolsElement.ValueKind);
            
            // Verify some known tools are in the list
            var hasCreateTool = false;
            var hasAddShapeTool = false;
            
            foreach (var tool in toolsElement.EnumerateArray())
            {
                if (tool.TryGetProperty("name", out var nameElement))
                {
                    string? name = nameElement.GetString();
                    if (name == "create_new_diagram") hasCreateTool = true;
                    if (name == "add_shape") hasAddShapeTool = true;
                }
            }
            
            Assert.True(hasCreateTool, "Tool list should include 'create_new_diagram'");
            Assert.True(hasAddShapeTool, "Tool list should include 'add_shape'");
        }

        [Fact]
        public async Task InvalidMethod_ReturnsError()
        {
            // Arrange
            var paramsJson = "{}";
            JsonElement paramsElement;
            using (JsonDocument doc = JsonDocument.Parse(paramsJson))
            {
                paramsElement = doc.RootElement.Clone();
            }
            var request = new McpRequest
            {
                Id = "1",
                JsonRpc = "2.0",
                Method = "mcp/nonexistentMethod",
                Params = paramsElement
            };

            // Act
            var response = await _dispatcher.DispatchRequestAsync(request);

            // Assert
            Assert.NotNull(response);
            Assert.Null(response.Result);
            Assert.NotNull(response.Error);
            Assert.Equal(-32601, response.Error.Code);
            Assert.Contains("not found", response.Error.Message);
        }

        [Fact]
        public async Task ListResources_WithEmptyDirectory_ContainsOnlyDiagramListResource()
        {
            // Arrange
            JsonElement params1;
            using (JsonDocument doc = JsonDocument.Parse("{}"))
            {
                params1 = doc.RootElement.Clone();
            }
            
            var request = new McpRequest
            {
                Id = "1",
                JsonRpc = "2.0",
                Method = "resources/list",
                Params = params1
            };

            // Act
            var response = await _dispatcher.DispatchRequestAsync(request);

            // Assert
            Assert.NotNull(response);
            Assert.NotNull(response.Result);
            Assert.Null(response.Error);
            
            // Verify the response has Resources property
            var resultJson = JsonSerializer.Serialize(response.Result);
            var resultObj = JsonSerializer.Deserialize<JsonElement>(resultJson);
            
            Assert.True(resultObj.TryGetProperty("resources", out var resourcesElement));
            Assert.Equal(JsonValueKind.Array, resourcesElement.ValueKind);
            
            // Empty directory should have only the diagram-list resource
            Assert.Equal(1, resourcesElement.GetArrayLength());
            
            // Verify the default diagram-list resource is included
            var resource = resourcesElement[0];
            Assert.True(resource.TryGetProperty("uri", out var uriElement));
            Assert.Equal("diagram-list://all", uriElement.GetString());
        }
    }
}