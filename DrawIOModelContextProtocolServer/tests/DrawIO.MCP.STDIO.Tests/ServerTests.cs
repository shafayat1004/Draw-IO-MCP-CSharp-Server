using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace DrawIO.MCP.STDIO.Tests
{
    public class ServerTests
    {
        private readonly ITestOutputHelper _output;

        public ServerTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task Initialize_Should_ReturnSuccess()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            try
            {
                var logWriter = new StringWriter();
                var dispatcher = new McpRequestDispatcher(tempDir, true, logWriter);

                // Act
                var request = new McpRequest
                {
                    Id = "1",
                    JsonRpc = "2.0",
                    Method = "mcp/initialize",
                    Params = System.Text.Json.JsonDocument.Parse("{\"clientIdentifier\":\"test\"}").RootElement
                };

                var response = await dispatcher.DispatchRequestAsync(request);

                // Assert
                Assert.Equal("1", response.Id);
                Assert.Equal("2.0", response.JsonRpc);
                Assert.NotNull(response.Result);
                
                _output.WriteLine($"Response: {response.Result}");
            }
            finally
            {
                // Clean up
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }
    }
} 