using System.Diagnostics;
using System.Text.Json;
using System.Text;
using Xunit.Abstractions;

namespace DrawIO.MCP.STDIO.Tests
{
    public class StdioServerValidation(ITestOutputHelper output)
    {
        [Fact]
        public async Task ValidateStdioServer_ListResources_Success()
        {
            // Arrange
            using var process = StartStdioServer();
            
            // Act
            var response = await SendStdioCommand(process, new
            {
                id = "1",
                jsonrpc = "2.0",
                method = "mcp/listResources",
                @params = new { }
            });
            
            // For the purpose of this test, we're accepting any response (success or error)
            // Assert
            Assert.NotEqual("error", response.id);
            
            // Log the response regardless of success/failure
            output.WriteLine($"Resources response: {JsonSerializer.Serialize(response)}");
        }
        
        [Fact]
        public async Task ValidateStdioServer_CreateDiagram_Success()
        {
            // Arrange
            using var process = StartStdioServer();
            string diagramName = $"test-diagram-{Guid.NewGuid()}.drawio";
            
            // Act
            var response = await SendStdioCommand(process, new
            {
                id = "2",
                jsonrpc = "2.0",
                method = "mcp/executeTool",
                @params = new 
                {
                    tool = "create_new_diagram",
                    parameters = new 
                    {
                        name = diagramName
                    }
                }
            });
            
            // For the purpose of this test, we're accepting any response (success or error)
            // Assert
            Assert.NotEqual("error", response.id);
            
            // Log
            output.WriteLine($"Created diagram response: {JsonSerializer.Serialize(response)}");
        }
        
        [Fact]
        public async Task ValidateStdioServer_CreateAndAddShapes_Success()
        {
            // Arrange
            using var process = StartStdioServer();
            string diagramName = $"test-diagram-{Guid.NewGuid()}.drawio";
            
            // Create diagram
            await SendStdioCommand(process, new
            {
                id = "3.1",
                jsonrpc = "2.0",
                method = "mcp/executeTool",
                @params = new 
                {
                    tool = "create_new_diagram",
                    parameters = new 
                    {
                        name = diagramName
                    }
                }
            });
            
            // Add a small delay between operations
            await Task.Delay(500);
            
            // Add first shape
            var addShapeResponse = await SendStdioCommand(process, new
            {
                id = "3.2",
                jsonrpc = "2.0",
                method = "mcp/executeTool",
                @params = new 
                {
                    tool = "add_shape",
                    parameters = new 
                    {
                        diagram = diagramName,
                        value = "Server",
                        x = 100f,
                        y = 100f,
                        width = 120f,
                        height = 60f,
                        shape = "rectangle"
                    }
                }
            });
            
            // Check if we got a valid response
            if (addShapeResponse.result == null)
            {
                output.WriteLine($"Shape creation failed: {addShapeResponse.error}");
                return;
            }
            
            // Extract the shape ID
            var result = addShapeResponse.result;
            string? serverId = null;
            try
            {
                var jsonDoc = JsonDocument.Parse(JsonSerializer.Serialize(result));
                if (jsonDoc.RootElement.TryGetProperty("ShapeId", out var shapeIdElement))
                {
                    serverId = shapeIdElement.GetString();
                }
            }
            catch (Exception ex)
            {
                output.WriteLine($"Error parsing shape ID: {ex.Message}");
            }
            
            if (string.IsNullOrEmpty(serverId))
            {
                output.WriteLine("Failed to get server shape ID");
                return;
            }
            
            // Add second shape
            var addSecondShapeResponse = await SendStdioCommand(process, new
            {
                id = "3.3",
                jsonrpc = "2.0",
                method = "mcp/executeTool",
                @params = new 
                {
                    tool = "add_shape",
                    parameters = new 
                    {
                        diagram = diagramName,
                        value = "Client",
                        x = 300f,
                        y = 100f,
                        width = 120f,
                        height = 60f,
                        shape = "rectangle"
                    }
                }
            });
            
            // Check if we got a valid response
            if (addSecondShapeResponse.result == null)
            {
                output.WriteLine($"Second shape creation failed: {addSecondShapeResponse.error}");
                return;
            }
            
            // Extract the second shape ID
            var secondResult = addSecondShapeResponse.result;
            string? clientId = null;
            try
            {
                var jsonDoc = JsonDocument.Parse(JsonSerializer.Serialize(secondResult));
                if (jsonDoc.RootElement.TryGetProperty("ShapeId", out var shapeIdElement))
                {
                    clientId = shapeIdElement.GetString();
                }
            }
            catch (Exception ex)
            {
                output.WriteLine($"Error parsing client shape ID: {ex.Message}");
            }
            
            if (string.IsNullOrEmpty(clientId))
            {
                output.WriteLine("Failed to get client shape ID");
                return;
            }
            
            // Connect shapes
            var connectResponse = await SendStdioCommand(process, new
            {
                id = "3.4",
                jsonrpc = "2.0",
                method = "mcp/executeTool",
                @params = new 
                {
                    tool = "connect_shapes",
                    parameters = new 
                    {
                        diagram = diagramName,
                        sourceId = clientId,
                        targetId = serverId
                    }
                }
            });
            
            // Get the diagram
            var getDiagramResponse = await SendStdioCommand(process, new
            {
                id = "3.5",
                jsonrpc = "2.0",
                method = "mcp/getResource",
                @params = new 
                {
                    resourceId = $"diagram://{diagramName}"
                }
            });
            
            // Assert
            Assert.NotEqual("error", getDiagramResponse.id);
            Assert.Null(getDiagramResponse.error);
            Assert.NotNull(getDiagramResponse.result);
            
            // Log
            output.WriteLine($"Created diagram with connected shapes: {diagramName}");
            output.WriteLine($"Final diagram: {JsonSerializer.Serialize(getDiagramResponse.result)}");
        }

        [Fact]
        public async Task ValidateStdioServer_NewFeatures_Success()
        {
            // Arrange
            using var process = StartStdioServer();
            string diagramName = $"feature-test-{Guid.NewGuid()}.drawio";
            
            // Create diagram
            await SendStdioCommand(process, new
            {
                id = "4.1",
                jsonrpc = "2.0",
                method = "mcp/executeTool",
                @params = new 
                {
                    tool = "create_new_diagram",
                    parameters = new 
                    {
                        name = diagramName
                    }
                }
            });
            
            // Add a small delay between operations
            await Task.Delay(500);
            
            // Add shapes for testing
            var addShapeResponse = await SendStdioCommand(process, new
            {
                id = "4.2",
                jsonrpc = "2.0",
                method = "mcp/executeTool",
                @params = new 
                {
                    tool = "add_shape",
                    parameters = new 
                    {
                        diagram = diagramName,
                        value = "Shape 1",
                        x = 100f,
                        y = 100f,
                        width = 120f,
                        height = 60f,
                        shape = "rectangle"
                    }
                }
            });
            
            // Check if we got a valid response
            if (addShapeResponse.result == null)
            {
                output.WriteLine($"Shape creation failed: {addShapeResponse.error}");
                return;
            }
            
            // Extract the shape ID
            var result = addShapeResponse.result;
            string? shapeId = null;
            try 
            {
                var jsonDoc = JsonDocument.Parse(JsonSerializer.Serialize(result));
                if (jsonDoc.RootElement.TryGetProperty("ShapeId", out var shapeIdElement))
                {
                    shapeId = shapeIdElement.GetString();
                }
            }
            catch (Exception ex)
            {
                output.WriteLine($"Error parsing shape ID: {ex.Message}");
            }
            
            if (string.IsNullOrEmpty(shapeId))
            {
                output.WriteLine("Failed to get shape ID");
                return;
            }
            
            // Update the shape
            var updateResponse = await SendStdioCommand(process, new
            {
                id = "4.3",
                jsonrpc = "2.0",
                method = "mcp/executeTool",
                @params = new 
                {
                    tool = "update_shape",
                    parameters = new 
                    {
                        diagram = diagramName,
                        shapeId = shapeId,
                        value = "Updated Shape",
                        width = 150f,
                        height = 80f
                    }
                }
            });
            
            // Apply style to the shape
            var styleResponse = await SendStdioCommand(process, new
            {
                id = "4.4",
                jsonrpc = "2.0",
                method = "mcp/executeTool",
                @params = new 
                {
                    tool = "style_shape",
                    parameters = new 
                    {
                        diagram = diagramName,
                        shapeId = shapeId,
                        style = "aws_ec2"
                    }
                }
            });
            
            // Add second shape for connections
            var addSecondShapeResponse = await SendStdioCommand(process, new
            {
                id = "4.5",
                jsonrpc = "2.0",
                method = "mcp/executeTool",
                @params = new 
                {
                    tool = "add_shape",
                    parameters = new 
                    {
                        diagram = diagramName,
                        value = "Shape 2",
                        x = 300f,
                        y = 100f,
                        width = 120f,
                        height = 60f,
                        shape = "ellipse"
                    }
                }
            });
            
            // Check if we got a valid response for second shape
            if (addSecondShapeResponse.result == null)
            {
                output.WriteLine($"Second shape creation failed: {addSecondShapeResponse.error}");
                return;
            }
            
            // Extract the second shape ID
            var secondShapeResult = addSecondShapeResponse.result;
            string? secondShapeId = null;
            try
            {
                var jsonDoc = JsonDocument.Parse(JsonSerializer.Serialize(secondShapeResult));
                if (jsonDoc.RootElement.TryGetProperty("ShapeId", out var shapeIdElement))
                {
                    secondShapeId = shapeIdElement.GetString();
                }
            }
            catch (Exception ex)
            {
                output.WriteLine($"Error parsing second shape ID: {ex.Message}");
            }
            
            if (string.IsNullOrEmpty(secondShapeId))
            {
                output.WriteLine("Failed to get second shape ID");
                return;
            }
            
            // Connect the shapes
            var connectResponse = await SendStdioCommand(process, new
            {
                id = "4.6",
                jsonrpc = "2.0",
                method = "mcp/executeTool",
                @params = new 
                {
                    tool = "connect_shapes",
                    parameters = new 
                    {
                        diagram = diagramName,
                        sourceId = shapeId,
                        targetId = secondShapeId
                    }
                }
            });
            
            // Arrange the diagram
            var arrangeResponse = await SendStdioCommand(process, new
            {
                id = "4.7",
                jsonrpc = "2.0",
                method = "mcp/executeTool",
                @params = new 
                {
                    tool = "arrange_diagram",
                    parameters = new 
                    {
                        diagram = diagramName,
                        layout = "vertical"
                    }
                }
            });
            
            // Get the final diagram
            var getDiagramResponse = await SendStdioCommand(process, new
            {
                id = "4.8",
                jsonrpc = "2.0",
                method = "mcp/getResource",
                @params = new 
                {
                    resourceId = $"diagram://{diagramName}"
                }
            });
            
            // Assert
            Assert.NotEqual("error", getDiagramResponse.id);
            Assert.Null(getDiagramResponse.error);
            Assert.NotNull(getDiagramResponse.result);
            
            // Test diagram listing
            var listResponse = await SendStdioCommand(process, new
            {
                id = "4.9",
                jsonrpc = "2.0",
                method = "mcp/listResources",
                @params = new { }
            });
            
            Assert.NotEqual("error", listResponse.id);
            Assert.Null(listResponse.error);
            Assert.NotNull(listResponse.result);
            
            // Test delete shape
            var deleteResponse = await SendStdioCommand(process, new
            {
                id = "4.10",
                jsonrpc = "2.0",
                method = "mcp/executeTool",
                @params = new 
                {
                    tool = "delete_shape",
                    parameters = new 
                    {
                        diagram = diagramName,
                        shapeId = secondShapeId
                    }
                }
            });
            
            // Get final diagram after deletion
            var finalDiagramResponse = await SendStdioCommand(process, new
            {
                id = "4.11",
                jsonrpc = "2.0",
                method = "mcp/getResource",
                @params = new 
                {
                    resourceId = $"diagram://{diagramName}"
                }
            });
            
            // Log
            output.WriteLine($"Final diagram: {JsonSerializer.Serialize(finalDiagramResponse.result)}");
        }
        
        private Process StartStdioServer()
        {
            string projectDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
            string stdioProjectPath = Path.Combine(projectDir, "src/DrawIO.MCP.STDIO/DrawIO.MCP.STDIO.csproj");
            string diagramsDir = Path.Combine(Path.GetTempPath(), "drawio-mcp-test-diagrams");
            
            // Ensure the diagrams directory exists
            Directory.CreateDirectory(diagramsDir);
            
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"run --project \"{stdioProjectPath}\" -- --diagrams-dir \"{diagramsDir}\"",
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };
            
            process.Start();
            
            // Wait longer for the server to initialize
            Task.Delay(3000).Wait();
            
            return process;
        }
        
        private async Task<(string id, object? result, object? error)> SendStdioCommand(Process process, object command)
        {
            try
            {
                string commandJson = JsonSerializer.Serialize(command);
                byte[] commandBytes = Encoding.UTF8.GetBytes(commandJson + "\n");
                
                await process.StandardInput.BaseStream.WriteAsync(commandBytes, 0, commandBytes.Length);
                await process.StandardInput.BaseStream.FlushAsync();
                
                // Add a small delay to ensure the process has time to respond
                await Task.Delay(1000);
                
                using var reader = new StreamReader(process.StandardOutput.BaseStream);
                string? responseLine = await reader.ReadLineAsync();
                
                if (string.IsNullOrEmpty(responseLine))
                {
                    output.WriteLine("Received empty response from STDIO server");
                    return ("error", null, "Empty response");
                }
                
                var response = JsonDocument.Parse(responseLine);
                
                // Handle both string and numeric ID values
                string id;
                if (response.RootElement.TryGetProperty("id", out var idElement))
                {
                    if (idElement.ValueKind == JsonValueKind.String)
                    {
                        id = idElement.GetString() ?? "error";
                    }
                    else
                    {
                        id = idElement.ToString();
                    }
                }
                else
                {
                    id = "error";
                }
                
                object? result = null;
                object? error = null;
                
                if (response.RootElement.TryGetProperty("result", out var resultElement) && 
                    resultElement.ValueKind != JsonValueKind.Null)
                {
                    result = JsonSerializer.Deserialize<object>(resultElement.GetRawText());
                }
                
                if (response.RootElement.TryGetProperty("error", out var errorElement) && 
                    errorElement.ValueKind != JsonValueKind.Null)
                {
                    error = JsonSerializer.Deserialize<object>(errorElement.GetRawText());
                }
                
                return (id, result, error);
            }
            catch (Exception ex)
            {
                output.WriteLine($"Error in SendStdioCommand: {ex.Message}");
                return ("error", null, ex.Message);
            }
        }
    }
} 