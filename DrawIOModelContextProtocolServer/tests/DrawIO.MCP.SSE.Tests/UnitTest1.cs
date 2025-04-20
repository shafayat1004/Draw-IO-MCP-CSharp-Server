using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;

namespace DrawIO.MCP.SSE.Tests
{
    public class CustomWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string _tempDiagramsDirectory;

        public CustomWebApplicationFactory()
        {
            _tempDiagramsDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDiagramsDirectory);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                // Use temp diagrams directory
                Environment.SetEnvironmentVariable("DIAGRAMS_DIR", _tempDiagramsDirectory);
            });
        }

        protected override IHost CreateHost(IHostBuilder builder)
        {
            // Need to create a temporary HTTP server
            builder.ConfigureWebHost(webHostBuilder => webHostBuilder.UseUrls("http://localhost:0"));
            return base.CreateHost(builder);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                try
                {
                    // Clean up the temporary directory
                    if (Directory.Exists(_tempDiagramsDirectory))
                    {
                        Directory.Delete(_tempDiagramsDirectory, true);
                    }
                }
                catch (Exception)
                {
                    // Ignore errors on cleanup
                }
            }
        }
    }

    public class ServerIntegrationTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly CustomWebApplicationFactory _factory;
        private readonly HttpClient _client;

        public ServerIntegrationTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
            _client = _factory.CreateClient();
        }

        [Fact]
        public async Task HealthEndpoint_ReturnsOk()
        {
            // Act
            var response = await _client.GetAsync("/health");

            // Assert
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            Assert.Equal("OK", content);
        }

        [Fact]
        public async Task RootEndpoint_ReturnsWelcomeMessage()
        {
            // Act
            var response = await _client.GetAsync("/");

            // Assert
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            Assert.Contains("DrawIO MCP Server", content);
        }
    }
}