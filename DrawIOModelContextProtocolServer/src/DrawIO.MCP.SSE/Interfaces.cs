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
        public abstract string Name { get; }
        public abstract string Description { get; }
        public abstract ToolParameter[] Parameters { get; }
        public abstract Task<object> ExecuteAsync(ToolParameters parameters);
        
        // Helper method to add diagram image to response if requested
        protected async Task<object> AddDiagramImageToResponseAsync(
            object response, 
            ToolParameters parameters, 
            DrawIoService drawIoService)
        {
            // If return_diagram is true and diagram parameter exists, add image to response
            bool returnDiagram = parameters.GetValue<bool>("return_diagram");
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
                            if (existingContent is List<object> contentList)
                            {
                                // Add the image to existing content
                                contentList.Add(imageData);
                            }
                            else if (existingContent is object[] contentArray)
                            {
                                // Convert array to list, add image, then convert back
                                var newContent = new List<object>(contentArray) { imageData };
                                respDict["content"] = newContent;
                            }
                            else if (existingContent != null)
                            {
                                // Create new content with original and image
                                respDict["content"] = new List<object> { existingContent, imageData };
                            }
                            else
                            {
                                // Just set the image as content
                                respDict["content"] = new List<object> { imageData };
                            }
                        }
                        else
                        {
                            // Add new content with just the image
                            respDict["content"] = new List<object> { imageData };
                        }
                        
                        return respDict;
                    }
                    else 
                    {
                        // Convert response to dictionary
                        var responseDict = new Dictionary<string, object>();
                        
                        // Add all properties from original response
                        foreach (var prop in response.GetType().GetProperties())
                        {
                            var value = prop.GetValue(response);
                            if (value != null)
                            {
                                responseDict[prop.Name] = value;
                            }
                        }
                        
                        // Add content with the image
                        responseDict["content"] = new List<object> { imageData };
                        
                        return responseDict;
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