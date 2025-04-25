using System.Text.Json;

namespace DrawIO.MCP.SSE
{
    public static class McpExtensions
    {
        // Extension method to add MCP server support
        public static IApplicationBuilder UseMcp(this IApplicationBuilder app, Action<IMcpBuilder> configure)
        {
            var builder = new McpBuilder();
            configure(builder);
            
            // Register the configured resources and tools
            // This would normally set up the SSE connections and endpoints
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
    }
    
    // Extension method to add MCP services
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddMcpServer(this IServiceCollection services)
        {
            // Register MCP-related services
            return services;
        }
    }
} 