using System.Text.Json;

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
        // Protected field for parameters that can be modified via reflection
        protected ToolParameter[]? _parameters;
        
        public abstract string Name { get; }
        public abstract string Description { get; }
        
        // Parameters property will initialize the field if null
        public virtual ToolParameter[] Parameters => _parameters ??= Array.Empty<ToolParameter>();
        
        public abstract Task<object> ExecuteAsync(ToolParameters parameters);
        
        // Helper method to add diagram image to response if requested
        protected async Task<object> AddDiagramImageToResponseAsync(
            object response, 
            ToolParameters parameters, 
            DrawIoService drawIoService)
        {
            // More robust handling of return_diagram parameter
            bool returnDiagram = false;
            
            try 
            {
                // First try to get as bool (properly typed parameter)
                returnDiagram = parameters.GetValue<bool>("return_diagram");
            }
            catch 
            {
                // If that fails, try to handle it as a string value
                try 
                {
                    string? returnDiagramStr = parameters.GetValue<string>("return_diagram");
                    if (!string.IsNullOrEmpty(returnDiagramStr))
                    {
                        returnDiagramStr = returnDiagramStr.ToLowerInvariant();
                        returnDiagram = (returnDiagramStr == "true");
                    }
                }
                catch 
                {
                    // Parameter doesn't exist or is not a valid type, leave as false
                    returnDiagram = false;
                }
            }
            
            string? diagramName = parameters.GetValue<string>("diagram");
            
            if (returnDiagram && !string.IsNullOrEmpty(diagramName))
            {
                // Get page index if specified
                int pageIndex = parameters.GetValue<int>("page_index", 
                              parameters.GetValue<int>("page", 0));
                
                // Get image data
                var imageData = await Task.FromResult(drawIoService.GetDiagramImageAsBase64(diagramName, pageIndex, "png"));
                
                if (imageData != null)
                {
                    // Create a copy of the response object with the image added
                    // Convert to dictionaries for manipulation
                    if (response is Dictionary<string, object> respDict)
                    {
                        // Check if there's already a content property
                        if (respDict.TryGetValue("content", out var existingContent))
                        {
                            // If content is an array, add the image to the array
                            if (existingContent is object[] contentArray)
                            {
                                var newContentList = contentArray.ToList();
                                newContentList.Add(imageData);
                                respDict["content"] = newContentList.ToArray();
                            }
                            else if (existingContent is List<object> contentList)
                            {
                                contentList.Add(imageData);
                            }
                            else
                            {
                                // If content is not an array, create a new array with both old content and image
                                respDict["content"] = new object[] { existingContent, imageData };
                            }
                        }
                        else
                        {
                            // If no content property exists, create one with the image
                            respDict["content"] = new object[] { imageData };
                        }
                        
                        // Also add the image as a separate property for backward compatibility
                        respDict["diagram"] = imageData;
                        
                        return respDict;
                    }
                    else
                    {
                        // If response is not a dictionary, create a new one with both the original response and image
                        return new Dictionary<string, object>
                        {
                            ["result"] = response,
                            ["diagram"] = imageData,
                            ["content"] = new object[] { imageData }
                        };
                    }
                }
            }
            
            return response;
        }
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

        public ToolParameters()
        {
        }
        
        public ToolParameters(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in element.EnumerateObject())
                {
                    // Convert JsonElement to appropriate CLR type
                    _values[property.Name] = property.Value.ToFriendlyObject() ?? string.Empty;
                }
            }
        }

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