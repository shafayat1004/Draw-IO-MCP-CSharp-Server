using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace DrawIO.MCP.SSE
{
    public static class McpExtensions
    {
        // Extension method to add MCP server support
        public static IApplicationBuilder UseMcp(this IApplicationBuilder app, Action<IMcpBuilder> configure)
        {
            var builder = new McpBuilder();
            configure(builder);
            
            // Get the WebApplication instance
            if (app is WebApplication webApp)
            {
                var serviceProvider = webApp.Services;
                
                // Map the tools endpoint
                webApp.MapPost("/mcp/tools/{toolName}", async (HttpContext context, string toolName) =>
                {
                    // Get all registered tool types from the container
                    var tools = builder.GetTools();
                    var toolInstances = tools.Select(t => serviceProvider.GetRequiredService(t) as Tool).ToList();
                    
                    // Find the tool with the matching name
                    var matchingTool = toolInstances.FirstOrDefault(t => t?.Name.Equals(toolName, StringComparison.OrdinalIgnoreCase) == true);
                    
                    if (matchingTool == null)
                    {
                        context.Response.StatusCode = 404;
                        await context.Response.WriteAsJsonAsync(new { status = "error", message = $"Tool not found: {toolName}" });
                        return;
                    }
                    
                    try
                    {
                        // Parse the request body to get parameters
                        var requestBody = await context.Request.ReadFromJsonAsync<JsonElement>();
                        var parameters = new ToolParameters(requestBody);
                        
                        // Execute the tool
                        var result = await matchingTool.ExecuteAsync(parameters);
                        
                        // Return the result
                        await context.Response.WriteAsJsonAsync(result);
                    }
                    catch (Exception ex)
                    {
                        context.Response.StatusCode = 500;
                        await context.Response.WriteAsJsonAsync(new { status = "error", message = ex.Message });
                    }
                });
                
                // Map resources endpoint if needed
                // (implementation omitted for brevity)
            }
            
            return app;
        }

        public static ToolParameter[] AddCommonParameters(this ToolParameter[] parameters)
        {
            // Check if this tool has a diagram parameter
            bool hasDiagramParameter = false;
            foreach (var param in parameters)
            {
                if (param.Name == "diagram")
                {
                    hasDiagramParameter = true;
                    break;
                }
            }

            // If it has a diagram parameter, add the return_diagram parameter
            if (hasDiagramParameter)
            {
                var allParameters = new List<ToolParameter>(parameters);
                
                // Add return_diagram parameter
                allParameters.Add(new ToolParameter
                {
                    Name = "return_diagram",
                    Type = "boolean",
                    Description = "Whether to include the diagram image in the response",
                    Required = false
                });
                
                return allParameters.ToArray();
            }
            
            return parameters;
        }

        // Fix to handle nulls properly
        public static object? ToFriendlyObject(this JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Object => element.EnumerateObject()
                    .ToDictionary(p => p.Name, p => p.Value.ToFriendlyObject()),
                JsonValueKind.Array => element.EnumerateArray()
                    .Select(e => e.ToFriendlyObject())
                    .ToArray(),
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.TryGetInt64(out var longVal) ? 
                    longVal : element.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => null
            };
        }
    }
    
    // Interface for MCP builder
    public interface IMcpBuilder
    {
        IMcpBuilder RegisterResourceProvider<T>() where T : ResourceProvider;
        IMcpBuilder RegisterTool<T>() where T : Tool;
        IReadOnlyList<Type> GetTools();
        IReadOnlyList<Type> GetResourceProviders();
    }
    
    // Implementation of MCP builder
    public class McpBuilder : IMcpBuilder
    {
        private readonly List<Type> _resourceProviders = new List<Type>();
        private readonly List<Type> _tools = new List<Type>();
        
        public IMcpBuilder RegisterResourceProvider<T>() where T : ResourceProvider
        {
            _resourceProviders.Add(typeof(T));
            return this;
        }
        
        public IMcpBuilder RegisterTool<T>() where T : Tool
        {
            _tools.Add(typeof(T));
            return this;
        }
        
        public IReadOnlyList<Type> GetTools() => _tools;
        
        public IReadOnlyList<Type> GetResourceProviders() => _resourceProviders;
    }
    
    // Extension method to add MCP services
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddMcpServer(this IServiceCollection services)
        {
            // Register all tool types
            var toolTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .Where(t => !t.IsAbstract && t.IsClass && t.IsSubclassOf(typeof(Tool)))
                .ToList();
                
            foreach (var toolType in toolTypes)
            {
                services.AddTransient(toolType);
            }
            
            return services;
        }
    }
} 