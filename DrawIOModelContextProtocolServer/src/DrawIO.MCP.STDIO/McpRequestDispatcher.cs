using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.FSharp.Core;
using static DrawIO.MCP.STDIO.FileOperations;

namespace DrawIO.MCP.STDIO
{
    /// <summary>
    /// Handles dispatching MCP requests to the appropriate handler method
    /// </summary>
    public class McpRequestDispatcher
    {
        private readonly string _diagramsDirectory;
        private readonly bool _verbose;
        private readonly TextWriter _logWriter;
        private readonly Dictionary<string, Func<JsonElement, Task<object>>> _methodHandlers;

        public McpRequestDispatcher(string diagramsDirectory, bool verbose, TextWriter logWriter)
        {
            _diagramsDirectory = diagramsDirectory;
            _verbose = verbose;
            _logWriter = logWriter;
            
            // Initialize method handlers
            _methodHandlers = new Dictionary<string, Func<JsonElement, Task<object>>>
            {
                { "initialize", InitializeAsync },
                { "mcp/initialize", InitializeAsync },
                { "notifications/initialized", NotificationsInitializedAsync },
                { "tools/list", ListToolsAsync },
                { "tools/call", ExecuteToolAsync },
                { "tools/execute", ExecuteToolAsync },
                { "resources/list", ListResourcesAsync },
                { "resources/read", ReadResourceAsync },
                { "prompts/list", ListPromptsAsync },
                { "mcp/shutdown", ShutdownAsync }
            };
        }

        public async Task<McpResponse> DispatchRequestAsync(McpRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            LogInfo($"Processing request ID: {request.Id}, Method: {request.Method}");

            if (string.IsNullOrEmpty(request.Method))
            {
                return McpResponse.CreateError(request.Id, -32601, "Method is required");
            }

            if (!_methodHandlers.TryGetValue(request.Method, out var handler))
            {
                LogInfo($"Method not found: {request.Method}");
                return McpResponse.CreateError(request.Id, -32601, $"Method '{request.Method}' not found");
            }

            try
            {
                var result = await handler(request.Params ?? new JsonElement());
                return McpResponse.CreateResult(request.Id, result);
            }
            catch (Exception ex)
            {
                LogInfo($"Error handling request {request.Method}: {ex.Message}");

                // Default error code
                int errorCode = -32603; // Internal error
                string errorMessage = ex.Message;
                object errorDetails = null;

                // Check for specific MCP version mismatch error
                if (ex.Message == "Unsupported protocol version" && ex.Data.Contains("details"))
                {
                    errorCode = -32602; // Invalid params - specific code for version mismatch
                    errorMessage = "Unsupported protocol version"; // Use the standard message
                    errorDetails = ex.Data["details"];
                }
                // Extract details if present for other errors too
                else if (ex.Data.Contains("details"))
                {
                    errorDetails = ex.Data["details"];
                }

                return McpResponse.CreateError(request.Id, errorCode, errorMessage, errorDetails);
            }
        }

        private McpResponse CreateErrorResponse(string id, int errorCode, string errorMessage)
        {
            return McpResponse.CreateError(id, errorCode, errorMessage);
        }

        private void LogError(string message)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            _logWriter.WriteLine($"[{timestamp}] ERROR: {message}");
        }

        private void LogInfo(string message)
        {
            if (_verbose)
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                _logWriter.WriteLine($"[{timestamp}] INFO: {message}");
            }
        }

        // MCP Method Handlers

        private Task<object> InitializeAsync(JsonElement parameters)
        {
            this.LogInfo("Initializing MCP server");

            // Default to the most recent supported version if client doesn't specify
            string protocolVersion = "2025-03-26";

            // List of protocol versions we support (most recent first)
            var supportedVersions = new[] { "2025-03-26", "2024-11-05" };

            if (parameters.TryGetProperty("protocolVersion", out var versionElement))
            {
                string requestedVersion = versionElement.GetString();
                this.LogInfo($"Client requested protocol version: {requestedVersion}");

                // If the requested version is one we support, use it
                if (Array.IndexOf(supportedVersions, requestedVersion) >= 0)
                {
                    protocolVersion = requestedVersion;
                    this.LogInfo($"Using client's requested protocol version: {protocolVersion}");
                }
                else
                {
                    // Client requested an unsupported version - return specific error
                    this.LogInfo($"Client requested unsupported version {requestedVersion}. Server supports: [{string.Join(", ", supportedVersions)}]");
                    var errorDetails = new Dictionary<string, object>
                    {
                        { "supported", supportedVersions },
                        { "requested", requestedVersion }
                    };
                    var errorException = new Exception("Unsupported protocol version");
                    errorException.Data["details"] = errorDetails;
                    throw errorException; // This will be caught by DispatchRequestAsync
                }
            }
            else
            {
                 this.LogInfo($"Client did not specify protocolVersion. Defaulting to {protocolVersion}");
            }

            // Return the exact format expected by VS Code
            return Task.FromResult<object>(new Dictionary<string, object>
            {
                ["protocolVersion"] = protocolVersion,
                ["capabilities"] = new Dictionary<string, object>
                {
                    ["experimental"] = new Dictionary<string, object>(),
                    ["tools"] = new Dictionary<string, object>
                    {
                        ["listChanged"] = true
                    }
                },
                ["serverInfo"] = new Dictionary<string, object>
                {
                    ["name"] = "DrawIO MCP Server",
                    ["version"] = "1.0.0"
                }
            });
        }

        private Task<object> NotificationsInitializedAsync(JsonElement parameters)
        {
            this.LogInfo("Received initialized notification");
            // Return an empty object instead of null to satisfy JSON-RPC spec
            return Task.FromResult<object>(new { });
        }

        private Task<object> ListPromptsAsync(JsonElement parameters)
        {
            this.LogInfo("Listing prompts");
            return Task.FromResult<object>(new
            {
                prompts = new object[] { }
            });
        }
        
        private async Task<object> ReadResourceAsync(JsonElement parameters)
        {
            if (!parameters.TryGetProperty("uri", out var uriElement))
            {
                throw new ArgumentException("Resource URI is required");
            }
            
            string uri = uriElement.GetString();
            this.LogInfo($"Reading resource: {uri}");
            
            if (uri.StartsWith("diagram-list://"))
            {
                // Handle diagram listing
                var diagrams = ListDiagrams();
                return new
                {
                    content = diagrams,
                    mimeType = "application/json"
                };
            }
            else if (uri.StartsWith("diagram://"))
            {
                // Handle diagram content
                string diagramName = uri.Substring("diagram://".Length);
                string filePath = Path.Combine(_diagramsDirectory, diagramName);
                
                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException($"Diagram file not found: {diagramName}");
                }
                
                string content = await File.ReadAllTextAsync(filePath);
                return new
                {
                    content = content,
                    mimeType = "application/xml"
                };
            }
            
            throw new ArgumentException($"Unsupported resource URI: {uri}");
        }
        
        private object[] ListDiagrams()
        {
            if (!Directory.Exists(_diagramsDirectory))
            {
                this.LogInfo($"Diagrams directory does not exist: {_diagramsDirectory}");
                return Array.Empty<object>();
            }
            
            string[] files = Directory.GetFiles(_diagramsDirectory, "*.drawio");
            var diagrams = files.Select(f => new
            {
                name = Path.GetFileName(f),
                uri = $"diagram://{Path.GetFileName(f)}"
            }).ToArray();
            
            return diagrams;
        }

        private Task<object> ListResourcesAsync(JsonElement parameters)
        {
            this.LogInfo("Listing resources");
            
            var resources = new List<object>
            {
                new
                {
                    uri = "diagram-list://all",
                    name = "All Diagrams",
                    schema = "diagram-list://",
                    description = "List all available diagrams"
                }
            };
            
            // Add resources for each existing diagram file
            if (Directory.Exists(_diagramsDirectory))
            {
                string[] files = Directory.GetFiles(_diagramsDirectory, "*.drawio");
                foreach (var file in files)
                {
                    string fileName = Path.GetFileName(file);
                    resources.Add(new
                    {
                        uri = $"diagram://{fileName}",
                        name = fileName,
                        schema = "diagram://",
                        description = $"Access {fileName} content"
                    });
                }
            }
            
            return Task.FromResult<object>(new
            {
                resources = resources.ToArray()
            });
        }

        private Task<object> GetResourceAsync(JsonElement parameters)
        {
            if (!parameters.TryGetProperty("id", out var idElement))
            {
                throw new ArgumentException("Resource ID is required");
            }
            
            string resourceId = idElement.GetString();
            this.LogInfo($"Getting resource: {resourceId}");
            
            if (resourceId.StartsWith("diagram://"))
            {
                string filename = resourceId.Substring("diagram://".Length);
                string filePath = Path.Combine(_diagramsDirectory, filename);
                
                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException($"Diagram file not found: {filename}");
                }
                
                // Load and parse the diagram using the Core library
                var diagram = LoadDiagram(filePath);
                
                // Convert the diagram to a simple object structure for JSON serialization
                var pages = new List<object>();
                
                foreach (var page in diagram.Pages)
                {
                    var cells = new List<object>();
                    
                    foreach (var cell in page.Cells)
                    {
                        var cellObj = new Dictionary<string, object>
                        {
                            { "id", cell.Id },
                            { "value", cell.Value },
                            { "style", cell.Style },
                            { "isVertex", cell.IsVertex },
                            { "isEdge", cell.IsEdge },
                            { "parent", cell.Parent }
                        };
                        
                        if (cell.Source.IsSome())
                        {
                            cellObj.Add("source", cell.Source.Value);
                        }
                        
                        if (cell.Target.IsSome())
                        {
                            cellObj.Add("target", cell.Target.Value);
                        }
                        
                        if (CoreTypes.HasGeometry(cell.Geometry))
                        {
                            var geo = cell.Geometry.Value;
                            cellObj.Add("geometry", new
                            {
                                x = geo.Position.X,
                                y = geo.Position.Y,
                                width = geo.Size.Width,
                                height = geo.Size.Height,
                                relative = geo.Relative
                            });
                        }
                        
                        cells.Add(cellObj);
                    }
                    
                    pages.Add(new
                    {
                        id = page.Id,
                        name = page.Name,
                        cells = cells
                    });
                }
                
                return Task.FromResult<object>(new
                {
                    type = "diagram",
                    id = resourceId,
                    content = new
                    {
                        modified = diagram.Modified,
                        pages = pages
                    }
                });
            }
            else if (resourceId == "diagram-list://all")
            {
                // Return a list of all diagrams
                var files = Directory.GetFiles(_diagramsDirectory, "*.drawio")
                    .Select(f => Path.GetFileName(f))
                    .ToList();
                
                return Task.FromResult<object>(new
                {
                    type = "diagram-list",
                    id = resourceId,
                    content = files
                });
            }
            
            throw new ArgumentException($"Unsupported resource type: {resourceId}");
        }

        private Task<object> ListToolsAsync(JsonElement parameters)
        {
            return McpToolHandlers.ListToolsAsync(this, parameters, _logWriter, _verbose);
        }

        private async Task<object> ExecuteToolAsync(JsonElement parameters)
        {
            // Try with the expected format (tool, parameters)
            if (!parameters.TryGetProperty("tool", out var toolElement))
            {
                // Fallback to alternative format (name, arguments)
                if (!parameters.TryGetProperty("name", out toolElement))
                {
                    throw new ArgumentException("Tool name is required");
                }
            }
            
            string toolName = toolElement.GetString();
            this.LogInfo($"Executing tool: {toolName}");
            
            JsonElement arguments;
            
            // Try with the expected format (parameters)
            if (parameters.TryGetProperty("parameters", out var paramsElement))
            {
                arguments = paramsElement;
            }
            // Fallback to alternative format (arguments)
            else if (parameters.TryGetProperty("arguments", out var argsElement))
            {
                arguments = argsElement;
            }
            else
            {
                arguments = new JsonElement();
            }
            
            try
            {
                // Use the tool executor to execute the tool
                // DiagramToolExecutor now handles all tools, including query tools
                object result = await DiagramToolExecutor.ExecuteToolAsync(toolName, arguments, _diagramsDirectory, _logWriter, _verbose);
                return result;
            }
            catch (ArgumentException ex) when (ex.Message.Contains("parameter"))
            {
                // Create a more detailed error for parameter validation issues
                var toolInfo = GetToolInfo(toolName);
                
                throw new ArgumentException(ex.Message, ex)
                {
                    Data = { ["details"] = toolInfo }
                };
            }
        }

        private object GetToolInfo(string toolName)
        {
            // This method would return the tool information for detailed error reports
            // For brevity, returning null for now - would be implemented with actual tool info
            return null;
        }

        private async Task<object> ShutdownAsync(JsonElement parameters)
        {
            this.LogInfo("Received shutdown request");
            // Add a small delay to ensure the response is sent before shutdown
            await Task.Delay(100);
            // Return success response
            return new { success = true };
        }
    }
} 