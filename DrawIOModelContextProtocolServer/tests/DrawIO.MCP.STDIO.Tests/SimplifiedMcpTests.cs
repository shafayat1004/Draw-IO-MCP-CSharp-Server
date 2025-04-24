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
            
            // For an invalid tool, the response should contain either an "error" property
            bool hasErrorProperty = resultObj.TryGetProperty("error", out var errorEl);
            Assert.True(hasErrorProperty, "Response should contain an 'error' property");
            
            // Check if the error message contains information about the invalid tool
            string errorMessage = errorEl.GetString() ?? string.Empty;
            Assert.Contains("Unknown tool", errorMessage, StringComparison.OrdinalIgnoreCase);
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
            // Arrange - create a valid request
            var request = new McpRequest
            {
                Id = "validation-test",
                JsonRpc = "2.0",
                Method = "tools/list",
                Params = JsonDocument.Parse("{}").RootElement
            };

            // Act
            var response = await _dispatcher.DispatchRequestAsync(request);
            string jsonResponse = JsonSerializer.Serialize(response);

            // Assert - validate JSON-RPC structure
            _output.WriteLine($"Full JSON response: {jsonResponse}");
            var responseDoc = JsonDocument.Parse(jsonResponse);
            var root = responseDoc.RootElement;

            // Check JSON-RPC 2.0 format compliance
            Assert.True(root.TryGetProperty("jsonrpc", out var jsonrpc));
            Assert.Equal("2.0", jsonrpc.GetString());
            
            Assert.True(root.TryGetProperty("id", out var id));
            Assert.Equal("validation-test", id.GetString());
            
            Assert.True(root.TryGetProperty("result", out _));
            Assert.False(root.TryGetProperty("error", out _), "Response should not contain error property for successful requests");
        }
    }
} 