using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.FSharp.Core;
using Microsoft.FSharp.Collections;
using static DrawIO.MCP.STDIO.FileOperations;

// Add a type alias for easier reference to F# types
using FSharpTypes = DrawIO.MCP.Core.Types;

namespace DrawIO.MCP.STDIO
{
    /// <summary>
    /// Handles execution of diagram manipulation tools
    /// </summary>
    public static class DiagramToolExecutor
    {
        /// <summary>
        /// Execute a tool with the given name and arguments
        /// </summary>
        public static async Task<object> ExecuteToolAsync(string toolName, JsonElement arguments, string diagramsDirectory, TextWriter logWriter, bool verbose)
        {
            LogMessage(logWriter, verbose, $"Executing tool: {toolName}");
            
            try
            {
                // Handle update_shape_style separately since it has issues
                if (toolName == "update_shape_style")
                {
                    return await UpdateShapeWithStyleAsync(arguments, diagramsDirectory);
                }
                
                // Execute the tool and get the result
                object result = toolName switch
                {
                    "create_new_diagram" => await CreateNewDiagramAsync(arguments, diagramsDirectory),
                    "add_shape" => await AddShapeAsync(arguments, diagramsDirectory),
                    "connect_shapes" => await ConnectShapesAsync(arguments, diagramsDirectory),
                    "generate_vpc" => await GenerateVpcAsync(arguments, diagramsDirectory),
                    "get_diagram_image" => await GetDiagramImageAsync(arguments, diagramsDirectory, logWriter, verbose),
                    "delete_shape" => await DeleteShapeAsync(arguments, diagramsDirectory),
                    "update_shape" => await UpdateShapeAsync(arguments, diagramsDirectory),
                    "style_shape" => await StyleShapeAsync(arguments, diagramsDirectory),
                    "arrange_diagram" => await ArrangeDiagramAsync(arguments, diagramsDirectory),
                    "move_shape" => await MoveShapeAsync(arguments, diagramsDirectory),
                    "create_diagram_page" => await CreateDiagramPageAsync(arguments, diagramsDirectory),
                    "get_diagram_page" => await GetDiagramPageAsync(arguments, diagramsDirectory),
                    "update_diagram_page" => await UpdateDiagramPageAsync(arguments, diagramsDirectory),
                    "delete_diagram_page" => await DeleteDiagramPageAsync(arguments, diagramsDirectory),
                    "move_cell_between_pages" => await MoveCellBetweenPagesAsync(arguments, diagramsDirectory),
                    "find_elements_by_text" => await FindElementsByTextAsync(arguments, diagramsDirectory),
                    "get_element_info" => await GetElementInfoAsync(arguments, diagramsDirectory),
                    "list_neighbors" => await ListNeighborsAsync(arguments, diagramsDirectory),
                    "get_diagram_bounds" => await GetDiagramBoundsAsync(arguments, diagramsDirectory),
                    "resize_shape" => await ResizeShapeAsync(arguments, diagramsDirectory),
                    "set_text_style" => await SetTextStyleAsync(arguments, diagramsDirectory),
                    "set_line_style" => await SetLineStyleAsync(arguments, diagramsDirectory),
                    "set_arrow_style" => await SetArrowStyleAsync(arguments, diagramsDirectory),
                    "reset_connector" => await ResetConnectorAsync(arguments, diagramsDirectory),
                    "reverse_connector" => await ReverseConnectorAsync(arguments, diagramsDirectory),
                    "add_waypoint" => await AddWaypointAsync(arguments, diagramsDirectory),
                    "remove_waypoint" => await RemoveWaypointAsync(arguments, diagramsDirectory),
                    "update_waypoint" => await UpdateWaypointAsync(arguments, diagramsDirectory),
                    "get_waypoints" => await GetWaypointsAsync(arguments, diagramsDirectory),
                    "clear_waypoints" => await ClearWaypointsAsync(arguments, diagramsDirectory),
                    "group_shapes" => await GroupShapesAsync(arguments, diagramsDirectory),
                    "ungroup_shapes" => await UngroupShapesAsync(arguments, diagramsDirectory),
                    "rotate_shape" => await RotateShapeAsync(arguments, diagramsDirectory, verbose, logWriter),
                    "flip_shape" => await FlipShapeAsync(arguments, diagramsDirectory, verbose, logWriter),
                    "set_diagram_background" => await SetDiagramBackgroundAsync(arguments, diagramsDirectory, logWriter, verbose),
                    "connect_shapes_at_points" => await ConnectShapesAtPointsAsync(arguments, diagramsDirectory, logWriter, verbose),
                    "list_shape_types" => await McpToolHandlers.ListShapeTypesAsync(arguments, diagramsDirectory, logWriter, verbose),
                    _ => throw new ArgumentException($"Unknown tool: {toolName}")
                };

                // Check if the client has requested the diagram image to be included in the response
                bool returnDiagram = arguments.TryGetProperty("return_diagram", out var returnDiagramElement) && 
                                    returnDiagramElement.ValueKind == JsonValueKind.True;
                
                // If not the get_diagram_image tool and return_diagram is true, append the diagram image
                if (returnDiagram && toolName != "get_diagram_image" && arguments.TryGetProperty("diagram", out var diagramElement))
                {
                    string diagramName = diagramElement.GetString();
                    int page = arguments.TryGetProperty("page_index", out var pageElement) ? 
                              pageElement.GetInt32() : 
                              (arguments.TryGetProperty("page", out var altPageElement) ? altPageElement.GetInt32() : 0);
                    
                    // Get the diagram image
                    var diagramImage = await GetDiagramImageForResponseAsync(diagramName, page, "png", diagramsDirectory, logWriter, verbose);
                    
                    // Convert result to Dictionary if it's not already
                    var resultDict = ConvertToDictionary(result) as Dictionary<string, object>;
                    if (resultDict != null)
                    {
                        // Append the diagram image to the content
                        if (resultDict.TryGetValue("content", out var contentObj) && contentObj is IEnumerable<object> contentList)
                        {
                            // Convert to list to be able to modify
                            var content = contentList.ToList();
                            
                            // Add the image to the content
                            if (diagramImage != null)
                            {
                                content.Add(diagramImage);
                                
                                // Update the content in the result
                                resultDict["content"] = content;
                            }
                        }
                        else if (diagramImage != null)
                        {
                            // Create new content list with the message
                            resultDict["content"] = new List<object> { diagramImage };
                        }
                        
                        // Return the updated result
                        return resultDict;
                    }
                }

                return ConvertToDictionary(result);
            }
            catch (Exception ex)
            {
                LogMessage(logWriter, true, $"Error executing tool {toolName}: {ex.Message}");
                // Return an error object as Dictionary<string, object>
                return new Dictionary<string, object>
                {
                    ["error"] = ex.Message,
                    ["detail"] = ex.ToString(),
                    ["content"] = new object[] { } // Add empty content array to satisfy MCP protocol
                };
            }
        }

        // Helper method to generate diagram image for responses
        private static async Task<object> GetDiagramImageForResponseAsync(string diagramName, int page, string format, string diagramsDirectory, TextWriter logWriter, bool verbose)
        {
            try
            {
                string filePath = Path.Combine(diagramsDirectory, diagramName);
                if (!File.Exists(filePath))
                {
                    LogMessage(logWriter, verbose, $"Diagram file not found for image generation: {diagramName}");
                    return null;
                }
                
                // Check if drawio CLI is available
                if (!CheckDrawIoCliAvailable(logWriter, verbose))
                {
                    LogMessage(logWriter, verbose, "DrawIO CLI not available for image generation");
                    return null;
                }
                
                // Create a temporary file to store the output image
                string tempFileName = $"{Path.GetFileNameWithoutExtension(diagramName)}_{page}_{DateTime.Now:yyyyMMddHHmmss}.{format}";
                string outputImagePath = Path.Combine(diagramsDirectory, tempFileName);
                
                // Build the drawio CLI command with proper escaping
                string drawioCommand = $"drawio --export --format {format} --page-index {page} --transparent --scale 1.0 --border 0 --output \"{outputImagePath}\" \"{filePath}\"";
                
                // Try bash first
                var bashStartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "bash",
                    Arguments = $"-c \"{drawioCommand}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                LogMessage(logWriter, verbose, $"Attempting to export with bash for response: {bashStartInfo.FileName} {bashStartInfo.Arguments}");
                
                bool exportSuccess = false;
                
                try
                {
                    using var bashProcess = System.Diagnostics.Process.Start(bashStartInfo);
                    if (bashProcess != null)
                    {
                        await bashProcess.WaitForExitAsync();
                        if (bashProcess.ExitCode == 0)
                        {
                            exportSuccess = true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogMessage(logWriter, verbose, $"Bash export failed for response: {ex.Message}");
                }

                // If bash failed, try direct drawio call
                if (!exportSuccess)
                {
                    LogMessage(logWriter, verbose, "Falling back to direct drawio CLI call...");
                    var processStartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "drawio",
                        Arguments = $"--export --format {format} --page-index {page} --transparent --scale 1.0 --border 0 --output \"{outputImagePath}\" \"{filePath}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    LogMessage(logWriter, verbose, $"Executing command for response: {processStartInfo.FileName} {processStartInfo.Arguments}");
                    
                    using var process = System.Diagnostics.Process.Start(processStartInfo);
                    if (process != null)
                    {
                        await process.WaitForExitAsync();
                        if (process.ExitCode == 0)
                        {
                            exportSuccess = true;
                        }
                    }
                }

                if (!exportSuccess || !File.Exists(outputImagePath))
                {
                    LogMessage(logWriter, verbose, $"Failed to generate diagram image for response");
                    return null;
                }
                
                // Read the generated image and convert it to base64
                byte[] imageBytes = await File.ReadAllBytesAsync(outputImagePath);
                string base64Image = Convert.ToBase64String(imageBytes);
                
                // Clean up the temporary file
                try
                {
                    File.Delete(outputImagePath);
                }
                catch (Exception ex)
                {
                    LogMessage(logWriter, verbose, $"Failed to delete temporary file {outputImagePath}: {ex.Message}");
                }
                
                return new Dictionary<string, object>
                {
                    ["type"] = "image",
                    ["data"] = base64Image,
                    ["mimeType"] = $"image/{format.ToLower()}"
                };
            }
            catch (Exception ex)
            {
                LogMessage(logWriter, true, $"Error generating diagram image for response: {ex.Message}");
                return null;
            }
        }

        private static object ConvertToDictionary(object obj)
        {
            if (obj == null) return null;

            // If it's already a dictionary, return as is
            if (obj is Dictionary<string, object>) return obj;

            var type = obj.GetType();

            // Handle arrays and lists
            if (type.IsArray || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)))
            {
                var list = ((IEnumerable<object>)obj).Cast<object>().Select(item => ConvertToDictionary(item)).ToList();
                return list;
            }

            // If it's an anonymous type or a complex object, convert to dictionary
            if (type.Name.StartsWith("<>f__AnonymousType") || (!type.IsPrimitive && type != typeof(string)))
            {
                var dict = new Dictionary<string, object>();
                foreach (var prop in type.GetProperties())
                {
                    var value = prop.GetValue(obj);
                    // Preserve original casing of property names
                    dict[prop.Name] = ConvertToDictionary(value);
                }
                return dict;
            }

            // Return primitive types and strings as is
            return obj;
        }

        private static void LogMessage(TextWriter logWriter, bool verbose, string message)
        {
            if (verbose)
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                logWriter.WriteLine($"[{timestamp}] TOOL: {message}");
            }
        }

        // Tool implementation methods

        private static Task<object> CreateNewDiagramAsync(JsonElement parameters, string diagramsDirectory)
        {
            string name = GetParameterString(parameters, "name");
            
            if (string.IsNullOrEmpty(Path.GetExtension(name)))
            {
                name += ".drawio";
            }
            
            string filePath = Path.Combine(diagramsDirectory, name);
            
            // Create the diagrams directory if it doesn't exist
            if (!Directory.Exists(diagramsDirectory))
            {
                Directory.CreateDirectory(diagramsDirectory);
            }
            
            // Create a new diagram using the Core library
            var diagram = CreateNewDiagram(filePath);
            
            return Task.FromResult<object>(new 
            {
                status = "success",
                DiagramId = $"diagram://{name}",
                FileName = name,
                content = new[] 
                { 
                    new 
                    { 
                        type = "text", 
                        text = $"Created new diagram: {name}" 
                    } 
                }
            });
        }

        private static Task<object> AddShapeAsync(JsonElement parameters, string diagramsDirectory)
        {
            try
            {
                // Extract parameters
                var diagramName = GetParameterString(parameters, "diagram");
                var value = GetParameterString(parameters, "value");
                var x = parameters.GetProperty("x").GetDouble();
                var y = parameters.GetProperty("y").GetDouble();
                var width = parameters.GetProperty("width").GetDouble();
                var height = parameters.GetProperty("height").GetDouble();
                var shape = parameters.TryGetProperty("shape", out var shapeElement) ? shapeElement.GetString() : "rectangle";
                
                // Load diagram
                var diagramPath = Path.Combine(diagramsDirectory, diagramName);
                var diagramObj = DrawIO.MCP.Core.FileOperations.loadDiagram(diagramPath);
                
                // Add shape using the new ShapeLibrary.addShapeByType function for more flexibility with shape types
                var (updatedDiagram, shapeId) = DrawIO.MCP.Core.ShapeLibrary.addShapeByType(diagramObj, 0, value, shape, x, y, width, height);
                
                // Save updated diagram
                DrawIO.MCP.Core.FileOperations.saveDiagram(updatedDiagram, diagramPath);
                
                return Task.FromResult<object>(new 
                { 
                    status = "success",
                    elementId = shapeId,
                    content = new[] 
                    { 
                        new 
                        { 
                            type = "text", 
                            text = $"Added shape with ID {shapeId}" 
                        } 
                    }
                });
            }
            catch (Exception ex)
            {
                return Task.FromResult<object>(new 
                { 
                    status = "error", 
                    message = ex.Message,
                    content = new[]
                    {
                        new
                        {
                            type = "text",
                            text = $"Error adding shape: {ex.Message}"
                        }
                    }
                });
            }
        }

        private static Task<object> ConnectShapesAsync(JsonElement parameters, string diagramsDirectory)
        {
            try
            {
                // Extract parameters
                var diagramName = GetParameterString(parameters, "diagram");
                var sourceId = GetParameterString(parameters, "source_id");
                var targetId = GetParameterString(parameters, "target_id");

                // Load diagram
                var diagramPath = Path.Combine(diagramsDirectory, diagramName);
                var diagramObj = DrawIO.MCP.Core.FileOperations.loadDiagram(diagramPath);

                // Connect shapes
                var (updatedDiagram, connectorId) = DrawIO.MCP.Core.DiagramManipulation.connectShapes(diagramObj, 0, sourceId, targetId);

                // Save updated diagram
                DrawIO.MCP.Core.FileOperations.saveDiagram(updatedDiagram, diagramPath);

                return Task.FromResult<object>(new 
                { 
                    status = "success",
                    elementId = connectorId,
                    content = new[] 
                    { 
                        new 
                        { 
                            type = "text", 
                            text = $"Connected shapes with connector ID {connectorId}" 
                        } 
                    }
                });
            }
            catch (Exception ex)
            {
                return Task.FromResult<object>(new
                {
                    isError = true,
                    error = ex.Message,
                    content = new[]
                    {
                        new
                        {
                            type = "text",
                            text = $"Error: {ex.Message}"
                        }
                    }
                });
            }
        }

        private static Task<object> GenerateVpcAsync(JsonElement parameters, string diagramsDirectory)
        {
            try
            {
                var diagramName = parameters.GetProperty("diagram_name").GetString();
                var diagramPath = Path.Combine(diagramsDirectory, diagramName);

                // Create empty diagram
                var emptyDiagram = DrawIO.MCP.Core.DiagramManipulation.createEmptyDiagram();

                // Add VPC
                var (vpcDiagram, vpcId) = DrawIO.MCP.Core.DiagramManipulation.addShape(emptyDiagram, 0, "VPC", 50, 50, 600, 400, "swimlane");

                // Add IGW
                var (igwDiagram, igwId) = DrawIO.MCP.Core.DiagramManipulation.addShape(vpcDiagram, 0, "IGW", 350, 10, 80, 40, "rectangle");

                // Connect components
                var (diagramWithConnector1, _) = DrawIO.MCP.Core.DiagramManipulation.connectShapes(igwDiagram, 0, igwId, vpcId);
                var (diagramWithConnector2, _) = DrawIO.MCP.Core.DiagramManipulation.connectShapes(diagramWithConnector1, 0, vpcId, vpcId);
                var (finalDiagram, _) = DrawIO.MCP.Core.DiagramManipulation.connectShapes(diagramWithConnector2, 0, vpcId, vpcId);

                // Save the diagram
                DrawIO.MCP.Core.FileOperations.saveDiagram(finalDiagram, diagramPath);

                return Task.FromResult<object>(new { status = "success" });
            }
            catch (Exception ex)
            {
                return Task.FromException<object>(ex);
            }
        }
        
        private static Task<object> DeleteShapeAsync(JsonElement parameters, string diagramsDirectory)
        {
            string diagram = GetParameterString(parameters, "diagram");
            
            // Try both naming conventions for parameters
            string shapeId;
            if (parameters.TryGetProperty("shapeId", out var shapeIdElement))
            {
                shapeId = shapeIdElement.GetString();
            }
            else
            {
                shapeId = GetParameterString(parameters, "shape_id");
            }
            
            string filePath = Path.Combine(diagramsDirectory, diagram);
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Diagram file not found: {diagram}");
            }
            
            // Load the diagram, delete the shape, and save it
            var diagramObj = LoadDiagram(filePath);
            var updatedDiagram = DrawIO.MCP.Core.DiagramManipulation.deleteShape(diagramObj, 0, shapeId);
            SaveDiagram(updatedDiagram, filePath);
            
            return Task.FromResult<object>(new
            {
                status = "success",
                DiagramId = $"diagram://{diagram}",
                content = new[] 
                { 
                    new 
                    { 
                        type = "text", 
                        text = $"Deleted shape with ID {shapeId}" 
                    } 
                }
            });
        }

        private static Task<object> UpdateShapeAsync(JsonElement parameters, string diagramsDirectory)
        {
            try
            {
                string diagram = GetParameterString(parameters, "diagram");
                
                // Try both naming conventions for parameters
                string shapeId;
                if (parameters.TryGetProperty("shapeId", out var shapeIdElement))
                {
                    shapeId = shapeIdElement.GetString();
                }
                else
                {
                    shapeId = GetParameterString(parameters, "shape_id");
                }
                
                // Handle null value properly - set to empty string if not provided
                string value = "";
                if (parameters.TryGetProperty("value", out var valueElement))
                {
                    value = valueElement.GetString() ?? "";
                }
                
                // Convert nullable parameters to F# options
                Microsoft.FSharp.Core.FSharpOption<double> x = null;
                Microsoft.FSharp.Core.FSharpOption<double> y = null;
                Microsoft.FSharp.Core.FSharpOption<double> width = null;
                Microsoft.FSharp.Core.FSharpOption<double> height = null;
                Microsoft.FSharp.Core.FSharpOption<string> style = null;
                
                if (parameters.TryGetProperty("x", out var xElement))
                {
                    x = Microsoft.FSharp.Core.FSharpOption<double>.Some(xElement.GetDouble());
                }
                
                if (parameters.TryGetProperty("y", out var yElement))
                {
                    y = Microsoft.FSharp.Core.FSharpOption<double>.Some(yElement.GetDouble());
                }
                
                if (parameters.TryGetProperty("width", out var widthElement))
                {
                    width = Microsoft.FSharp.Core.FSharpOption<double>.Some(widthElement.GetDouble());
                }
                
                if (parameters.TryGetProperty("height", out var heightElement))
                {
                    height = Microsoft.FSharp.Core.FSharpOption<double>.Some(heightElement.GetDouble());
                }
                
                if (parameters.TryGetProperty("style", out var styleElement))
                {
                    style = Microsoft.FSharp.Core.FSharpOption<string>.Some(styleElement.GetString());
                }
                
                string filePath = Path.Combine(diagramsDirectory, diagram);
                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException($"Diagram file not found: {diagram}");
                }
                
                // Load the diagram, update the shape, and save it
                var diagramObj = LoadDiagram(filePath);
                var updatedDiagram = DrawIO.MCP.Core.DiagramManipulation.updateShape(
                    diagramObj, 
                    0, // page index
                    shapeId,
                    value,
                    x,
                    y,
                    width,
                    height,
                    style
                );
                SaveDiagram(updatedDiagram, filePath);
                
                return Task.FromResult<object>(new 
                {
                    status = "success",
                    DiagramId = $"diagram://{diagram}",
                    content = new[] 
                    { 
                        new 
                        { 
                            type = "text", 
                            text = $"Updated shape with ID {shapeId}" 
                        } 
                    }
                });
            }
            catch (Exception ex)
            {
                return Task.FromResult<object>(new 
                { 
                    status = "error",
                    error = ex.Message,
                    detail = ex.ToString(),
                    content = new[] 
                    { 
                        new 
                        { 
                            type = "text", 
                            text = $"Error updating shape: {ex.Message}" 
                        } 
                    }
                });
            }
        }

        private static Task<object> StyleShapeAsync(JsonElement parameters, string diagramsDirectory)
        {
            string diagram = GetParameterString(parameters, "diagram");
            
            // Try both naming conventions for parameters
            string shapeId;
            if (parameters.TryGetProperty("shapeId", out var shapeIdElement))
            {
                shapeId = shapeIdElement.GetString();
            }
            else
            {
                shapeId = GetParameterString(parameters, "shape_id");
            }
            
            string fillColor = null;
            string strokeColor = null;
            
            if (parameters.TryGetProperty("fill_color", out var fillColorElement))
            {
                fillColor = fillColorElement.GetString();
            }
            else if (parameters.TryGetProperty("fillColor", out fillColorElement))
            {
                fillColor = fillColorElement.GetString();
            }
            
            if (parameters.TryGetProperty("stroke_color", out var strokeColorElement))
            {
                strokeColor = strokeColorElement.GetString();
            }
            else if (parameters.TryGetProperty("strokeColor", out strokeColorElement))
            {
                strokeColor = strokeColorElement.GetString();
            }
            
            string filePath = Path.Combine(diagramsDirectory, diagram);
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Diagram file not found: {diagram}");
            }
            
            // Build style string
            var styleBuilder = new System.Text.StringBuilder();
            
            if (!string.IsNullOrEmpty(fillColor))
            {
                styleBuilder.Append($"fillColor={fillColor};");
            }
            
            if (!string.IsNullOrEmpty(strokeColor))
            {
                styleBuilder.Append($"strokeColor={strokeColor};");
            }
            
            string styleString = styleBuilder.ToString();
            
            if (string.IsNullOrEmpty(styleString))
            {
                throw new ArgumentException("At least one style property (fill_color, stroke_color) must be provided");
            }
            
            // Load the diagram, update style, and save it
            var diagramObj = LoadDiagram(filePath);
            var updatedDiagram = DrawIO.MCP.Core.DiagramManipulation.updateShape(diagramObj, 0, shapeId, null, null, null, null, null, styleString);
            SaveDiagram(updatedDiagram, filePath);
            
            return Task.FromResult<object>(new 
            {
                status = "success",
                DiagramId = $"diagram://{diagram}",
                Style = styleString,
                content = new[] 
                { 
                    new 
                    { 
                        type = "text", 
                        text = $"Applied style {styleString} to shape {shapeId}" 
                    } 
                }
            });
        }

        private static Task<object> ArrangeDiagramAsync(JsonElement parameters, string diagramsDirectory)
        {
            string diagram = GetParameterString(parameters, "diagram");
            string layout = GetParameterString(parameters, "layout", "grid");
            
            string filePath = Path.Combine(diagramsDirectory, diagram);
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Diagram file not found: {diagram}");
            }
            
            // Load the diagram
            var loadedDiagram = LoadDiagram(filePath);
            
            // Get optional page_id parameter
            string pageId = null;
            if (parameters.TryGetProperty("page_id", out var pageIdElement))
            {
                pageId = pageIdElement.GetString();
            }
            
            // Create the FSharpOption for page ID
            var pageIdOption = string.IsNullOrEmpty(pageId) 
                ? FSharpOption<string>.None 
                : FSharpOption<string>.Some(pageId);
            
            // Arrange the diagram
            var updatedDiagram = DrawIO.MCP.Core.DiagramManipulation.arrangeDiagram(loadedDiagram, pageIdOption);
            
            // Save the updated diagram
            SaveDiagram(updatedDiagram, filePath);
            
            return Task.FromResult<object>(new
            {
                status = "success",
                message = $"Diagram arranged using layout: {layout}",
                content = new[] 
                { 
                    new 
                    { 
                        type = "text", 
                        text = $"Diagram arranged using layout: {layout}" 
                    } 
                }
            });
        }

        private static Task<object> MoveShapeAsync(JsonElement parameters, string diagramsDirectory)
        {
            string diagram = GetParameterString(parameters, "diagram");
            string shape_id = GetParameterString(parameters, "shape_id");
            float x = GetParameterFloat(parameters, "x");
            float y = GetParameterFloat(parameters, "y");
            
            string filePath = Path.Combine(diagramsDirectory, diagram);
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Diagram file not found: {diagram}");
            }
            
            // Load the diagram
            var loadedDiagram = LoadDiagram(filePath);
            
            // Move the shape
            var updatedDiagram = DrawIO.MCP.Core.DiagramManipulation.moveShape(loadedDiagram, shape_id, x, y);
            
            // Save the updated diagram
            SaveDiagram(updatedDiagram, filePath);
            
            return Task.FromResult<object>(new 
            {
                status = "success",
                message = $"Shape {shape_id} moved to position ({x}, {y})",
                content = new[] 
                { 
                    new 
                    { 
                        type = "text", 
                        text = $"Shape {shape_id} moved to position ({x}, {y})" 
                    } 
                }
            });
        }
        
        private static Task<object> CreateDiagramPageAsync(JsonElement parameters, string diagramsDirectory)
        {
            string diagram = GetParameterString(parameters, "diagram");
            string name = GetParameterString(parameters, "name");
            
            string filePath = Path.Combine(diagramsDirectory, diagram);
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Diagram file not found: {diagram}");
            }
            
            // Load the diagram
            var loadedDiagram = LoadDiagram(filePath);
            
            // Create a new page
            var updatedDiagram = DrawIO.MCP.Core.FileOperations.createDiagramPage(loadedDiagram, name);
            
            // Save the updated diagram
            SaveDiagram(updatedDiagram, filePath);
            
            // Get the new page ID (last page in the list)
            string pageId = updatedDiagram.Pages[updatedDiagram.Pages.Length - 1].Id;
            
            return Task.FromResult<object>(new
            {
                status = "success",
                page_id = pageId,
                message = $"Created new page '{name}' with ID {pageId}",
                content = new[] 
                { 
                    new 
                    { 
                        type = "text", 
                        text = $"Created new page '{name}' with ID {pageId}" 
                    } 
                }
            });
        }
        
        private static Task<object> GetDiagramPageAsync(JsonElement parameters, string diagramsDirectory)
        {
            string diagram = GetParameterString(parameters, "diagram");
            int page_index = GetParameterInt(parameters, "page_index");
            
            string filePath = Path.Combine(diagramsDirectory, diagram);
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Diagram file not found: {diagram}");
            }
            
            // Load the diagram
            var loadedDiagram = LoadDiagram(filePath);
            
            // Get the page
            var pageOption = FSharpOption<FSharpTypes.Page>.None;
            
            if (page_index >= 0 && page_index < loadedDiagram.Pages.Length)
            {
                pageOption = FSharpOption<FSharpTypes.Page>.Some(loadedDiagram.Pages[page_index]);
            }
            
            if (pageOption.IsNone())
            {
                throw new ArgumentException($"Page at index {page_index} not found");
            }
            
            var page = pageOption.Value;
            
            // Convert cells to a format suitable for JSON serialization
            var cells = new List<Dictionary<string, object>>();
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
                
                if (cell.Geometry.IsSome())
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
            
            return Task.FromResult<object>(new
            {
                status = "success",
                page = new
                {
                    id = page.Id,
                    name = page.Name,
                    cells = cells
                },
                content = new[] 
                { 
                    new 
                    { 
                        type = "text", 
                        text = $"Page '{page.Name}' with ID {page.Id} has {cells.Count} cells" 
                    } 
                }
            });
        }
        
        private static Task<object> UpdateDiagramPageAsync(JsonElement parameters, string diagramsDirectory)
        {
            string diagram = GetParameterString(parameters, "diagram");
            int pageIndex = GetParameterInt(parameters, "page_index");
            
            string filePath = Path.Combine(diagramsDirectory, diagram);
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Diagram file not found: {diagram}");
            }
            
            // Load the diagram
            var loadedDiagram = LoadDiagram(filePath);
            
            // Validate page index
            if (pageIndex < 0 || pageIndex >= loadedDiagram.Pages.Length)
            {
                throw new ArgumentException($"Page index {pageIndex} is out of range");
            }
            
            // Get the page ID from the index
            string pageId = loadedDiagram.Pages[pageIndex].Id;
            
            // Get the optional name parameter
            string name = null;
            if (parameters.TryGetProperty("name", out var nameElement))
            {
                name = nameElement.GetString();
            }
            
            // Update the page
            var updatedDiagram = DrawIO.MCP.Core.FileOperations.updateDiagramPage(loadedDiagram, pageId, 
                string.IsNullOrEmpty(name) ? FSharpOption<string>.None : FSharpOption<string>.Some(name));
            
            // Save the updated diagram
            SaveDiagram(updatedDiagram, filePath);
            
            return Task.FromResult<object>(new
            {
                status = "success",
                message = $"Page {pageId} updated",
                content = new[] 
                { 
                    new 
                    { 
                        type = "text", 
                        text = $"Page {pageId} updated" + (name != null ? $" with new name '{name}'" : "") 
                    } 
                }
            });
        }
        
        private static Task<object> DeleteDiagramPageAsync(JsonElement parameters, string diagramsDirectory)
        {
            string diagram = GetParameterString(parameters, "diagram");
            string pageId = GetParameterString(parameters, "page_id");
            
            string filePath = Path.Combine(diagramsDirectory, diagram);
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Diagram file not found: {diagram}");
            }
            
            // Load the diagram
            var loadedDiagram = LoadDiagram(filePath);
            
            // Delete the page
            var updatedDiagram = DrawIO.MCP.Core.FileOperations.deleteDiagramPage(loadedDiagram, pageId);
            
            // Check if the diagram was modified (the page was deleted)
            bool deleted = updatedDiagram.Pages.Length < loadedDiagram.Pages.Length;
            
            if (deleted)
            {
                // Save the updated diagram
                SaveDiagram(updatedDiagram, filePath);
            }
            
            string message = deleted ? $"Page {pageId} deleted" : "Page could not be deleted (may be the only page)";
            
            return Task.FromResult<object>(new
            {
                status = deleted ? "success" : "error",
                message = message,
                content = new[] 
                { 
                    new 
                    { 
                        type = "text", 
                        text = message
                    } 
                }
            });
        }
        
        private static Task<object> MoveCellBetweenPagesAsync(JsonElement parameters, string diagramsDirectory)
        {
            string diagram = GetParameterString(parameters, "diagram");
            string cellId = GetParameterString(parameters, "cell_id");
            string sourcePageId = GetParameterString(parameters, "source_page_id");
            string targetPageId = GetParameterString(parameters, "target_page_id");
            
            string filePath = Path.Combine(diagramsDirectory, diagram);
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Diagram file not found: {diagram}");
            }
            
            // Load the diagram
            var loadedDiagram = LoadDiagram(filePath);
            
            // Move the cell between pages
            var updatedDiagram = DrawIO.MCP.Core.FileOperations.moveCellBetweenPages(loadedDiagram, cellId, sourcePageId, targetPageId);
            
            // Save the updated diagram
            SaveDiagram(updatedDiagram, filePath);
            
            string message = $"Cell {cellId} moved from page {sourcePageId} to page {targetPageId}";
            
            return Task.FromResult<object>(new
            {
                status = "success",
                message = message,
                content = new[] 
                { 
                    new 
                    { 
                        type = "text", 
                        text = message
                    } 
                }
            });
        }

        private static async Task<object> GetDiagramImageAsync(JsonElement parameters, string diagramsDirectory, TextWriter logWriter, bool verbose)
        {
            string diagram = GetParameterString(parameters, "diagram");
            int page = parameters.TryGetProperty("page", out var pageElement) ? pageElement.GetInt32() : 0;
            string format = parameters.TryGetProperty("format", out var formatElement) ? formatElement.GetString() : "png";
            
            string filePath = Path.Combine(diagramsDirectory, diagram);
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Diagram file not found: {diagram}");
            }
            
            LogMessage(logWriter, verbose, $"Getting diagram image for {diagram}, page {page}, format {format}");
            
            try
            {
                // Check if drawio CLI is available
                bool drawIoAvailable = CheckDrawIoCliAvailable(logWriter, verbose);
                if (!drawIoAvailable)
                {
                    return new
                    {
                        isError = true,
                        content = new[]
                        {
                            new
                            {
                                type = "text",
                                text = "Error: drawio CLI is not available. Please install drawio CLI to enable image export."
                            }
                        }
                    };
                }
                
                // Create a temporary file to store the output image
                string tempFileName = $"{Path.GetFileNameWithoutExtension(diagram)}_{page}_{DateTime.Now:yyyyMMddHHmmss}.{format}";
                string outputImagePath = Path.Combine(diagramsDirectory, tempFileName);
                
                // Build the drawio CLI command with proper escaping
                string drawioCommand = $"drawio --export --format {format} --page-index {page} --transparent --scale 1.0 --border 0 --output \"{outputImagePath}\" \"{filePath}\"";
                
                // Try bash first
                var bashStartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "bash",
                    Arguments = $"-c \"{drawioCommand}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                LogMessage(logWriter, verbose, $"Attempting to export with bash: {bashStartInfo.FileName} {bashStartInfo.Arguments}");
                
                bool exportSuccess = false;
                string stderr = "";
                
                try
                {
                    using var bashProcess = System.Diagnostics.Process.Start(bashStartInfo);
                    if (bashProcess != null)
                    {
                        string stdout = await bashProcess.StandardOutput.ReadToEndAsync();
                        stderr = await bashProcess.StandardError.ReadToEndAsync();
                        await bashProcess.WaitForExitAsync();

                        if (bashProcess.ExitCode == 0)
                        {
                            exportSuccess = true;
                        }
                        else
                        {
                            LogMessage(logWriter, verbose, $"bash export failed: {stderr}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogMessage(logWriter, verbose, $"bash export failed: {ex.Message}");
                }

                // If bash failed, try direct drawio call (for PowerShell)
                if (!exportSuccess)
                {
                    LogMessage(logWriter, verbose, "Falling back to direct drawio CLI call...");
                    var processStartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "drawio",
                        Arguments = $"--export --format {format} --page-index {page} --transparent --scale 1.0 --border 0 --output \"{outputImagePath}\" \"{filePath}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    LogMessage(logWriter, verbose, $"Executing command: {processStartInfo.FileName} {processStartInfo.Arguments}");
                    
                    using var process = System.Diagnostics.Process.Start(processStartInfo);
                    if (process == null)
                    {
                        return new
                        {
                            isError = true,
                            content = new[]
                            {
                                new
                                {
                                    type = "text",
                                    text = "Error: Failed to start drawio CLI process"
                                }
                            }
                        };
                    }
                    
                    string stdout = await process.StandardOutput.ReadToEndAsync();
                    stderr = await process.StandardError.ReadToEndAsync();
                    await process.WaitForExitAsync();
                    
                    if (process.ExitCode == 0)
                    {
                        exportSuccess = true;
                    }
                }

                if (!exportSuccess)
                {
                    LogMessage(logWriter, true, $"drawio CLI export failed. Error: {stderr}");
                    
                    return new
                    {
                        isError = true,
                        content = new[]
                        {
                            new
                            {
                                type = "text",
                                text = $"Error: drawio CLI failed: {stderr}"
                            }
                        }
                    };
                }
                
                // Verify the file was created
                if (!File.Exists(outputImagePath))
                {
                    LogMessage(logWriter, true, $"Output file not created: {outputImagePath}");
                    
                    return new
                    {
                        isError = true,
                        content = new[]
                        {
                            new
                            {
                                type = "text",
                                text = "Error: Image file was not created by drawio CLI"
                            }
                        }
                    };
                }
                
                // Read the generated image and convert it to base64
                byte[] imageBytes = await File.ReadAllBytesAsync(outputImagePath);
                string base64Image = Convert.ToBase64String(imageBytes);
                
                // Clean up the temporary file
                try
                {
                    File.Delete(outputImagePath);
                }
                catch (Exception ex)
                {
                    LogMessage(logWriter, verbose, $"Failed to delete temporary file {outputImagePath}: {ex.Message}");
                }
                
                return new
                {
                    content = new[]
                    {
                        new
                        {
                            type = "image",
                            data = base64Image,
                            mimeType = $"image/{format.ToLower()}"
                        }
                    }
                };
            }
            catch (Exception ex)
            {
                LogMessage(logWriter, true, $"Error generating diagram image: {ex.Message}");
                
                return new
                {
                    isError = true,
                    content = new[]
                    {
                        new
                        {
                            type = "text",
                            text = $"Error: Failed to generate diagram image: {ex.Message}"
                        }
                    }
                };
            }
        }
        
        private static bool CheckDrawIoCliAvailable(TextWriter logWriter, bool verbose)
        {
            try
            {
                // Try bash first
                var bashStartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "bash",
                    Arguments = "-c \"drawio --version\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                LogMessage(logWriter, verbose, "Attempting to check drawio CLI with bash...");
                
                try
                {
                    using var bashProcess = System.Diagnostics.Process.Start(bashStartInfo);
                    if (bashProcess != null)
                    {
                        string stdout = bashProcess.StandardOutput.ReadToEnd();
                        string stderr = bashProcess.StandardError.ReadToEnd();
                        bashProcess.WaitForExit(5000); // Wait up to 5 seconds

                        if (bashProcess.ExitCode == 0)
                        {
                            LogMessage(logWriter, verbose, $"drawio CLI is available via bash, version: {stdout.Trim()}");
                            return true;
                        }
                        LogMessage(logWriter, verbose, $"bash drawio check failed: {stderr}");
                    }
                }
                catch (Exception ex)
                {
                    LogMessage(logWriter, verbose, $"bash drawio check failed: {ex.Message}");
                }

                // Fallback to direct drawio call (for PowerShell)
                LogMessage(logWriter, verbose, "Falling back to direct drawio CLI check...");
                var processStartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "drawio",
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                
                using var process = System.Diagnostics.Process.Start(processStartInfo);
                if (process == null)
                {
                    LogMessage(logWriter, verbose, "Failed to start drawio CLI process");
                    return false;
                }
                
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit(5000); // Wait up to 5 seconds
                
                if (process.ExitCode == 0)
                {
                    LogMessage(logWriter, verbose, $"drawio CLI is available via direct call, version: {output.Trim()}");
                    return true;
                }
                
                LogMessage(logWriter, verbose, $"Direct drawio CLI check failed with exit code {process.ExitCode}. Error: {error}");
                return false;
            }
            catch (Exception ex)
            {
                LogMessage(logWriter, verbose, $"Error checking drawio CLI availability: {ex.Message}");
                return false;
            }
        }

        // Helper methods for parameter extraction

        private static string GetParameterString(JsonElement parameters, string name, string defaultValue = null)
        {
            if (parameters.TryGetProperty(name, out var element))
            {
                return element.GetString() ?? defaultValue;
            }
            
            if (defaultValue != null)
            {
                return defaultValue;
            }
            
            throw new ArgumentException($"Required parameter '{name}' is missing. Expected type: string");
        }

        private static float GetParameterFloat(JsonElement parameters, string name)
        {
            if (!parameters.TryGetProperty(name, out var element))
            {
                throw new ArgumentException($"Required parameter '{name}' is missing. Expected type: number");
            }
            
            try
            {
                return element.GetSingle();
            }
            catch (InvalidOperationException)
            {
                throw new ArgumentException($"Parameter '{name}' must be a valid number");
            }
        }

        private static int GetParameterInt(JsonElement parameters, string name, int? defaultValue = null)
        {
            if (parameters.TryGetProperty(name, out var element))
            {
                try
                {
                    return element.GetInt32();
                }
                catch (InvalidOperationException)
                {
                    throw new ArgumentException($"Parameter '{name}' must be a valid integer");
                }
            }
            
            if (defaultValue.HasValue)
            {
                return defaultValue.Value;
            }
            
            throw new ArgumentException($"Required parameter '{name}' is missing. Expected type: integer");
        }

        private static JsonElement GetParameterObject(JsonElement parameters, string name)
        {
            if (parameters.TryGetProperty(name, out var element))
            {
                return element;
            }
            
            throw new ArgumentException($"Required parameter '{name}' is missing. Expected type: object");
        }
        
        // Helper method to update a shape with style properties
        private static Task<object> UpdateShapeWithStyleAsync(JsonElement parameters, string diagramsDirectory)
        {
            string diagram = GetParameterString(parameters, "diagram");
            
            // Try both naming conventions for parameters
            string shape_id;
            if (parameters.TryGetProperty("shapeId", out var shapeIdElement))
            {
                shape_id = shapeIdElement.GetString();
            }
            else
            {
                shape_id = GetParameterString(parameters, "shape_id");
            }
            
            // Try both naming conventions for style properties
            JsonElement stylePropertiesElement;
            if (parameters.TryGetProperty("styleProperties", out var stylePropsElement))
            {
                stylePropertiesElement = stylePropsElement;
            }
            else
            {
                stylePropertiesElement = GetParameterObject(parameters, "style_properties");
            }
            
            var styleProperties = stylePropertiesElement
                .EnumerateObject()
                .Select(p => new KeyValuePair<string, string>(p.Name, p.Value.GetString() ?? ""))
                .ToDictionary(p => p.Key, p => p.Value);
            
            string filePath = Path.Combine(diagramsDirectory, diagram);
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Diagram file not found: {diagram}");
            }
            
            // Load the diagram
            var loadedDiagram = LoadDiagram(filePath);
            
            // Build a combined style string instead of using a map
            var styleString = string.Join(";", styleProperties.Select(kv => $"{kv.Key}={kv.Value}"));
            if (!styleString.EndsWith(";")) styleString += ";";
            
            // Update the shape style by using UpdateShape instead
            var updatedDiagram = DrawIO.MCP.Core.DiagramManipulation.updateShape(
                loadedDiagram, 
                0, // page index, assuming 0 for now
                shape_id,
                null, // value
                null, // x
                null, // y
                null, // width
                null, // height
                styleString); // style
            
            // Save the updated diagram
            SaveDiagram(updatedDiagram, filePath);
            
            return Task.FromResult<object>(new
            {
                status = "success",
                message = $"Style of shape {shape_id} updated",
                styleProperties = styleProperties,
                content = new[] 
                { 
                    new 
                    { 
                        type = "text", 
                        text = $"Style of shape {shape_id} updated" 
                    } 
                }
            });
        }

        // Query tool implementations
        
        private static Task<object> FindElementsByTextAsync(JsonElement parameters, string diagramsDirectory)
        {
            string diagram = GetParameterString(parameters, "diagram");
            string searchText = GetParameterString(parameters, "search_text");
            int pageIndex = GetParameterInt(parameters, "page_index", 0);
            
            string filePath = Path.Combine(diagramsDirectory, diagram);
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Diagram file not found: {diagram}");
            }
            
            // Load the diagram and find elements by text
            var diagramObj = LoadDiagram(filePath);
            var results = DrawIO.MCP.Core.DiagramManipulation.findElementsByText(diagramObj, pageIndex, searchText);
            
            // Convert to a list of dictionaries for JSON response
            var elementList = results
                .Select<Tuple<string, string>, Dictionary<string, string>>(pair => new Dictionary<string, string>
                {
                    ["id"] = pair.Item1,
                    ["text"] = pair.Item2
                })
                .ToList();

            var messageContent = new[] 
            { 
                new Dictionary<string, string>
                { 
                    ["type"] = "text", 
                    ["text"] = $"Found {elementList.Count} elements containing '{searchText}'" 
                } 
            };

            return Task.FromResult<object>(new Dictionary<string, object>
            {
                ["elements"] = elementList,
                ["count"] = elementList.Count,
                ["content"] = messageContent
            });
        }
        
        private static Task<object> GetElementInfoAsync(JsonElement parameters, string diagramsDirectory)
        {
            string diagram = GetParameterString(parameters, "diagram");
            string elementId = GetParameterString(parameters, "element_id");
            int pageIndex = GetParameterInt(parameters, "page_index", 0);
            
            string filePath = Path.Combine(diagramsDirectory, diagram);
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Diagram file not found: {diagram}");
            }
            
            // Load the diagram and get element info
            var diagramObj = LoadDiagram(filePath);
            var elementInfo = DrawIO.MCP.Core.DiagramManipulation.getElementInfo(diagramObj, pageIndex, elementId);
            
            if (elementInfo.IsNone())
            {
                var errorMessage = new[] 
                { 
                    new Dictionary<string, string>
                    { 
                        ["type"] = "text", 
                        ["text"] = $"Element with ID '{elementId}' not found" 
                    } 
                };

                return Task.FromResult<object>(new Dictionary<string, object>
                {
                    ["error"] = $"Element with ID '{elementId}' not found",
                    ["isError"] = true,
                    ["content"] = errorMessage
                });
            }
            
            // Extract the element info from the F# option
            var info = elementInfo.Value;
            
            // Convert connections to list of dictionaries
            var connections = info.Connections
                .Select<Tuple<string, string>, Dictionary<string, string>>(conn => new Dictionary<string, string>
                {
                    ["edgeId"] = conn.Item1,
                    ["connectedTo"] = conn.Item2
                })
                .ToList();

            var infoMessage = new[] 
            { 
                new Dictionary<string, string>
                { 
                    ["type"] = "text", 
                    ["text"] = $"Element info for '{info.Id}': {info.Type} with value '{info.Value}'" 
                } 
            };

            var result = new Dictionary<string, object>
            {
                ["id"] = info.Id,
                ["type"] = info.Type,
                ["value"] = info.Value,
                ["style"] = info.Style,
                ["parent"] = info.Parent,
                ["connections"] = connections,
                ["content"] = infoMessage
            };

            if (info.Position.IsSome())
            {
                var pos = info.Position.Value;
                result["position"] = new Dictionary<string, double>
                {
                    ["x"] = pos.X,
                    ["y"] = pos.Y
                };
            }

            if (info.Size.IsSome())
            {
                var size = info.Size.Value;
                result["size"] = new Dictionary<string, double>
                {
                    ["width"] = size.Width,
                    ["height"] = size.Height
                };
            }

            return Task.FromResult<object>(result);
        }
        
        private static Task<object> ListNeighborsAsync(JsonElement parameters, string diagramsDirectory)
        {
            string diagram = GetParameterString(parameters, "diagram");
            string elementId = GetParameterString(parameters, "element_id");
            int pageIndex = GetParameterInt(parameters, "page_index", 0);
            
            string filePath = Path.Combine(diagramsDirectory, diagram);
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Diagram file not found: {diagram}");
            }
            
            // Load the diagram and list neighbors
            var diagramObj = LoadDiagram(filePath);
            var neighbors = DrawIO.MCP.Core.DiagramManipulation.listNeighbors(diagramObj, pageIndex, elementId);
            
            // Convert to a list of dictionaries for JSON response
            var neighborsList = neighbors
                .Select<Tuple<string, string, string>, Dictionary<string, string>>(tuple => new Dictionary<string, string>
                {
                    ["id"] = tuple.Item1,
                    ["label"] = tuple.Item2,
                    ["direction"] = tuple.Item3
                })
                .ToList();

            var neighborMessage = new[] 
            { 
                new Dictionary<string, string>
                { 
                    ["type"] = "text", 
                    ["text"] = $"Found {neighborsList.Count} neighbors for element {elementId}" 
                } 
            };
            
            return Task.FromResult<object>(new Dictionary<string, object>
            {
                ["neighbors"] = neighborsList,
                ["count"] = neighborsList.Count,
                ["content"] = neighborMessage
            });
        }
        
        private static Task<object> GetDiagramBoundsAsync(JsonElement parameters, string diagramsDirectory)
        {
            string diagram = GetParameterString(parameters, "diagram");
            int pageIndex = GetParameterInt(parameters, "page_index", 0);
            
            string filePath = Path.Combine(diagramsDirectory, diagram);
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Diagram file not found: {diagram}");
            }
            
            // Load the diagram and get bounds
            var diagramObj = LoadDiagram(filePath);
            var bounds = DrawIO.MCP.Core.DiagramManipulation.getDiagramBounds(diagramObj, pageIndex);
            
            if (bounds.IsNone())
            {
                var errorMessage = new[] 
                { 
                    new Dictionary<string, string>
                    { 
                        ["type"] = "text", 
                        ["text"] = "No elements with geometry found in the diagram" 
                    } 
                };

                return Task.FromResult<object>(new Dictionary<string, object>
                {
                    ["error"] = "No elements with geometry found in the diagram",
                    ["isError"] = true,
                    ["content"] = errorMessage
                });
            }
            
            // Extract bounds from the F# option
            var boundingBox = bounds.Value;

            var boundsMessage = new[] 
            { 
                new Dictionary<string, string>
                { 
                    ["type"] = "text", 
                    ["text"] = $"Diagram bounds retrieved successfully" 
                } 
            };
            
            return Task.FromResult<object>(new Dictionary<string, object>
            {
                ["minX"] = boundingBox.MinX,
                ["minY"] = boundingBox.MinY,
                ["maxX"] = boundingBox.MaxX,
                ["maxY"] = boundingBox.MaxY,
                ["width"] = boundingBox.Width,
                ["height"] = boundingBox.Height,
                ["content"] = boundsMessage
            });
        }

        private static Task<object> ResizeShapeAsync(JsonElement parameters, string diagramsDirectory)
        {
            string diagram = GetParameterString(parameters, "diagram");
            
            // Try both naming conventions for parameters
            string shapeId;
            if (parameters.TryGetProperty("shapeId", out var shapeIdElement))
            {
                shapeId = shapeIdElement.GetString();
            }
            else
            {
                shapeId = GetParameterString(parameters, "shape_id");
            }
            
            float width = parameters.GetProperty("width").GetSingle();
            float height = parameters.GetProperty("height").GetSingle();
            
            string filePath = Path.Combine(diagramsDirectory, diagram);
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Diagram file not found: {diagram}");
            }
            
            // Load the diagram, update the shape dimensions, and save it
            var diagramObj = LoadDiagram(filePath);
            var updatedDiagram = DrawIO.MCP.Core.DiagramManipulation.updateShape(
                diagramObj, 
                0, // page index
                shapeId,
                null, // value
                null, // x
                null, // y
                width,
                height,
                null // style
            );
            SaveDiagram(updatedDiagram, filePath);
            
            return Task.FromResult<object>(new 
            {
                status = "success",
                DiagramId = $"diagram://{diagram}",
                content = new[] 
                { 
                    new 
                    { 
                        type = "text", 
                        text = $"Resized shape {shapeId} to width: {width}, height: {height}" 
                    } 
                }
            });
        }

        private static Task<object> SetTextStyleAsync(JsonElement parameters, string diagramsDirectory)
        {
            string diagram = GetParameterString(parameters, "diagram");
            
            // Try both naming conventions for parameters
            string shapeId;
            if (parameters.TryGetProperty("shapeId", out var shapeIdElement))
            {
                shapeId = shapeIdElement.GetString();
            }
            else
            {
                shapeId = GetParameterString(parameters, "shape_id");
            }
            
            var styleBuilder = new System.Text.StringBuilder();
            
            // Handle font color
            if (parameters.TryGetProperty("font_color", out var fontColorElement))
            {
                string fontColor = fontColorElement.GetString();
                styleBuilder.Append($"fontColor={fontColor};");
            }
            
            // Handle font size
            if (parameters.TryGetProperty("font_size", out var fontSizeElement))
            {
                float fontSize = fontSizeElement.GetSingle();
                styleBuilder.Append($"fontSize={fontSize};");
            }
            
            // Handle font style
            if (parameters.TryGetProperty("font_style", out var fontStyleElement))
            {
                string fontStyle = fontStyleElement.GetString().ToLower();
                switch (fontStyle)
                {
                    case "bold":
                        styleBuilder.Append("fontStyle=1;");
                        break;
                    case "italic":
                        styleBuilder.Append("fontStyle=2;");
                        break;
                    case "bolditalic":
                        styleBuilder.Append("fontStyle=3;");
                        break;
                    case "normal":
                        styleBuilder.Append("fontStyle=0;");
                        break;
                    default:
                        throw new ArgumentException($"Invalid font style: {fontStyle}. Valid values are: normal, bold, italic, bolditalic");
                }
            }
            
            string styleString = styleBuilder.ToString();
            
            if (string.IsNullOrEmpty(styleString))
            {
                throw new ArgumentException("At least one style property (font_color, font_size, font_style) must be provided");
            }
            
            string filePath = Path.Combine(diagramsDirectory, diagram);
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Diagram file not found: {diagram}");
            }
            
            // Load the diagram, update style, and save it
            var diagramObj = LoadDiagram(filePath);
            var updatedDiagram = DrawIO.MCP.Core.DiagramManipulation.updateShape(
                diagramObj, 
                0, // page index
                shapeId,
                null, // value
                null, // x
                null, // y
                null, // width
                null, // height
                styleString
            );
            SaveDiagram(updatedDiagram, filePath);
            
            return Task.FromResult<object>(new 
            {
                status = "success",
                DiagramId = $"diagram://{diagram}",
                Style = styleString,
                content = new[] 
                { 
                    new 
                    { 
                        type = "text", 
                        text = $"Applied text style {styleString} to shape {shapeId}" 
                    } 
                }
            });
        }

        private static Task<object> SetLineStyleAsync(JsonElement parameters, string diagramsDirectory)
        {
            try
            {
                string diagram = GetParameterString(parameters, "diagram");
                string connectorId = GetParameterString(parameters, "connector_id");
                
                var styleBuilder = new System.Text.StringBuilder();
                bool hasStyleChanges = false;
                
                // Handle line style
                if (parameters.TryGetProperty("line_style", out var lineStyleElement))
                {
                    string lineStyle = lineStyleElement.GetString().ToLower();
                    switch (lineStyle)
                    {
                        case "dashed":
                            styleBuilder.Append("dashed=1;");
                            hasStyleChanges = true;
                            break;
                        case "dotted":
                            styleBuilder.Append("dashed=1;dashPattern=1 4;");
                            hasStyleChanges = true;
                            break;
                        case "solid":
                            styleBuilder.Append("dashed=0;");
                            hasStyleChanges = true;
                            break;
                        default:
                            throw new ArgumentException($"Invalid line style: {lineStyle}. Valid values are: solid, dashed, dotted");
                    }
                }
                
                // Handle line width
                if (parameters.TryGetProperty("line_width", out var lineWidthElement))
                {
                    float lineWidth = lineWidthElement.GetSingle();
                    if (lineWidth <= 0)
                    {
                        throw new ArgumentException("Line width must be greater than 0");
                    }
                    styleBuilder.Append($"strokeWidth={lineWidth};");
                    hasStyleChanges = true;
                }

                // Handle edge style
                if (parameters.TryGetProperty("edge_style", out var edgeStyleElement))
                {
                    string edgeStyle = edgeStyleElement.GetString().ToLower();
                    switch (edgeStyle)
                    {
                        case "sharp":
                            styleBuilder.Append("edgeStyle=sharp;rounded=0;");
                            hasStyleChanges = true;
                            break;
                        case "rounded":
                            styleBuilder.Append("edgeStyle=sharp;rounded=1;");
                            hasStyleChanges = true;
                            break;
                        case "curved":
                            styleBuilder.Append("edgeStyle=curved;rounded=1;");
                            hasStyleChanges = true;
                            break;
                        default:
                            throw new ArgumentException($"Invalid edge style: {edgeStyle}. Valid values are: sharp, rounded, curved");
                    }
                }

                // Handle routing style
                if (parameters.TryGetProperty("routing_style", out var routingStyleElement))
                {
                    string routingStyle = routingStyleElement.GetString().ToLower();
                    switch (routingStyle)
                    {
                        case "straight":
                            styleBuilder.Append("noJump=0;orthogonalLoop=0;");
                            hasStyleChanges = true;
                            break;
                        case "orthogonal":
                            styleBuilder.Append("noJump=0;orthogonalLoop=1;");
                            hasStyleChanges = true;
                            break;
                        case "curved":
                            styleBuilder.Append("noJump=0;curved=1;orthogonalLoop=0;");
                            hasStyleChanges = true;
                            break;
                        default:
                            throw new ArgumentException($"Invalid routing style: {routingStyle}. Valid values are: straight, orthogonal, curved");
                    }
                }

                // Handle jump style
                if (parameters.TryGetProperty("jump_style", out var jumpStyleElement))
                {
                    string jumpStyle = jumpStyleElement.GetString().ToLower();
                    switch (jumpStyle)
                    {
                        case "overlapped":
                            styleBuilder.Append("noJump=1;");
                            hasStyleChanges = true;
                            break;
                        case "arc":
                            styleBuilder.Append("noJump=0;jumpStyle=arc;");
                            hasStyleChanges = true;
                            break;
                        case "gap":
                            styleBuilder.Append("noJump=0;jumpStyle=gap;");
                            hasStyleChanges = true;
                            break;
                        default:
                            throw new ArgumentException($"Invalid jump style: {jumpStyle}. Valid values are: overlapped, arc, gap");
                    }
                }
                
                string styleString = styleBuilder.ToString();
                
                if (!hasStyleChanges)
                {
                    return Task.FromResult<object>(new
                    {
                        isError = true,
                        content = new[]
                        {
                            new
                            {
                                type = "text",
                                text = "At least one style property (line_style, line_width, edge_style, routing_style, jump_style) must be provided"
                            }
                        }
                    });
                }
                
                string filePath = Path.Combine(diagramsDirectory, diagram);
                if (!File.Exists(filePath))
                {
                    return Task.FromResult<object>(new
                    {
                        isError = true,
                        content = new[]
                        {
                            new
                            {
                                type = "text",
                                text = $"Diagram file not found: {diagram}"
                            }
                        }
                    });
                }
                
                // Load the diagram, update style, and save it
                var diagramObj = LoadDiagram(filePath);
                if (diagramObj == null)
                {
                    return Task.FromResult<object>(new
                    {
                        isError = true,
                        content = new[]
                        {
                            new
                            {
                                type = "text",
                                text = $"Failed to load diagram: {diagram}"
                            }
                        }
                    });
                }
                
                var updatedDiagram = DrawIO.MCP.Core.DiagramManipulation.updateShape(
                    diagramObj, 
                    0, // page index
                    connectorId,
                    null, // value
                    null, // x
                    null, // y
                    null, // width
                    null, // height
                    styleString
                );
                
                if (updatedDiagram == null)
                {
                    return Task.FromResult<object>(new
                    {
                        isError = true,
                        content = new[]
                        {
                            new
                            {
                                type = "text",
                                text = $"Failed to update connector style in diagram: {diagram}"
                            }
                        }
                    });
                }
                
                SaveDiagram(updatedDiagram, filePath);
                
                return Task.FromResult<object>(new 
                {
                    status = "success",
                    DiagramId = $"diagram://{diagram}",
                    Style = styleString,
                    content = new[] 
                    { 
                        new 
                        { 
                            type = "text", 
                            text = $"Applied line style {styleString} to connector {connectorId}" 
                        } 
                    }
                });
            }
            catch (Exception ex)
            {
                return Task.FromResult<object>(new
                {
                    isError = true,
                    content = new[]
                    {
                        new
                        {
                            type = "text",
                            text = $"Error: {ex.Message}"
                        }
                    }
                });
            }
        }

        private static Task<object> SetArrowStyleAsync(JsonElement parameters, string diagramsDirectory)
        {
            try
            {
                string diagram = GetParameterString(parameters, "diagram");
                string connectorId = GetParameterString(parameters, "connector_id");
                
                var styleBuilder = new System.Text.StringBuilder();
                bool hasStyleChanges = false;
                
                // Handle start arrow
                if (parameters.TryGetProperty("start_arrow", out var startArrowElement))
                {
                    string startArrow = startArrowElement.GetString().ToLower();
                    string arrowStyle = GetArrowStyle(startArrow);
                    styleBuilder.Append($"startArrow={arrowStyle};");
                    hasStyleChanges = true;
                }
                
                // Handle end arrow
                if (parameters.TryGetProperty("end_arrow", out var endArrowElement))
                {
                    string endArrow = endArrowElement.GetString().ToLower();
                    string arrowStyle = GetArrowStyle(endArrow);
                    styleBuilder.Append($"endArrow={arrowStyle};");
                    hasStyleChanges = true;
                }
                
                string styleString = styleBuilder.ToString();
                
                if (!hasStyleChanges)
                {
                    return Task.FromResult<object>(new
                    {
                        isError = true,
                        content = new[]
                        {
                            new
                            {
                                type = "text",
                                text = "At least one arrow style property (start_arrow, end_arrow) must be provided"
                            }
                        }
                    });
                }
                
                string filePath = Path.Combine(diagramsDirectory, diagram);
                if (!File.Exists(filePath))
                {
                    return Task.FromResult<object>(new
                    {
                        isError = true,
                        content = new[]
                        {
                            new
                            {
                                type = "text",
                                text = $"Diagram file not found: {diagram}"
                            }
                        }
                    });
                }
                
                // Load the diagram, update style, and save it
                var diagramObj = LoadDiagram(filePath);
                if (diagramObj == null)
                {
                    return Task.FromResult<object>(new
                    {
                        isError = true,
                        content = new[]
                        {
                            new
                            {
                                type = "text",
                                text = $"Failed to load diagram: {diagram}"
                            }
                        }
                    });
                }
                
                var updatedDiagram = DrawIO.MCP.Core.DiagramManipulation.updateShape(
                    diagramObj, 
                    0, // page index
                    connectorId,
                    null, // value
                    null, // x
                    null, // y
                    null, // width
                    null, // height
                    styleString
                );
                
                if (updatedDiagram == null)
                {
                    return Task.FromResult<object>(new
                    {
                        isError = true,
                        content = new[]
                        {
                            new
                            {
                                type = "text",
                                text = $"Failed to update connector style in diagram: {diagram}"
                            }
                        }
                    });
                }
                
                SaveDiagram(updatedDiagram, filePath);
                
                return Task.FromResult<object>(new 
                {
                    status = "success",
                    DiagramId = $"diagram://{diagram}",
                    Style = styleString,
                    content = new[] 
                    { 
                        new 
                        { 
                            type = "text", 
                            text = $"Applied arrow style {styleString} to connector {connectorId}" 
                        } 
                    }
                });
            }
            catch (Exception ex)
            {
                return Task.FromResult<object>(new
                {
                    isError = true,
                    content = new[]
                    {
                        new
                        {
                            type = "text",
                            text = $"Error: {ex.Message}"
                        }
                    }
                });
            }
        }

        private static string GetArrowStyle(string arrowType)
        {
            return arrowType.ToLower() switch
            {
                "none" => "none",
                "classic" => "classic",
                "diamond" => "diamond",
                "oval" => "oval",
                "open" => "open",
                "block" => "block",
                _ => throw new ArgumentException($"Invalid arrow style: {arrowType}. Valid values are: none, classic, diamond, oval, open, block")
            };
        }

        private static Task<object> ResetConnectorAsync(JsonElement parameters, string diagramsDirectory)
        {
            string diagram = GetParameterString(parameters, "diagram");
            string connectorId = GetParameterString(parameters, "connector_id");
            
            string filePath = Path.Combine(diagramsDirectory, diagram);
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Diagram file not found: {diagram}");
            }
            
            // Load the diagram
            var diagramObj = LoadDiagram(filePath);
            
            // Reset the connector by removing waypoints
            var updatedDiagram = DrawIO.MCP.Core.DiagramManipulation.updateShape(
                diagramObj, 
                0, // page index
                connectorId,
                null, // value
                null, // x
                null, // y
                null, // width
                null, // height
                "noJump=0;orthogonalLoop=1;jettySize=auto;" // Reset to default routing
            );
            SaveDiagram(updatedDiagram, filePath);
            
            return Task.FromResult<object>(new 
            {
                status = "success",
                DiagramId = $"diagram://{diagram}",
                content = new[] 
                { 
                    new 
                    { 
                        type = "text", 
                        text = $"Reset connector {connectorId} to default path" 
                    } 
                }
            });
        }

        private static Task<object> ReverseConnectorAsync(JsonElement parameters, string diagramsDirectory)
        {
            string diagram = GetParameterString(parameters, "diagram");
            string connectorId = GetParameterString(parameters, "connector_id");
            
            string filePath = Path.Combine(diagramsDirectory, diagram);
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Diagram file not found: {diagram}");
            }
            
            // Load the diagram
            var diagramObj = LoadDiagram(filePath);
            
            // Get the connector info
            var elementInfo = DrawIO.MCP.Core.DiagramManipulation.getElementInfo(diagramObj, 0, connectorId);
            if (elementInfo.IsNone())
            {
                throw new ArgumentException($"Connector with ID {connectorId} not found");
            }
            
            var info = elementInfo.Value;
            if (!info.IsEdge)
            {
                throw new ArgumentException($"Element {connectorId} is not a connector");
            }
            
            // Create a new connector in the reverse direction
            var (updatedDiagram, newConnectorId) = DrawIO.MCP.Core.DiagramManipulation.connectShapes(
                diagramObj,
                0, // page index
                info.Target.Value, // New source is old target
                info.Source.Value  // New target is old source
            );
            
            // Copy the style from the original connector
            updatedDiagram = DrawIO.MCP.Core.DiagramManipulation.updateShape(
                updatedDiagram,
                0,
                newConnectorId,
                info.Value, // Keep the same label
                null,
                null,
                null,
                null,
                info.Style // Keep the same style
            );
            
            // Delete the original connector
            updatedDiagram = DrawIO.MCP.Core.DiagramManipulation.deleteShape(updatedDiagram, 0, connectorId);
            
            // Save the updated diagram
            SaveDiagram(updatedDiagram, filePath);
            
            var messageContent = new[] 
            { 
                new Dictionary<string, string>
                { 
                    ["type"] = "text", 
                    ["text"] = $"Reversed connector direction" 
                } 
            };

            return Task.FromResult<object>(new Dictionary<string, object>
            {
                ["Status"] = "success",
                ["NewConnectorId"] = newConnectorId,
                ["content"] = messageContent
            });
        }

        private static Task<object> AddWaypointAsync(JsonElement parameters, string diagramsDirectory)
        {
            string diagram = GetParameterString(parameters, "diagram");
            string connectorId = GetParameterString(parameters, "connector_id");
            float x = GetParameterFloat(parameters, "x");
            float y = GetParameterFloat(parameters, "y");
            
            // Optional parameters
            bool isRelative = parameters.TryGetProperty("is_relative", out var relativeParam) && relativeParam.GetBoolean();
            int? position = parameters.TryGetProperty("position", out var positionParam) ? (int?)positionParam.GetInt32() : null;
            
            string filePath = Path.Combine(diagramsDirectory, diagram);
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Diagram file not found: {diagram}");
            }
            
            // Load the diagram
            var diagramObj = LoadDiagram(filePath);
            
            // Create some value to pass to F# Option
            Microsoft.FSharp.Core.FSharpOption<int> positionOption = 
                position.HasValue ? Microsoft.FSharp.Core.FSharpOption<int>.Some(position.Value) : null;
            
            // Add waypoint to the connector
            var updatedDiagram = DrawIO.MCP.Core.DiagramManipulation.addWaypoint(
                diagramObj, 
                0, // page index
                connectorId,
                x,
                y,
                isRelative,
                positionOption
            );
            SaveDiagram(updatedDiagram, filePath);
            
            return Task.FromResult<object>(new 
            {
                status = "success",
                DiagramId = $"diagram://{diagram}",
                content = new[] 
                { 
                    new 
                    { 
                        type = "text", 
                        text = $"Added waypoint at ({x}, {y}) to connector {connectorId}" 
                    } 
                }
            });
        }
        
        private static Task<object> RemoveWaypointAsync(JsonElement parameters, string diagramsDirectory)
        {
            string diagram = GetParameterString(parameters, "diagram");
            string connectorId = GetParameterString(parameters, "connector_id");
            int waypointIndex = GetParameterInt(parameters, "waypoint_index");
            
            string filePath = Path.Combine(diagramsDirectory, diagram);
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Diagram file not found: {diagram}");
            }
            
            // Load the diagram
            var diagramObj = LoadDiagram(filePath);
            
            // Remove waypoint from connector
            var updatedDiagram = DrawIO.MCP.Core.DiagramManipulation.removeWaypoint(
                diagramObj, 
                0, // page index
                connectorId,
                waypointIndex
            );
            SaveDiagram(updatedDiagram, filePath);
            
            return Task.FromResult<object>(new 
            {
                status = "success",
                DiagramId = $"diagram://{diagram}",
                content = new[] 
                { 
                    new 
                    { 
                        type = "text", 
                        text = $"Removed waypoint at index {waypointIndex} from connector {connectorId}" 
                    } 
                }
            });
        }
        
        private static Task<object> UpdateWaypointAsync(JsonElement parameters, string diagramsDirectory)
        {
            string diagram = GetParameterString(parameters, "diagram");
            string connectorId = GetParameterString(parameters, "connector_id");
            int waypointIndex = GetParameterInt(parameters, "waypoint_index");
            
            // Optional parameters - allow updating just x, just y, or both
            float? x = parameters.TryGetProperty("x", out var xParam) ? (float?)xParam.GetSingle() : null;
            float? y = parameters.TryGetProperty("y", out var yParam) ? (float?)yParam.GetSingle() : null;
            
            if (x == null && y == null)
            {
                throw new ArgumentException("At least one of 'x' or 'y' must be provided");
            }
            
            string filePath = Path.Combine(diagramsDirectory, diagram);
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Diagram file not found: {diagram}");
            }
            
            // Load the diagram
            var diagramObj = LoadDiagram(filePath);
            
            // Create F# options for x and y values
            Microsoft.FSharp.Core.FSharpOption<double> xOption = 
                x.HasValue ? Microsoft.FSharp.Core.FSharpOption<double>.Some(x.Value) : null;
            Microsoft.FSharp.Core.FSharpOption<double> yOption = 
                y.HasValue ? Microsoft.FSharp.Core.FSharpOption<double>.Some(y.Value) : null;
            
            // Update waypoint position
            var updatedDiagram = DrawIO.MCP.Core.DiagramManipulation.updateWaypoint(
                diagramObj, 
                0, // page index
                connectorId,
                waypointIndex,
                xOption,
                yOption
            );
            SaveDiagram(updatedDiagram, filePath);
            
            return Task.FromResult<object>(new 
            {
                status = "success",
                DiagramId = $"diagram://{diagram}",
                content = new[] 
                { 
                    new 
                    { 
                        type = "text", 
                        text = $"Updated waypoint at index {waypointIndex} for connector {connectorId}" 
                    } 
                }
            });
        }
        
        private static Task<object> GetWaypointsAsync(JsonElement parameters, string diagramsDirectory)
        {
            string diagram = GetParameterString(parameters, "diagram");
            string connectorId = GetParameterString(parameters, "connector_id");
            
            string filePath = Path.Combine(diagramsDirectory, diagram);
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Diagram file not found: {diagram}");
            }
            
            // Load the diagram
            var diagramObj = LoadDiagram(filePath);
            
            // Get waypoints
            var waypoints = DrawIO.MCP.Core.DiagramManipulation.getWaypoints(
                diagramObj, 
                0, // page index
                connectorId
            );
            
            // Convert waypoints to simplified objects for JSON response
            var waypointList = waypoints.Select((wp, index) => new 
            {
                index,
                x = wp.X,
                y = wp.Y,
                is_relative = wp.IsRelative
            }).ToArray();
            
            return Task.FromResult<object>(new 
            {
                status = "success",
                DiagramId = $"diagram://{diagram}",
                waypoints = waypointList,
                count = waypointList.Length,
                content = new[] 
                { 
                    new 
                    { 
                        type = "text", 
                        text = $"Retrieved {waypointList.Length} waypoints from connector {connectorId}" 
                    } 
                }
            });
        }
        
        private static Task<object> ClearWaypointsAsync(JsonElement parameters, string diagramsDirectory)
        {
            string diagram = GetParameterString(parameters, "diagram");
            string connectorId = GetParameterString(parameters, "connector_id");
            
            string filePath = Path.Combine(diagramsDirectory, diagram);
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Diagram file not found: {diagram}");
            }
            
            // Load the diagram
            var diagramObj = LoadDiagram(filePath);
            
            // Clear waypoints
            var updatedDiagram = DrawIO.MCP.Core.DiagramManipulation.clearWaypoints(
                diagramObj, 
                0, // page index
                connectorId
            );
            SaveDiagram(updatedDiagram, filePath);
            
            return Task.FromResult<object>(new 
            {
                status = "success",
                DiagramId = $"diagram://{diagram}",
                content = new[] 
                { 
                    new 
                    { 
                        type = "text", 
                        text = $"Cleared all waypoints from connector {connectorId}" 
                    } 
                }
            });
        }

        private static Task<object> GroupShapesAsync(JsonElement parameters, string diagramsDirectory)
        {
            try
            {
                // Extract parameters
                var diagramName = GetParameterString(parameters, "diagram");
                var pageIndex = GetParameterInt(parameters, "page_index", 0);
                var shapeIds = new List<string>();
                
                // Extract shape IDs from the JSON array
                if (parameters.TryGetProperty("shape_ids", out var shapeIdsElement) && shapeIdsElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var element in shapeIdsElement.EnumerateArray())
                    {
                        shapeIds.Add(element.GetString());
                    }
                }
                else
                {
                    throw new ArgumentException("Missing or invalid shape_ids array parameter");
                }
                
                if (shapeIds.Count < 2)
                {
                    throw new ArgumentException("At least two shapes must be provided for grouping");
                }

                // Load diagram
                var diagramPath = Path.Combine(diagramsDirectory, diagramName);
                var diagramObj = DrawIO.MCP.Core.FileOperations.loadDiagram(diagramPath);

                // Create F# list from C# list
                var fsharpList = Microsoft.FSharp.Collections.ListModule.OfSeq(shapeIds);
                
                // Group shapes
                var (updatedDiagram, groupId) = DrawIO.MCP.Core.DiagramManipulation.groupShapes(diagramObj, pageIndex, fsharpList);

                // Save updated diagram
                DrawIO.MCP.Core.FileOperations.saveDiagram(updatedDiagram, diagramPath);

                return Task.FromResult<object>(new 
                { 
                    status = "success",
                    groupId = groupId,
                    content = new[] 
                    { 
                        new 
                        { 
                            type = "text", 
                            text = $"Grouped {shapeIds.Count} shapes into group with ID {groupId}" 
                        } 
                    }
                });
            }
            catch (Exception ex)
            {
                return Task.FromResult<object>(new
                {
                    isError = true,
                    error = ex.Message,
                    content = new[]
                    {
                        new
                        {
                            type = "text",
                            text = $"Error: {ex.Message}"
                        }
                    }
                });
            }
        }

        private static Task<object> UngroupShapesAsync(JsonElement parameters, string diagramsDirectory)
        {
            try
            {
                // Extract parameters
                var diagramName = GetParameterString(parameters, "diagram");
                var groupId = GetParameterString(parameters, "group_id");
                var pageIndex = GetParameterInt(parameters, "page_index", 0);

                // Load diagram
                var diagramPath = Path.Combine(diagramsDirectory, diagramName);
                var diagramObj = DrawIO.MCP.Core.FileOperations.loadDiagram(diagramPath);

                // Ungroup shapes
                var updatedDiagram = DrawIO.MCP.Core.DiagramManipulation.ungroupShapes(diagramObj, pageIndex, groupId);

                // Save updated diagram
                DrawIO.MCP.Core.FileOperations.saveDiagram(updatedDiagram, diagramPath);

                return Task.FromResult<object>(new 
                { 
                    status = "success",
                    content = new[] 
                    { 
                        new 
                        { 
                            type = "text", 
                            text = $"Ungrouped shapes from group with ID {groupId}" 
                        } 
                    }
                });
            }
            catch (Exception ex)
            {
                return Task.FromResult<object>(new
                {
                    isError = true,
                    error = ex.Message,
                    content = new[]
                    {
                        new
                        {
                            type = "text",
                            text = $"Error: {ex.Message}"
                        }
                    }
                });
            }
        }

        // Add right before the last closing brace of the class
        private static Task<object> RotateShapeAsync(JsonElement parameters, string diagramsDirectory, bool verbose = false, TextWriter logWriter = null)
        {
            string diagram = GetParameterString(parameters, "diagram");
            string shapeId = GetParameterString(parameters, "shape_id");
            float angle = GetParameterFloat(parameters, "angle");
            
            string filePath = Path.Combine(diagramsDirectory, diagram);
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Diagram file not found: {diagram}");
            }
            
            // Load the diagram
            var loadedDiagram = LoadDiagram(filePath);
            
            // Rotate the shape
            var updatedDiagram = DrawIO.MCP.Core.DiagramManipulation.rotateShape(loadedDiagram, shapeId, angle);
            
            // Save the updated diagram
            SaveDiagram(updatedDiagram, filePath);
            
            bool returnDiagram = false;
            if (parameters.TryGetProperty("return_diagram", out var returnDiagramProp))
            {
                returnDiagram = returnDiagramProp.GetBoolean();
            }
            
            if (returnDiagram)
            {
                return GetDiagramImageAsync(parameters, diagramsDirectory, logWriter, verbose);
            }
            
            return Task.FromResult<object>(new 
            {
                status = "success",
                message = $"Shape {shapeId} rotated by {angle} degrees",
                content = new[] 
                { 
                    new 
                    { 
                        type = "text", 
                        text = $"Shape {shapeId} rotated by {angle} degrees" 
                    } 
                }
            });
        }

        private static Task<object> FlipShapeAsync(JsonElement parameters, string diagramsDirectory, bool verbose = false, TextWriter logWriter = null)
        {
            string diagram = GetParameterString(parameters, "diagram");
            string shapeId = GetParameterString(parameters, "shape_id");
            string directionStr = GetParameterString(parameters, "direction");
            
            string filePath = Path.Combine(diagramsDirectory, diagram);
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Diagram file not found: {diagram}");
            }
            
            // Load the diagram
            var loadedDiagram = LoadDiagram(filePath);
            
            // Convert direction from string to enum
            DrawIO.MCP.Core.DiagramManipulation.FlipDirection flipDirection;
            if (string.Equals(directionStr, "horizontal", StringComparison.OrdinalIgnoreCase))
            {
                flipDirection = DrawIO.MCP.Core.DiagramManipulation.FlipDirection.Horizontal;
            }
            else if (string.Equals(directionStr, "vertical", StringComparison.OrdinalIgnoreCase))
            {
                flipDirection = DrawIO.MCP.Core.DiagramManipulation.FlipDirection.Vertical;
            }
            else
            {
                throw new ArgumentException($"Invalid flip direction '{directionStr}'. Must be 'horizontal' or 'vertical'.");
            }
            
            // Flip the shape
            var updatedDiagram = DrawIO.MCP.Core.DiagramManipulation.flipShape(loadedDiagram, shapeId, flipDirection);
            
            // Save the updated diagram
            SaveDiagram(updatedDiagram, filePath);
            
            bool returnDiagram = false;
            if (parameters.TryGetProperty("return_diagram", out var returnDiagramProp))
            {
                returnDiagram = returnDiagramProp.GetBoolean();
            }
            
            if (returnDiagram)
            {
                return GetDiagramImageAsync(parameters, diagramsDirectory, logWriter, verbose);
            }
            
            return Task.FromResult<object>(new 
            {
                status = "success",
                message = $"Shape {shapeId} flipped {directionStr}",
                content = new[] 
                { 
                    new 
                    { 
                        type = "text", 
                        text = $"Shape {shapeId} flipped {directionStr}" 
                    } 
                }
            });
        }

        private static Task<object> SetDiagramBackgroundAsync(JsonElement parameters, string diagramsDirectory, TextWriter logWriter = null, bool verbose = false)
        {
            try
            {
                // Extract parameters
                var diagramName = GetParameterString(parameters, "diagram");
                
                // Try to get background color and image
                string backgroundColor = null;
                string backgroundImage = null;
                
                if (parameters.TryGetProperty("background_color", out var bgColorElement))
                {
                    backgroundColor = bgColorElement.GetString();
                }
                
                if (parameters.TryGetProperty("background_image", out var bgImageElement))
                {
                    backgroundImage = bgImageElement.GetString();
                }
                
                // Check that at least one parameter is provided
                if (string.IsNullOrEmpty(backgroundColor) && string.IsNullOrEmpty(backgroundImage))
                {
                    throw new ArgumentException("At least one of background_color or background_image must be provided");
                }
                
                // Load diagram
                var diagramPath = Path.Combine(diagramsDirectory, diagramName);
                var diagramObj = DrawIO.MCP.Core.FileOperations.loadDiagram(diagramPath);
                
                // Create FSharpOption types for the parameters
                FSharpOption<string> backgroundImageOption = 
                    string.IsNullOrEmpty(backgroundImage) 
                        ? FSharpOption<string>.None 
                        : FSharpOption<string>.Some(backgroundImage);
                        
                FSharpOption<string> backgroundColorOption = 
                    string.IsNullOrEmpty(backgroundColor) 
                        ? FSharpOption<string>.None 
                        : FSharpOption<string>.Some(backgroundColor);
                
                // Set the diagram background
                var updatedDiagram = DrawIO.MCP.Core.DiagramManipulation.setDiagramBackground(
                    diagramObj, backgroundImageOption, backgroundColorOption);
                
                // Save updated diagram
                DrawIO.MCP.Core.FileOperations.saveDiagram(updatedDiagram, diagramPath);
                
                // Generate response message
                string message = "Diagram background updated";
                if (!string.IsNullOrEmpty(backgroundColor))
                {
                    message += $" with color {backgroundColor}";
                }
                if (!string.IsNullOrEmpty(backgroundImage))
                {
                    message += $"{(!string.IsNullOrEmpty(backgroundColor) ? " and" : " with")} image {backgroundImage}";
                }
                
                return Task.FromResult<object>(new 
                { 
                    status = "success",
                    content = new[] 
                    { 
                        new 
                        { 
                            type = "text", 
                            text = message
                        } 
                    }
                });
            }
            catch (Exception ex)
            {
                return Task.FromResult<object>(new
                {
                    isError = true,
                    error = ex.Message,
                    content = new[]
                    {
                        new
                        {
                            type = "text",
                            text = $"Error: {ex.Message}"
                        }
                    }
                });
            }
        }

        private static Task<object> ConnectShapesAtPointsAsync(JsonElement parameters, string diagramsDirectory, TextWriter logWriter = null, bool verbose = false)
        {
            try
            {
                // Extract parameters
                var diagramName = GetParameterString(parameters, "diagram");
                var sourceId = GetParameterString(parameters, "source_id");
                var targetId = GetParameterString(parameters, "target_id");
                var pageIndex = GetParameterInt(parameters, "page_index", 0);
                
                // Optional parameters
                double? sourceX = null;
                double? sourceY = null;
                double? targetX = null;
                double? targetY = null;
                
                if (parameters.TryGetProperty("source_x", out var sourceXElement) && sourceXElement.ValueKind == JsonValueKind.Number)
                {
                    sourceX = sourceXElement.GetDouble();
                }
                
                if (parameters.TryGetProperty("source_y", out var sourceYElement) && sourceYElement.ValueKind == JsonValueKind.Number)
                {
                    sourceY = sourceYElement.GetDouble();
                }
                
                if (parameters.TryGetProperty("target_x", out var targetXElement) && targetXElement.ValueKind == JsonValueKind.Number)
                {
                    targetX = targetXElement.GetDouble();
                }
                
                if (parameters.TryGetProperty("target_y", out var targetYElement) && targetYElement.ValueKind == JsonValueKind.Number)
                {
                    targetY = targetYElement.GetDouble();
                }
                
                // Load diagram
                var diagramPath = Path.Combine(diagramsDirectory, diagramName);
                var diagramObj = DrawIO.MCP.Core.FileOperations.loadDiagram(diagramPath);
                
                // Create FSharpOption types for the parameters
                FSharpOption<double> sourceXOption = 
                    sourceX.HasValue 
                        ? FSharpOption<double>.Some(sourceX.Value) 
                        : FSharpOption<double>.None;
                        
                FSharpOption<double> sourceYOption = 
                    sourceY.HasValue 
                        ? FSharpOption<double>.Some(sourceY.Value) 
                        : FSharpOption<double>.None;
                        
                FSharpOption<double> targetXOption = 
                    targetX.HasValue 
                        ? FSharpOption<double>.Some(targetX.Value) 
                        : FSharpOption<double>.None;
                        
                FSharpOption<double> targetYOption = 
                    targetY.HasValue 
                        ? FSharpOption<double>.Some(targetY.Value) 
                        : FSharpOption<double>.None;
                
                // Connect shapes at specified points
                var (updatedDiagram, edgeId) = DrawIO.MCP.Core.DiagramManipulation.connectShapesAtPoints(
                    diagramObj, pageIndex, sourceId, targetId, sourceXOption, sourceYOption, targetXOption, targetYOption);
                
                // Save updated diagram
                DrawIO.MCP.Core.FileOperations.saveDiagram(updatedDiagram, diagramPath);
                
                return Task.FromResult<object>(new 
                { 
                    status = "success",
                    connectorId = edgeId,
                    content = new[] 
                    { 
                        new 
                        { 
                            type = "text", 
                            text = $"Connected shapes {sourceId} and {targetId} with connector {edgeId}"
                        } 
                    }
                });
            }
            catch (Exception ex)
            {
                return Task.FromResult<object>(new
                {
                    isError = true,
                    error = ex.Message,
                    content = new[]
                    {
                        new
                        {
                            type = "text",
                            text = $"Error: {ex.Message}"
                        }
                    }
                });
            }
        }

        // Helper method to load a diagram from a file path
        private static DrawIO.MCP.Core.Types.Diagram LoadDiagram(string filePath)
        {
            return DrawIO.MCP.Core.FileOperations.loadDiagram(filePath);
        }

        // Helper method to save a diagram to a file path
        private static void SaveDiagram(DrawIO.MCP.Core.Types.Diagram diagram, string filePath)
        {
            DrawIO.MCP.Core.FileOperations.saveDiagram(diagram, filePath);
        }
    }
} 