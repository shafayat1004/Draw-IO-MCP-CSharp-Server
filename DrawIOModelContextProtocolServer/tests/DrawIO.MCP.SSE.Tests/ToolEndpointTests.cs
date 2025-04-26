using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace DrawIO.MCP.SSE.Tests
{
    public class ToolEndpointTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly HttpClient _client;

        public ToolEndpointTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task ListShapeTypes_ShouldReturnAvailableShapeCategories()
        {
            // Arrange & Act
            var response = await _client.PostAsJsonAsync("/mcp/tools/list_shape_types", new { });
            
            // Assert
            response.EnsureSuccessStatusCode();
            
            // Parse as generic JsonElement since we're dealing with System.Text.Json
            var content = await response.Content.ReadFromJsonAsync<JsonElement>();
            
            // Start assertions - content can't be null as JsonElement is a value type
            Assert.True(content.TryGetProperty("status", out var statusElement));
            Assert.Equal("success", statusElement.GetString());
            
            Assert.True(content.TryGetProperty("shapeCategories", out var shapeCategoriesElement));
            
            // Check each expected category
            Assert.True(shapeCategoriesElement.TryGetProperty("Basic", out _));
            Assert.True(shapeCategoriesElement.TryGetProperty("Flowchart", out _));
            Assert.True(shapeCategoriesElement.TryGetProperty("UML", out _));
            Assert.True(shapeCategoriesElement.TryGetProperty("Network", out _));
            Assert.True(shapeCategoriesElement.TryGetProperty("Containers", out _));
        }
    }
} 