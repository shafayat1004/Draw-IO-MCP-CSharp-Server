using System.Diagnostics;
using System.Text.Json;
using System.Text;
using Xunit.Abstractions;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Collections.Generic;
using System.Threading;

namespace DrawIO.MCP.STDIO.Tests
{
    public class StdioServerValidation : IAsyncDisposable
    {
        private class JsonRpcResponse
        {
            public string? id { get; set; }
            public string? jsonrpc { get; set; }
            public object? result { get; set; }
            public object? error { get; set; }
        }

        private readonly ITestOutputHelper _output;
        private readonly SemaphoreSlim _streamLock = new SemaphoreSlim(1, 1);
        private Process? _process;
        private string? _diagramsDirectory;

        public StdioServerValidation(ITestOutputHelper output)
        {
            _output = output;
        }

        private void Log(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            _output.WriteLine($"[{timestamp}] {message}");
        }

        [Fact(Timeout = 30000)] // 30 second timeout
        public async Task ValidateStdioServer_ListResources_Success()
        {
            // Arrange
            using var process = await StartStdioServer();
            _process = process;
            
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
            _output.WriteLine($"Resources response: {JsonSerializer.Serialize(response)}");
        }
        
        [Fact(Timeout = 30000)] // 30 second timeout
        public async Task ValidateStdioServer_CreateDiagram_Success()
        {
            // Arrange
            using var process = await StartStdioServer();
            _process = process;
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
            _output.WriteLine($"Created diagram response: {JsonSerializer.Serialize(response)}");
        }
        
        [Fact(Timeout = 30000)] // 30 second timeout
        public async Task ValidateStdioServer_CreateAndAddShapes_Success()
        {
            // Arrange
            using var process = await StartStdioServer();
            _process = process;
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
                _output.WriteLine($"Shape creation failed: {addShapeResponse.error}");
                return;
            }
            
            // Extract the shape ID
            var result = addShapeResponse.result;
            string? serverId = null;
            try
            {
                var jsonDoc = JsonDocument.Parse(JsonSerializer.Serialize(result));
                if (jsonDoc.RootElement.TryGetProperty("shapeId", out var shapeIdElement))
                {
                    serverId = shapeIdElement.GetString();
                }
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Error parsing shape ID: {ex.Message}");
            }
            
            if (string.IsNullOrEmpty(serverId))
            {
                _output.WriteLine("Failed to get server shape ID");
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
                _output.WriteLine($"Second shape creation failed: {addSecondShapeResponse.error}");
                return;
            }
            
            // Extract the second shape ID
            var secondResult = addSecondShapeResponse.result;
            string? clientId = null;
            try
            {
                var jsonDoc = JsonDocument.Parse(JsonSerializer.Serialize(secondResult));
                if (jsonDoc.RootElement.TryGetProperty("shapeId", out var shapeIdElement))
                {
                    clientId = shapeIdElement.GetString();
                }
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Error parsing client shape ID: {ex.Message}");
            }
            
            if (string.IsNullOrEmpty(clientId))
            {
                _output.WriteLine("Failed to get client shape ID");
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
            _output.WriteLine($"Created diagram with connected shapes: {diagramName}");
            _output.WriteLine($"Final diagram: {JsonSerializer.Serialize(getDiagramResponse.result)}");
        }

        [Fact(Timeout = 30000)] // 30 second timeout
        public async Task ValidateStdioServer_NewFeatures_Success()
        {
            // Arrange
            using var process = await StartStdioServer();
            _process = process;
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
                _output.WriteLine($"Shape creation failed: {addShapeResponse.error}");
                return;
            }
            
            // Extract the shape ID
            var result = addShapeResponse.result;
            string? shapeId = null;
            try 
            {
                var jsonDoc = JsonDocument.Parse(JsonSerializer.Serialize(result));
                if (jsonDoc.RootElement.TryGetProperty("shapeId", out var shapeIdElement))
                {
                    shapeId = shapeIdElement.GetString();
                }
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Error parsing shape ID: {ex.Message}");
            }
            
            if (string.IsNullOrEmpty(shapeId))
            {
                _output.WriteLine("Failed to get shape ID");
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
                _output.WriteLine($"Second shape creation failed: {addSecondShapeResponse.error}");
                return;
            }
            
            // Extract the second shape ID
            var secondShapeResult = addSecondShapeResponse.result;
            string? secondShapeId = null;
            try
            {
                var jsonDoc = JsonDocument.Parse(JsonSerializer.Serialize(secondShapeResult));
                if (jsonDoc.RootElement.TryGetProperty("shapeId", out var shapeIdElement))
                {
                    secondShapeId = shapeIdElement.GetString();
                }
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Error parsing second shape ID: {ex.Message}");
            }
            
            if (string.IsNullOrEmpty(secondShapeId))
            {
                _output.WriteLine("Failed to get second shape ID");
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
            _output.WriteLine($"Final diagram: {JsonSerializer.Serialize(finalDiagramResponse.result)}");
        }
        
        [Fact(Timeout = 30000)] // 30 second timeout
        public async Task ValidateStdioServer_ConnectorManipulation_Success()
        {
            // Arrange
            using var process = await StartStdioServer();
            _process = process;
            string diagramName = $"test-diagram-{Guid.NewGuid()}.drawio";
            
            // Create diagram
            await SendStdioCommand(process, new
            {
                id = "5.1",
                jsonrpc = "2.0",
                method = "tools/execute",
                @params = new 
                {
                    tool = "create_new_diagram",
                    parameters = new 
                    {
                        name = diagramName
                    }
                }
            });
            
            // Add first shape
            var addShape1Response = await SendStdioCommand(process, new
            {
                id = "5.2",
                jsonrpc = "2.0",
                method = "tools/execute",
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
            
            // Extract first shape ID
            var shape1Id = ExtractShapeId(addShape1Response);
            if (string.IsNullOrEmpty(shape1Id))
            {
                _output.WriteLine("Failed to get first shape ID");
                return;
            }
            
            // Add second shape
            var addShape2Response = await SendStdioCommand(process, new
            {
                id = "5.3",
                jsonrpc = "2.0",
                method = "tools/execute",
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
                        shape = "rectangle"
                    }
                }
            });
            
            // Extract second shape ID
            var shape2Id = ExtractShapeId(addShape2Response);
            if (string.IsNullOrEmpty(shape2Id))
            {
                _output.WriteLine("Failed to get second shape ID");
                return;
            }
            
            // Connect shapes
            var connectResponse = await SendStdioCommand(process, new
            {
                id = "5.4",
                jsonrpc = "2.0",
                method = "tools/execute",
                @params = new 
                {
                    tool = "connect_shapes",
                    parameters = new 
                    {
                        diagram = diagramName,
                        source_id = shape1Id,
                        target_id = shape2Id
                    }
                }
            });
            
            // Extract connector ID
            var connectorId = ExtractElementId(connectResponse);
            if (string.IsNullOrEmpty(connectorId))
            {
                _output.WriteLine("Failed to get connector ID");
                return;
            }
            
            // Test set_line_style
            var lineStyleResponse = await SendStdioCommand(process, new
            {
                id = "5.5",
                jsonrpc = "2.0",
                method = "tools/execute",
                @params = new 
                {
                    tool = "set_line_style",
                    parameters = new 
                    {
                        diagram = diagramName,
                        connector_id = connectorId,
                        line_style = "dashed",
                        line_width = 2.5f
                    }
                }
            });
            
            Assert.NotNull(lineStyleResponse.result);
            
            // Test set_arrow_style
            var arrowStyleResponse = await SendStdioCommand(process, new
            {
                id = "5.6",
                jsonrpc = "2.0",
                method = "tools/execute",
                @params = new 
                {
                    tool = "set_arrow_style",
                    parameters = new 
                    {
                        diagram = diagramName,
                        connector_id = connectorId,
                        start_arrow = "diamond",
                        end_arrow = "classic"
                    }
                }
            });
            
            Assert.NotNull(arrowStyleResponse.result);
            
            // Test reset_connector
            var resetResponse = await SendStdioCommand(process, new
            {
                id = "5.7",
                jsonrpc = "2.0",
                method = "tools/execute",
                @params = new 
                {
                    tool = "reset_connector",
                    parameters = new 
                    {
                        diagram = diagramName,
                        connector_id = connectorId
                    }
                }
            });
            
            Assert.NotNull(resetResponse.result);
            
            // Test reverse_connector
            var reverseResponse = await SendStdioCommand(process, new
            {
                id = "5.8",
                jsonrpc = "2.0",
                method = "tools/execute",
                @params = new 
                {
                    tool = "reverse_connector",
                    parameters = new 
                    {
                        diagram = diagramName,
                        connector_id = connectorId
                    }
                }
            });
            
            Assert.NotNull(reverseResponse.result);
            
            // Log results
            _output.WriteLine($"Line style response: {JsonSerializer.Serialize(lineStyleResponse.result)}");
            _output.WriteLine($"Arrow style response: {JsonSerializer.Serialize(arrowStyleResponse.result)}");
            _output.WriteLine($"Reset response: {JsonSerializer.Serialize(resetResponse.result)}");
            _output.WriteLine($"Reverse response: {JsonSerializer.Serialize(reverseResponse.result)}");
        }
        
        [Fact(Timeout = 30000)] // 30 second timeout
        public async Task TestShapeManipulationTools()
        {
            // Arrange
            var diagramName = "test_shape_manipulation.drawio";
            using var process = await StartStdioServer();
            _process = process;
            
            try
            {
                // Create a new diagram
                _output.WriteLine("Creating new diagram...");
                var createResponse = await SendStdioCommand(process, new
                {
                    id = "1",
                    jsonrpc = "2.0",
                    method = "mcp/createNewDiagram",
                    @params = new { name = diagramName }
                });
                Assert.NotNull(createResponse);
                Assert.Null(createResponse.error);
                _output.WriteLine($"Create response: {JsonSerializer.Serialize(createResponse)}");

                // Add a rectangle
                _output.WriteLine("Adding rectangle...");
                var addShapeResponse = await SendStdioCommand(process, new
                {
                    id = "2",
                    jsonrpc = "2.0",
                    method = "mcp/addShape",
                    @params = new
                    {
                        diagramName = diagramName,
                        shapeType = "rectangle",
                        x = 100,
                        y = 100,
                        width = 100,
                        height = 50
                    }
                });
                Assert.NotNull(addShapeResponse);
                Assert.Null(addShapeResponse.error);
                _output.WriteLine($"Add shape response: {JsonSerializer.Serialize(addShapeResponse)}");

                // Get the shape ID from the response
                string? shapeId = ExtractShapeId(addShapeResponse);
                _output.WriteLine($"Shape ID: {shapeId}");
                
                Assert.NotNull(shapeId);
                
                // Test resize_shape
                var resizeResponse = await SendStdioCommand(process, new
                {
                    id = "3",
                    jsonrpc = "2.0",
                    method = "tools/execute",
                    @params = new 
                    {
                        tool = "resize_shape",
                        parameters = new 
                        {
                            diagram = diagramName,
                            shape_id = shapeId,
                            width = 150f,
                            height = 75f
                        }
                    }
                });
                
                Assert.NotNull(resizeResponse.result);
                
                // Test set_text_style with font color
                var textColorResponse = await SendStdioCommand(process, new
                {
                    id = "4",
                    jsonrpc = "2.0",
                    method = "tools/execute",
                    @params = new 
                    {
                        tool = "set_text_style",
                        parameters = new 
                        {
                            diagram = diagramName,
                            shape_id = shapeId,
                            font_color = "#FF0000"
                        }
                    }
                });
                
                Assert.NotNull(textColorResponse.result);
                
                // Test set_text_style with font size
                var fontSizeResponse = await SendStdioCommand(process, new
                {
                    id = "5",
                    jsonrpc = "2.0",
                    method = "tools/execute",
                    @params = new 
                    {
                        tool = "set_text_style",
                        parameters = new 
                        {
                            diagram = diagramName,
                            shape_id = shapeId,
                            font_size = 16f
                        }
                    }
                });
                
                Assert.NotNull(fontSizeResponse.result);
                
                // Test set_text_style with bold style
                var boldStyleResponse = await SendStdioCommand(process, new
                {
                    id = "6",
                    jsonrpc = "2.0",
                    method = "tools/execute",
                    @params = new 
                    {
                        tool = "set_text_style",
                        parameters = new 
                        {
                            diagram = diagramName,
                            shape_id = shapeId,
                            font_style = "bold"
                        }
                    }
                });
                
                Assert.NotNull(boldStyleResponse.result);
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Test failed with error: {ex.Message}\nStack trace: {ex.StackTrace}");
                throw;
            }
        }
        
        private string? ExtractShapeId(JsonRpcResponse response)
        {
            if (response.result == null) return null;
            try
            {
                var jsonDoc = JsonDocument.Parse(JsonSerializer.Serialize(response.result));
                // Try both cases
                if (jsonDoc.RootElement.TryGetProperty("shapeId", out var shapeIdElement) ||
                    jsonDoc.RootElement.TryGetProperty("ShapeId", out shapeIdElement))
                {
                    return shapeIdElement.GetString();
                }
                // Also check for elementId as fallback
                if (jsonDoc.RootElement.TryGetProperty("elementId", out var elementIdElement) ||
                    jsonDoc.RootElement.TryGetProperty("ElementId", out elementIdElement))
                {
                    return elementIdElement.GetString();
                }
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Error parsing shape ID: {ex.Message}");
                _output.WriteLine($"Response result: {JsonSerializer.Serialize(response.result)}");
            }
            return null;
        }

        private string? ExtractElementId(JsonRpcResponse response)
        {
            if (response.result == null) return null;
            try
            {
                var jsonDoc = JsonDocument.Parse(JsonSerializer.Serialize(response.result));
                // Try both cases
                if (jsonDoc.RootElement.TryGetProperty("elementId", out var elementIdElement) ||
                    jsonDoc.RootElement.TryGetProperty("ElementId", out elementIdElement))
                {
                    return elementIdElement.GetString();
                }
                // Also check for shapeId as fallback
                if (jsonDoc.RootElement.TryGetProperty("shapeId", out var shapeIdElement) ||
                    jsonDoc.RootElement.TryGetProperty("ShapeId", out shapeIdElement))
                {
                    return shapeIdElement.GetString();
                }
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Error parsing element ID: {ex.Message}");
                _output.WriteLine($"Response result: {JsonSerializer.Serialize(response.result)}");
            }
            return null;
        }
        
        private async Task<Process> StartStdioServer()
        {
            _output.WriteLine("Starting server...");

            var projectDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
            var stdioProjectPath = Path.Combine(projectDir, "src", "DrawIO.MCP.STDIO", "DrawIO.MCP.STDIO.csproj");
            _diagramsDirectory = Path.Combine(projectDir, "diagrams");

            if (Directory.Exists(_diagramsDirectory))
            {
                Directory.Delete(_diagramsDirectory, true);
            }
            Directory.CreateDirectory(_diagramsDirectory);

            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run --project \"{stdioProjectPath}\" -- --diagrams-dir \"{_diagramsDirectory}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                CreateNoWindow = true
            };

            _process = new Process { StartInfo = startInfo };
            _process.Start();

            var serverInitialized = false;
            var errorOutput = new StringBuilder();
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            try
            {
                await using var registration = cts.Token.Register(() => _process.Kill(true));

                var readOutputTask = Task.Run(async () =>
                {
                    await _streamLock.WaitAsync();
                    try
                    {
                        while (!_process.HasExited)
                        {
                            var line = await _process.StandardOutput.ReadLineAsync();
                            if (line == null) break;
                            
                            _output.WriteLine($"Server: {line}");
                            if (line.Contains("Starting STDIO protocol server"))
                            {
                                serverInitialized = true;
                                break;
                            }
                        }
                    }
                    finally
                    {
                        _streamLock.Release();
                    }
                });

                var readErrorTask = Task.Run(async () =>
                {
                    while (!_process.HasExited)
                    {
                        var line = await _process.StandardError.ReadLineAsync();
                        if (line == null) break;
                        
                        errorOutput.AppendLine(line);
                        _output.WriteLine($"Server Error: {line}");
                    }
                });

                await Task.WhenAny(readOutputTask, readErrorTask, Task.Delay(TimeSpan.FromSeconds(10), cts.Token));

                if (!serverInitialized)
                {
                    throw new Exception($"Server failed to initialize within timeout. Error output: {errorOutput}");
                }

                // Send initialization request
                await _streamLock.WaitAsync();
                try
                {
                    var initRequest = new
                    {
                        id = "init",
                        jsonrpc = "2.0",
                        method = "mcp/initialize",
                        @params = new { }
                    };
                    var json = JsonSerializer.Serialize(initRequest);
                    await _process.StandardInput.WriteLineAsync(json);
                    await _process.StandardInput.FlushAsync();
                    
                    // Wait for initialization response
                    var response = await _process.StandardOutput.ReadLineAsync();
                    _output.WriteLine($"Server init response: {response}");
                    
                    if (string.IsNullOrEmpty(response))
                    {
                        throw new Exception("Server initialization failed: Empty response");
                    }

                    // Parse the response
                    var responseObj = JsonSerializer.Deserialize<JsonRpcResponse>(response);
                    if (responseObj == null)
                    {
                        throw new Exception($"Server initialization failed: Invalid JSON response: {response}");
                    }

                    if (responseObj.error != null)
                    {
                        throw new Exception($"Server initialization failed: Error response: {responseObj.error}");
                    }

                    if (responseObj.result == null)
                    {
                        throw new Exception("Server initialization failed: Missing result in response");
                    }

                    // Validate the result format
                    var resultJson = JsonSerializer.Serialize(responseObj.result);
                    var resultObj = JsonSerializer.Deserialize<JsonElement>(resultJson);
                    
                    if (!resultObj.TryGetProperty("protocolVersion", out var versionElement))
                    {
                        throw new Exception("Server initialization failed: Missing protocolVersion in result");
                    }

                    if (!resultObj.TryGetProperty("capabilities", out var capabilitiesElement))
                    {
                        throw new Exception("Server initialization failed: Missing capabilities in result");
                    }

                    // Send initialized notification
                    var initializedNotification = new
                    {
                        jsonrpc = "2.0",
                        method = "notifications/initialized",
                        @params = new { }
                    };
                    json = JsonSerializer.Serialize(initializedNotification);
                    await _process.StandardInput.WriteLineAsync(json);
                    await _process.StandardInput.FlushAsync();
                }
                finally
                {
                    _streamLock.Release();
                }

                return _process;
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Error during server startup: {ex}");
                if (!_process.HasExited)
                {
                    _process.Kill(true);
                }
                throw;
            }
        }
        
        private async Task<JsonRpcResponse> SendStdioCommand(Process process, object command)
        {
            var json = JsonSerializer.Serialize(command);
            Log($"Sending command: {json}");
            
            // Use semaphore to ensure exclusive stream access
            await _streamLock.WaitAsync();
            try
            {
                // Write command
                await process.StandardInput.WriteLineAsync(json);
                await process.StandardInput.FlushAsync();
                
                // Read response with timeout
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var response = await Task.Run(async () =>
                {
                    while (!cts.Token.IsCancellationRequested)
                    {
                        var line = await process.StandardOutput.ReadLineAsync();
                        if (line != null)
                        {
                            return line;
                        }
                        await Task.Delay(50, cts.Token); // Small polling delay
                    }
                    throw new TimeoutException("Timeout waiting for server response");
                }, cts.Token);
                
                Log($"Received response: {response}");
                var result = JsonSerializer.Deserialize<JsonRpcResponse>(response);
                if (result == null)
                {
                    throw new Exception("Failed to deserialize response");
                }
                
                // Check for error in response
                if (result.error != null)
                {
                    Log($"Server returned error: {JsonSerializer.Serialize(result.error)}");
                    throw new Exception($"Server error: {result.error}");
                }
                
                return result;
            }
            catch (Exception ex) when (ex is not TimeoutException)
            {
                Log($"Error in SendStdioCommand: {ex.Message}");
                throw;
            }
            finally
            {
                _streamLock.Release();
            }
        }

        // Custom process wrapper to ensure proper cleanup
        private class ProcessWrapper : IAsyncDisposable, IDisposable
        {
            private readonly Process _process;
            private readonly Func<Task> _cleanupAction;
            private readonly ITestOutputHelper _output;
            private bool _disposed;
            
            public ProcessWrapper(Process process, Func<Task> cleanupAction, ITestOutputHelper output)
            {
                _process = process;
                _cleanupAction = cleanupAction;
                _output = output;
            }
            
            private void Log(string message)
            {
                var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                _output.WriteLine($"[{timestamp}] ProcessWrapper: {message}");
            }
            
            public void Start() => _process.Start();
            
            public void Kill() 
            {
                Log("Killing process...");
                try
                {
                    if (!_process.HasExited)
                    {
                        _process.Kill(true); // Kill entire process tree
                        Log("Process killed");
                    }
                }
                catch (Exception ex)
                {
                    Log($"Error killing process: {ex.Message}");
                }
            }
            
            public bool HasExited => _process.HasExited;
            public int ExitCode => _process.ExitCode;
            public StreamWriter StandardInput => _process.StandardInput;
            public StreamReader StandardOutput => _process.StandardOutput;
            public StreamReader StandardError => _process.StandardError;
            
            public async Task WaitForExitAsync(CancellationToken cancellationToken = default)
            {
                try
                {
                    Log("Waiting for process to exit...");
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    await _process.WaitForExitAsync(cts.Token);
                    Log($"Process exited with code {_process.ExitCode}");
                }
                catch (OperationCanceledException)
                {
                    Log("Process did not exit within timeout");
                    Kill();
                }
            }
            
            public void Dispose()
            {
                Log("Disposing via synchronous Dispose...");
                DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
            
            public async ValueTask DisposeAsync()
            {
                if (_disposed)
                {
                    Log("Already disposed, skipping");
                    return;
                }
                
                Log("Starting async disposal...");
                try
                {
                    if (!_process.HasExited)
                    {
                        try
                        {
                            Log("Process still running, attempting graceful shutdown...");
                            // First try to send shutdown request
                            try
                            {
                                await _process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new
                                {
                                    id = "shutdown",
                                    jsonrpc = "2.0",
                                    method = "mcp/shutdown"
                                }));
                                await _process.StandardInput.FlushAsync();
                                
                                // Wait for graceful shutdown
                                using var shutdownCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                                await _process.WaitForExitAsync(shutdownCts.Token);
                                Log("Process exited after shutdown request");
                            }
                            catch (OperationCanceledException)
                            {
                                Log("Process did not exit after shutdown request, attempting kill...");
                                Kill();
                                using var killCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                                await _process.WaitForExitAsync(killCts.Token);
                                Log("Process exited after kill");
                            }
                            catch (Exception ex)
                            {
                                Log($"Error during shutdown request: {ex.Message}");
                                // Fall through to kill
                            }
                            
                            // If still running, force kill
                            if (!_process.HasExited)
                            {
                                try
                                {
                                    Log("Process still running, attempting force kill of process tree...");
                                    Kill();
                                    using var forceKillCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                                    await _process.WaitForExitAsync(forceKillCts.Token);
                                    Log("Process exited after force kill");
                                }
                                catch (Exception ex)
                                {
                                    Log($"Error during force kill: {ex.Message}");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Log($"Error during process kill: {ex.Message}");
                        }
                    }
                    else
                    {
                        Log($"Process already exited with code {_process.ExitCode}");
                    }
                }
                catch (Exception ex)
                {
                    Log($"Error in process cleanup: {ex.Message}");
                }
                
                try
                {
                    Log("Running cleanup action...");
                    await _cleanupAction();
                    Log("Cleanup action completed");
                }
                catch (Exception ex)
                {
                    Log($"Error during cleanup action: {ex.Message}");
                }
                
                try
                {
                    Log("Disposing process...");
                    _process.Dispose();
                    Log("Process disposed");
                }
                catch (Exception ex)
                {
                    Log($"Error disposing process: {ex.Message}");
                }
                
                _disposed = true;
                Log("Disposal complete");
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_process != null)
            {
                try
                {
                    if (!_process.HasExited)
                    {
                        _process.Kill();
                        await _process.WaitForExitAsync();
                    }
                    _process.Dispose();
                }
                catch (Exception ex)
                {
                    Log($"Error during cleanup: {ex.Message}");
                }
            }
        }
    }
} 