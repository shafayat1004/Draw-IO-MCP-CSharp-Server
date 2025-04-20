namespace DrawIO.MCP.SSE
{
    // Base interfaces for MCP implementation
    public abstract class ResourceProvider
    {
        public abstract bool CanProvide(string resourceId);
        public abstract Task<Resource> GetResourceAsync(string resourceId, ResourceQuery? query = null);
        public abstract Task<IEnumerable<ResourceInfo>> ListResourcesAsync(ResourceQuery? query = null);
    }

    public class Resource
    {
        public required string Id { get; set; }
        public required string Type { get; set; }
        public required object Content { get; set; }
    }

    public class ResourceInfo
    {
        public required string Id { get; set; }
        public required string Type { get; set; }
        public required string Title { get; set; }
    }

    public class ResourceQuery
    {
        // Any necessary properties for resource queries
    }

    public abstract class Tool
    {
        public abstract string Name { get; }
        public abstract string Description { get; }
        public abstract ToolParameter[] Parameters { get; }
        public abstract Task<object> ExecuteAsync(ToolParameters parameters);
    }

    public class ToolParameter
    {
        public required string Name { get; set; }
        public required string Type { get; set; }
        public required string Description { get; set; }
        public bool Required { get; set; }
    }

    public class ToolParameters
    {
        private readonly Dictionary<string, object> _values = new Dictionary<string, object>();

        public void SetValue(string name, object value)
        {
            _values[name] = value;
        }

        public T? GetValue<T>(string name)
        {
            if (_values.TryGetValue(name, out var value))
            {
                return (T)value;
            }
            
            return default;
        }

        public T GetValue<T>(string name, T defaultValue)
        {
            if (_values.TryGetValue(name, out var value))
            {
                return (T)value;
            }
            
            return defaultValue;
        }
    }
} 