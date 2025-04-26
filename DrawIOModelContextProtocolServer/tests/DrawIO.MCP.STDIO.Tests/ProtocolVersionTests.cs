using System.Text.Json;

namespace DrawIO.MCP.STDIO.Tests
{
    public class ProtocolVersionTests
    {
        private readonly McpRequestDispatcher _dispatcher;
        private readonly StringWriter _testLogWriter;

        public ProtocolVersionTests()
        {
            // Create a temporary directory for testing
            string tempDir = Path.Combine(Path.GetTempPath(), "drawio-mcp-test-protocols");
            if (!Directory.Exists(tempDir))
            {
                Directory.CreateDirectory(tempDir);
            }
            
            // Create a string writer for capturing logs during tests
            _testLogWriter = new StringWriter();
            
            _dispatcher = new McpRequestDispatcher(tempDir, true, _testLogWriter);
        }

        [Fact]
        public async Task Initialize_WithProtocolVersion_2024_11_05_ShouldAccept()
        {
            // Arrange
            string paramsJson = @"{""protocolVersion"": ""2024-11-05""}";
            JsonElement paramsElement;
            using (JsonDocument doc = JsonDocument.Parse(paramsJson))
            {
                paramsElement = doc.RootElement.Clone();
            }

            var request = new McpRequest
            {
                Id = "test-2024",
                JsonRpc = "2.0",
                Method = "initialize",
                Params = paramsElement
            };

            // Act
            var response = await _dispatcher.DispatchRequestAsync(request);

            // Assert
            Assert.NotNull(response);
            Assert.NotNull(response.Result);
            Assert.Null(response.Error);
            
            var resultJson = JsonSerializer.Serialize(response.Result);
            var resultObj = JsonSerializer.Deserialize<JsonElement>(resultJson);
            
            Assert.True(resultObj.TryGetProperty("protocolVersion", out var versionElement));
            Assert.Equal("2024-11-05", versionElement.GetString());
        }

        [Fact]
        public async Task Initialize_WithProtocolVersion_2025_03_26_ShouldAccept()
        {
            // Arrange
            string paramsJson = @"{""protocolVersion"": ""2025-03-26""}";
            JsonElement paramsElement;
            using (JsonDocument doc = JsonDocument.Parse(paramsJson))
            {
                paramsElement = doc.RootElement.Clone();
            }

            var request = new McpRequest
            {
                Id = "test-2025",
                JsonRpc = "2.0",
                Method = "initialize",
                Params = paramsElement
            };

            // Act
            var response = await _dispatcher.DispatchRequestAsync(request);

            // Assert
            Assert.NotNull(response);
            Assert.NotNull(response.Result);
            Assert.Null(response.Error);
            
            var resultJson = JsonSerializer.Serialize(response.Result);
            var resultObj = JsonSerializer.Deserialize<JsonElement>(resultJson);
            
            Assert.True(resultObj.TryGetProperty("protocolVersion", out var versionElement));
            Assert.Equal("2025-03-26", versionElement.GetString());
        }

        [Fact]
        public async Task Initialize_WithNoProtocolVersion_ShouldDefaultTo_2025_03_26()
        {
            // Arrange
            string paramsJson = @"{}";
            JsonElement paramsElement;
            using (JsonDocument doc = JsonDocument.Parse(paramsJson))
            {
                paramsElement = doc.RootElement.Clone();
            }

            var request = new McpRequest
            {
                Id = "test-default",
                JsonRpc = "2.0",
                Method = "initialize",
                Params = paramsElement
            };

            // Act
            var response = await _dispatcher.DispatchRequestAsync(request);

            // Assert
            Assert.NotNull(response);
            Assert.NotNull(response.Result);
            Assert.Null(response.Error);
            
            var resultJson = JsonSerializer.Serialize(response.Result);
            var resultObj = JsonSerializer.Deserialize<JsonElement>(resultJson);
            
            Assert.True(resultObj.TryGetProperty("protocolVersion", out var versionElement));
            Assert.Equal("2025-03-26", versionElement.GetString());
        }

        [Fact]
        public async Task Initialize_WithUnsupportedProtocolVersion_ShouldReturnError()
        {
            // Arrange
            string requestedVersion = "2023-01-01";
            string paramsJson = $"{{\"protocolVersion\": \"{requestedVersion}\"}}";
            JsonElement paramsElement;
            using (JsonDocument doc = JsonDocument.Parse(paramsJson))
            {
                paramsElement = doc.RootElement.Clone();
            }

            var request = new McpRequest
            {
                Id = "test-unsupported",
                JsonRpc = "2.0",
                Method = "initialize",
                Params = paramsElement
            };

            // Act
            var response = await _dispatcher.DispatchRequestAsync(request);

            // Assert
            Assert.NotNull(response);
            Assert.Null(response.Result);
            Assert.NotNull(response.Error);

            Assert.Equal(-32602, response.Error.Code);
            Assert.Equal("Unsupported protocol version", response.Error.Message);
            Assert.NotNull(response.Error.Details);

            var errorDetailsJson = JsonSerializer.Serialize(response.Error.Details);
            var errorDetailsObj = JsonSerializer.Deserialize<JsonElement>(errorDetailsJson);

            Assert.True(errorDetailsObj.TryGetProperty("requested", out var reqVerElement));
            Assert.Equal(requestedVersion, reqVerElement.GetString());

            Assert.True(errorDetailsObj.TryGetProperty("supported", out var supVerElement));
            Assert.Equal(JsonValueKind.Array, supVerElement.ValueKind);
            var supportedVersions = supVerElement.EnumerateArray().Select(e => e.GetString()).ToList();
            Assert.Contains("2025-03-26", supportedVersions);
            Assert.Contains("2024-11-05", supportedVersions);

            string logs = _testLogWriter.ToString();
            Assert.Contains($"Client requested unsupported version {requestedVersion}", logs);
        }
    }
} 