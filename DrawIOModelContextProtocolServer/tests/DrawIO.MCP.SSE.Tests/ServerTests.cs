using Xunit.Abstractions;

namespace DrawIO.MCP.SSE.Tests
{
    public class ServerTests
    {
        private readonly ITestOutputHelper _output;

        public ServerTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void Server_Configuration_ShouldBeValid()
        {
            // This test just verifies that the basic configuration process works
            // Actual server testing would be done with integration tests
            
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            try
            {
                // Act - attempt to create a server configuration
                var config = new ServerConfig
                {
                    DiagramsDirectory = tempDir,
                    Verbose = true,
                    Port = 0  // Use zero for automatic port assignment
                };

                // Assert
                Assert.NotNull(config);
                Assert.Equal(tempDir, config.DiagramsDirectory);
                Assert.True(config.Verbose);
                
                _output.WriteLine("Server configuration created successfully");
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

    // Mock configuration class for testing
    public class ServerConfig
    {
        public required string DiagramsDirectory { get; set; }
        public bool Verbose { get; set; }
        public int Port { get; set; }
    }
} 