using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace DrawIO.MCP.SSE.Tests
{
    // Mock controller for testing MCP API requests
    [ApiController]
    [Route("mcp")]
    public class MockMcpController : ControllerBase
    {
        private readonly string _diagramsDirectory;
        
        public MockMcpController()
        {
            _diagramsDirectory = Environment.GetEnvironmentVariable("DIAGRAMS_DIR") ?? 
                                Path.Combine(Path.GetTempPath(), "test_diagrams");
            
            if (!Directory.Exists(_diagramsDirectory))
            {
                Directory.CreateDirectory(_diagramsDirectory);
            }
        }
        
        [HttpPost("api")]
        public async Task<IActionResult> HandleMcpRequest()
        {
            // Read request body
            using var reader = new StreamReader(Request.Body);
            var requestBody = await reader.ReadToEndAsync();
            
            try
            {
                var request = JsonSerializer.Deserialize<JsonElement>(requestBody);
                
                if (request.TryGetProperty("method", out var methodElement))
                {
                    string method = methodElement.GetString() ?? "";
                    
                    if (method == "mcp/executeTool")
                    {
                        return HandleExecuteTool(request);
                    }
                    else if (method == "mcp/getResource")
                    {
                        return HandleGetResource(request);
                    }
                    else if (method == "mcp/listResources")
                    {
                        return HandleListResources(request);
                    }
                }
                
                return BadRequest(new
                {
                    jsonrpc = "2.0",
                    id = request.GetProperty("id"),
                    error = new
                    {
                        code = -32601,
                        message = "Method not found"
                    }
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    jsonrpc = "2.0",
                    id = 0, // Default ID value to avoid null warning
                    error = new
                    {
                        code = -32700,
                        message = $"Parse error: {ex.Message}"
                    }
                });
            }
        }
        
        private IActionResult HandleExecuteTool(JsonElement request)
        {
            if (!request.TryGetProperty("params", out var paramsElement) ||
                !paramsElement.TryGetProperty("tool", out var toolElement))
            {
                return BadRequest(new
                {
                    jsonrpc = "2.0",
                    id = request.GetProperty("id"),
                    error = new
                    {
                        code = -32602,
                        message = "Invalid params"
                    }
                });
            }
            
            string tool = toolElement.GetString() ?? "";
            var parameters = paramsElement.TryGetProperty("parameters", out var parametersElement) 
                ? parametersElement 
                : new JsonElement();
            
            // Return success responses based on the tool
            object result = tool switch
            {
                "create_new_diagram" => HandleCreateNewDiagram(parameters),
                "add_shape" => HandleAddShape(parameters),
                "connect_shapes" => HandleConnectShapes(parameters),
                "delete_shape" => HandleDeleteShape(parameters),
                "update_shape" => HandleUpdateShape(parameters),
                "style_shape" => HandleStyleShape(parameters),
                "arrange_diagram" => HandleArrangeDiagram(parameters),
                "create_diagram_page" => HandleCreateDiagramPage(parameters),
                "move_cell_between_pages" => HandleMoveCellBetweenPages(parameters),
                "generate_vpc" => HandleGenerateVpc(parameters),
                _ => throw new ArgumentException($"Unknown tool: {tool}")
            };
            
            return Ok(new
            {
                jsonrpc = "2.0",
                id = request.GetProperty("id"),
                result = result
            });
        }
        
        private IActionResult HandleGetResource(JsonElement request)
        {
            if (!request.TryGetProperty("params", out var paramsElement) ||
                !paramsElement.TryGetProperty("resourceId", out var resourceIdElement))
            {
                return BadRequest(new
                {
                    jsonrpc = "2.0",
                    id = request.GetProperty("id"),
                    error = new
                    {
                        code = -32602,
                        message = "Invalid params"
                    }
                });
            }
            
            string resourceId = resourceIdElement.GetString() ?? "";
            
            if (resourceId.StartsWith("diagram://"))
            {
                string diagramName = resourceId.Substring("diagram://".Length);
                
                // Create a mock diagram response
                var result = new
                {
                    id = resourceId,
                    type = "diagram",
                    content = new
                    {
                        modified = DateTime.UtcNow,
                        pages = new[]
                        {
                            new
                            {
                                id = "page-1",
                                name = "Page-1",
                                cells = new object[]
                                {
                                    new
                                    {
                                        id = "0",
                                        value = "",
                                        style = "",
                                        parent = "",
                                        isVertex = false,
                                        isEdge = false
                                    },
                                    new
                                    {
                                        id = "1",
                                        value = "",
                                        style = "",
                                        parent = "0",
                                        isVertex = false,
                                        isEdge = false
                                    }
                                }
                            }
                        }
                    }
                };
                
                return Ok(new
                {
                    jsonrpc = "2.0",
                    id = request.GetProperty("id"),
                    result = result
                });
            }
            
            return BadRequest(new
            {
                jsonrpc = "2.0",
                id = request.GetProperty("id"),
                error = new
                {
                    code = -32602,
                    message = $"Unknown resource ID: {resourceId}"
                }
            });
        }
        
        private IActionResult HandleListResources(JsonElement request)
        {
            var resources = new[]
            {
                new
                {
                    id = "diagram://test.drawio",
                    type = "diagram",
                    title = "test.drawio"
                }
            };
            
            return Ok(new
            {
                jsonrpc = "2.0",
                id = request.GetProperty("id"),
                result = new
                {
                    resources = resources
                }
            });
        }
        
        // Tool handler methods
        private object HandleCreateNewDiagram(JsonElement parameters)
        {
            string name = parameters.TryGetProperty("name", out var nameElement) 
                ? nameElement.GetString() ?? "diagram.drawio"
                : "diagram.drawio";
                
            return new
            {
                Status = "created",
                DiagramId = $"diagram://{name}",
                FileName = name
            };
        }
        
        private object HandleAddShape(JsonElement parameters)
        {
            return new
            {
                Status = "added",
                ShapeId = Guid.NewGuid().ToString("N").Substring(0, 8)
            };
        }
        
        private object HandleConnectShapes(JsonElement parameters)
        {
            return new
            {
                Status = "connected",
                EdgeId = Guid.NewGuid().ToString("N").Substring(0, 8)
            };
        }
        
        private object HandleDeleteShape(JsonElement parameters)
        {
            return new
            {
                Status = "deleted"
            };
        }
        
        private object HandleUpdateShape(JsonElement parameters)
        {
            return new
            {
                Status = "updated"
            };
        }
        
        private object HandleStyleShape(JsonElement parameters)
        {
            return new
            {
                Status = "styled"
            };
        }
        
        private object HandleArrangeDiagram(JsonElement parameters)
        {
            return new
            {
                Status = "success"
            };
        }
        
        private object HandleCreateDiagramPage(JsonElement parameters)
        {
            return new
            {
                Status = "created",
                PageId = Guid.NewGuid().ToString()
            };
        }
        
        private object HandleMoveCellBetweenPages(JsonElement parameters)
        {
            return new
            {
                Status = "moved"
            };
        }
        
        private object HandleGenerateVpc(JsonElement parameters)
        {
            string name = parameters.TryGetProperty("name", out var nameElement) 
                ? nameElement.GetString() ?? "vpc.drawio"
                : "vpc.drawio";
                
            return new
            {
                Status = "created",
                DiagramId = $"diagram://{name}",
                FileName = name
            };
        }
    }
} 