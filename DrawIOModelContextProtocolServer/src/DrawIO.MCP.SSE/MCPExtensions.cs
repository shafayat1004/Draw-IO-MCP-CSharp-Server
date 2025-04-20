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