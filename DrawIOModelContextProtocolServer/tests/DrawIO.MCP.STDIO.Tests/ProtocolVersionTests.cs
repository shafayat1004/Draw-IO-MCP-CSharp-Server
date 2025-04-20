using System.Text.Json;
using System.IO;
using System.Threading.Tasks;
using Xunit;

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
        public async Task Initialize_WithNoProtocolVersion_ShouldDefaultTo_2024_11_05()
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
            Assert.Equal("2024-11-05", versionElement.GetString());
        }

        [Fact]
        public async Task Initialize_WithUnsupportedProtocolVersion_ShouldFallbackTo_2024_11_05()
        {
            // Arrange
            string paramsJson = @"{""protocolVersion"": ""2023-01-01""}";
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
            Assert.NotNull(response.Result);
            Assert.Null(response.Error);
            
            var resultJson = JsonSerializer.Serialize(response.Result);
            var resultObj = JsonSerializer.Deserialize<JsonElement>(resultJson);
            
            Assert.True(resultObj.TryGetProperty("protocolVersion", out var versionElement));
            Assert.Equal("2024-11-05", versionElement.GetString());
            
            // Check logs to verify fallback message was logged
            string logs = _testLogWriter.ToString();
            Assert.Contains("Client requested unsupported version 2023-01-01", logs);
        }
    }
} 