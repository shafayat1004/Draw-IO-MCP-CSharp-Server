using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.FSharp.Core;
using Microsoft.FSharp.Collections;
using DrawIO.MCP.Core;
using static DrawIO.MCP.Core.Types;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System.Linq;

namespace DrawIO.MCP.SSE
{
    public class DrawIoService
    {
        private readonly string _diagramsDirectory;
        private readonly ILogger<DrawIoService>? _logger;

        public DrawIoService(string diagramsDirectory)
        {
            _diagramsDirectory = diagramsDirectory;
            _logger = null;
            
            // Create the diagrams directory if it doesn't exist
            if (!Directory.Exists(_diagramsDirectory))
            {
                Directory.CreateDirectory(_diagramsDirectory);
            }
        }

        public DrawIoService(string diagramsDirectory, ILogger<DrawIoService> logger)
        {
            _diagramsDirectory = diagramsDirectory;
            _logger = logger;
            
            // Create the diagrams directory if it doesn't exist
            if (!Directory.Exists(_diagramsDirectory))
            {
                Directory.CreateDirectory(_diagramsDirectory);
            }
        }

        public DrawIoService(ILogger<DrawIoService> logger)
        {
            _diagramsDirectory = Environment.GetEnvironmentVariable("DIAGRAMS_DIR") ?? 
                                 Path.Combine(Directory.GetCurrentDirectory(), "diagrams");
            _logger = logger;
            
            // Create the diagrams directory if it doesn't exist
            if (!Directory.Exists(_diagramsDirectory))
            {
                Directory.CreateDirectory(_diagramsDirectory);
            }
        }

        /// <summary>
        /// Creates a new empty diagram
        /// </summary>
        public Diagram CreateNewDiagram(string name)
        {
            if (string.IsNullOrEmpty(Path.GetExtension(name)))
            {
                name += ".drawio";
            }
            
            string filePath = Path.Combine(_diagramsDirectory, name);
            
            var diagram = FileOperations.createNewDiagram(filePath);
            return diagram;
        }

        /// <summary>
        /// Loads a diagram from file
        /// </summary>
        public Diagram LoadDiagram(string name)
        {
            if (string.IsNullOrEmpty(Path.GetExtension(name)))
            {
                name += ".drawio";
            }
            
            string filePath = Path.Combine(_diagramsDirectory, name);
            
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Diagram file not found: {filePath}");
            }
            
            return FileOperations.loadDiagram(filePath);
        }

        /// <summary>
        /// Saves a diagram to file
        /// </summary>
        public void SaveDiagram(Diagram diagram, string name)
        {
            if (string.IsNullOrEmpty(Path.GetExtension(name)))
            {
                name += ".drawio";
            }
            
            string filePath = Path.Combine(_diagramsDirectory, name);
            FileOperations.saveDiagram(diagram, filePath);
        }

        /// <summary>
        /// Adds a shape to a diagram
        /// </summary>
        public (Diagram, string) AddShape(string diagramName, string value, float x, float y, float width, float height, string shape)
        {
            var diagram = LoadDiagram(diagramName);
            var (updatedDiagram, shapeId) = DiagramManipulation.addShape(diagram, 0, value, x, y, width, height, shape);
            SaveDiagram(updatedDiagram, diagramName);
            return (updatedDiagram, shapeId);
        }

        /// <summary>
        /// Connects two shapes with an edge
        /// </summary>
        public (Diagram, string) ConnectShapes(string diagramName, string sourceId, string targetId)
        {
            var diagram = LoadDiagram(diagramName);
            var (updatedDiagram, edgeId) = DiagramManipulation.connectShapes(diagram, 0, sourceId, targetId);
            SaveDiagram(updatedDiagram, diagramName);
            return (updatedDiagram, edgeId);
        }

        /// <summary>
        /// Applies a style to a shape
        /// </summary>
        public void StyleShape(string diagramName, string shapeId, string fillColor, string strokeColor)
        {
            var diagram = LoadDiagram(diagramName);
            
            var styleProperties = new Dictionary<string, string>();
            
            if (!string.IsNullOrEmpty(fillColor))
            {
                styleProperties.Add("fillColor", fillColor);
            }
            
            if (!string.IsNullOrEmpty(strokeColor))
            {
                styleProperties.Add("strokeColor", strokeColor);
            }
            
            // Convert C# Dictionary to F# Map
            var style = styleProperties.ToFSharpMap();
            var updatedDiagram = DiagramManipulation.updateShapeStyle(diagram, shapeId, style);
            
            SaveDiagram(updatedDiagram, diagramName);
        }

        /// <summary>
        /// Moves a shape to a new position
        /// </summary>
        public void MoveShape(string diagramName, string shapeId, float x, float y)
        {
            var diagram = LoadDiagram(diagramName);
            var updatedDiagram = DiagramManipulation.moveShape(diagram, shapeId, x, y);
            SaveDiagram(updatedDiagram, diagramName);
        }

        /// <summary>
        /// Rotates a shape by the specified angle (in degrees)
        /// </summary>
        public void RotateShape(string diagramName, string shapeId, float angle)
        {
            var diagram = LoadDiagram(diagramName);
            var updatedDiagram = DiagramManipulation.rotateShape(diagram, shapeId, angle);
            SaveDiagram(updatedDiagram, diagramName);
        }

        /// <summary>
        /// Flips a shape horizontally or vertically
        /// </summary>
        public void FlipShape(string diagramName, string shapeId, string direction)
        {
            var diagram = LoadDiagram(diagramName);
            
            DiagramManipulation.FlipDirection flipDirection;
            if (string.Equals(direction, "horizontal", StringComparison.OrdinalIgnoreCase))
            {
                flipDirection = DiagramManipulation.FlipDirection.Horizontal;
            }
            else if (string.Equals(direction, "vertical", StringComparison.OrdinalIgnoreCase))
            {
                flipDirection = DiagramManipulation.FlipDirection.Vertical;
            }
            else
            {
                throw new ArgumentException($"Invalid flip direction '{direction}'. Must be 'horizontal' or 'vertical'.");
            }
            
            var updatedDiagram = DiagramManipulation.flipShape(diagram, shapeId, flipDirection);
            SaveDiagram(updatedDiagram, diagramName);
        }

        /// <summary>
        /// Sets the background color or image for a diagram
        /// </summary>
        public void SetDiagramBackground(string diagramName, string backgroundImage, string backgroundColor)
        {
            var diagram = LoadDiagram(diagramName);
            
            // Create FSharpOption types for the parameters
            FSharpOption<string> backgroundImageOption = 
                string.IsNullOrEmpty(backgroundImage) 
                    ? FSharpOption<string>.None 
                    : FSharpOption<string>.Some(backgroundImage);
                    
            FSharpOption<string> backgroundColorOption = 
                string.IsNullOrEmpty(backgroundColor) 
                    ? FSharpOption<string>.None 
                    : FSharpOption<string>.Some(backgroundColor);
            
            var updatedDiagram = DiagramManipulation.setDiagramBackground(diagram, backgroundImageOption, backgroundColorOption);
            SaveDiagram(updatedDiagram, diagramName);
        }

        /// <summary>
        /// Connects two shapes with an edge at specific points
        /// </summary>
        public (Diagram, string) ConnectShapesAtPoints(
            string diagramName, 
            string sourceId, 
            string targetId, 
            float? sourceX = null, 
            float? sourceY = null, 
            float? targetX = null, 
            float? targetY = null)
        {
            var diagram = LoadDiagram(diagramName);
            
            // Create FSharpOption types for the parameters, converting float to double
            FSharpOption<double> sourceXOption = 
                sourceX.HasValue 
                    ? FSharpOption<double>.Some((double)sourceX.Value) 
                    : FSharpOption<double>.None;
                    
            FSharpOption<double> sourceYOption = 
                sourceY.HasValue 
                    ? FSharpOption<double>.Some((double)sourceY.Value) 
                    : FSharpOption<double>.None;
                    
            FSharpOption<double> targetXOption = 
                targetX.HasValue 
                    ? FSharpOption<double>.Some((double)targetX.Value) 
                    : FSharpOption<double>.None;
                    
            FSharpOption<double> targetYOption = 
                targetY.HasValue 
                    ? FSharpOption<double>.Some((double)targetY.Value) 
                    : FSharpOption<double>.None;
            
            var (updatedDiagram, connectorId) = DiagramManipulation.connectShapesAtPoints(
                diagram, 0, sourceId, targetId, sourceXOption, sourceYOption, targetXOption, targetYOption);
            
            SaveDiagram(updatedDiagram, diagramName);
            
            return (updatedDiagram, connectorId);
        }

        /// <summary>
        /// Gets a diagram as an image in the specified format
        /// </summary>
        public string GetDiagramImage(string diagramName, string format = "png", int page = 0)
        {
            if (string.IsNullOrEmpty(Path.GetExtension(diagramName)))
            {
                diagramName += ".drawio";
            }
            
            string diagramPath = Path.Combine(_diagramsDirectory, diagramName);
            
            if (!File.Exists(diagramPath))
            {
                throw new FileNotFoundException($"Diagram file not found: {diagramPath}");
            }
            
            // Create a temporary file to store the output image
            string tempFileName = $"{Path.GetFileNameWithoutExtension(diagramName)}_{page}_{DateTime.Now:yyyyMMddHHmmss}.{format}";
            string outputImagePath = Path.Combine(_diagramsDirectory, tempFileName);
            
            // Build the drawio CLI command
            string drawioCommand = $"drawio --export --format {format} --page-index {page} --transparent --scale 1.0 --border 0 --output \"{outputImagePath}\" \"{diagramPath}\"";
            
            // Execute the command
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"-c \"{drawioCommand}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };
            
            process.Start();
            process.WaitForExit();
            
            // Check if the image was created
            if (!File.Exists(outputImagePath))
            {
                throw new Exception($"Failed to generate diagram image. Command: {drawioCommand}");
            }
            
            // Read the image file as a base64 string
            byte[] imageBytes = File.ReadAllBytes(outputImagePath);
            string base64Image = Convert.ToBase64String(imageBytes);
            
            // Clean up the temporary file
            File.Delete(outputImagePath);
            
            return base64Image;
        }
        
        /// <summary>
        /// Gets a diagram as a base64-encoded image
        /// </summary>
        public async Task<Dictionary<string, string>?> GetDiagramImageAsBase64(string fileName, int pageIndex = 0, string format = "png")
        {
            if (!fileName.EndsWith(".drawio", StringComparison.OrdinalIgnoreCase))
            {
                fileName += ".drawio";
            }
            
            string filePath = Path.Combine(_diagramsDirectory, fileName);
            
            if (!File.Exists(filePath))
            {
                _logger?.LogError($"Diagram file not found: {fileName}");
                throw new FileNotFoundException($"Diagram file not found: {fileName}");
            }
            
            _logger?.LogInformation($"Generating diagram image for {fileName}, page {pageIndex}, format {format}");
            
            try
            {
                // Check if drawio CLI is available
                if (!await IsDrawIoCliAvailableAsync())
                {
                    _logger?.LogWarning("drawio CLI is not available for image export");
                    return null;
                }
                
                // Create a temporary file to store the output image
                string tempFileName = $"{Path.GetFileNameWithoutExtension(fileName)}_{pageIndex}_{DateTime.Now:yyyyMMddHHmmss}.{format}";
                string outputImagePath = Path.Combine(_diagramsDirectory, tempFileName);
                
                // Build the drawio CLI command with proper escaping
                string drawioCommand = $"drawio --export --format {format} --page-index {pageIndex} --transparent --scale 1.0 --border 0 --output \"{outputImagePath}\" \"{filePath}\"";
                
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

                _logger?.LogDebug($"Attempting to export with bash: {bashStartInfo.FileName} {bashStartInfo.Arguments}");
                
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
                    _logger?.LogWarning($"bash export failed: {ex.Message}");
                }

                // If bash failed, try direct drawio call
                if (!exportSuccess)
                {
                    _logger?.LogDebug("Falling back to direct drawio CLI call...");
                    var processStartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "drawio",
                        Arguments = $"--export --format {format} --page-index {pageIndex} --transparent --scale 1.0 --border 0 --output \"{outputImagePath}\" \"{filePath}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    _logger?.LogDebug($"Executing command: {processStartInfo.FileName} {processStartInfo.Arguments}");
                    
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
                    _logger?.LogWarning($"Failed to generate diagram image");
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
                    _logger?.LogWarning($"Failed to delete temporary file {outputImagePath}: {ex.Message}");
                }
                
                return new Dictionary<string, string>
                {
                    ["type"] = "image",
                    ["data"] = base64Image,
                    ["mimeType"] = $"image/{format.ToLower()}"
                };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error generating diagram image: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Checks if the drawio CLI is available
        /// </summary>
        private async Task<bool> IsDrawIoCliAvailableAsync()
        {
            try
            {
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
                if (process != null)
                {
                    await process.WaitForExitAsync();
                    return process.ExitCode == 0;
                }
            }
            catch
            {
                _logger?.LogWarning("DrawIO CLI check failed - command not found or other error");
            }
            
            return false;
        }

        /// <summary>
        /// Gets all diagrams in the diagrams directory
        /// </summary>
        public List<string> GetAllDiagrams()
        {
            return Directory.GetFiles(_diagramsDirectory, "*.drawio")
                .Select(Path.GetFileName)
                .Where(name => name != null)
                .Cast<string>()  // Force non-nullable after the filter
                .ToList();
        }

        /// <summary>
        /// Creates a new diagram
        /// </summary>
        public Diagram CreateDiagram(string fileName)
        {
            return CreateNewDiagram(fileName);
        }

        /// <summary>
        /// Gets a diagram
        /// </summary>
        public Diagram GetDiagram(string fileName)
        {
            return LoadDiagram(fileName);
        }

        /// <summary>
        /// Deletes a shape from a diagram
        /// </summary>
        public Diagram DeleteShape(string fileName, string shapeId)
        {
            var diagram = LoadDiagram(fileName);
            var updatedDiagram = DiagramManipulation.deleteShape(diagram, 0, shapeId);
            SaveDiagram(updatedDiagram, fileName);
            
            return updatedDiagram;
        }

        /// <summary>
        /// Updates a shape in a diagram
        /// </summary>
        public Diagram UpdateShape(string fileName, string shapeId, string value, float? x = null, float? y = null, float? width = null, float? height = null, string style = null)
        {
            var diagram = LoadDiagram(fileName);
            
            // Convert nullable parameters to F# options with correct types (double instead of float)
            var xOpt = x.HasValue ? FSharpOption<double>.Some((double)x.Value) : FSharpOption<double>.None;
            var yOpt = y.HasValue ? FSharpOption<double>.Some((double)y.Value) : FSharpOption<double>.None;
            var widthOpt = width.HasValue ? FSharpOption<double>.Some((double)width.Value) : FSharpOption<double>.None;
            var heightOpt = height.HasValue ? FSharpOption<double>.Some((double)height.Value) : FSharpOption<double>.None;
            var styleOpt = !string.IsNullOrEmpty(style) ? FSharpOption<string>.Some(style) : FSharpOption<string>.None;
            
            var updatedDiagram = DiagramManipulation.updateShape(diagram, 0, shapeId, value, xOpt, yOpt, widthOpt, heightOpt, styleOpt);
            SaveDiagram(updatedDiagram, fileName);
            
            return updatedDiagram;
        }

        /// <summary>
        /// Arranges a diagram using the specified layout
        /// </summary>
        public Diagram ArrangeDiagram(string fileName, string layoutType)
        {
            var diagram = LoadDiagram(fileName);
            
            var updatedDiagram = DiagramManipulation.arrangeLayout(diagram, 0, layoutType);
            SaveDiagram(updatedDiagram, fileName);
            
            return updatedDiagram;
        }

        /// <summary>
        /// Generates a sample VPC diagram
        /// </summary>
        public Diagram GenerateVpcDiagram(string fileName)
        {
            if (string.IsNullOrEmpty(Path.GetExtension(fileName)))
            {
                fileName += ".drawio";
            }
            
            string filePath = Path.Combine(_diagramsDirectory, fileName);
            
            // Create base diagram
            var diagram = FileOperations.createNewDiagram(filePath);
            
            // Create VPC shapes
            string vpcId = DiagramManipulation.addShape(diagram, 0, "VPC", 400, 300, 500, 400, "rectangle").Item2;
            string publicSubnetId = DiagramManipulation.addShape(diagram, 0, "Public Subnet", 250, 150, 200, 120, "rectangle").Item2;
            string privateSubnetId = DiagramManipulation.addShape(diagram, 0, "Private Subnet", 550, 150, 200, 120, "rectangle").Item2;
            string igwId = DiagramManipulation.addShape(diagram, 0, "Internet Gateway", 400, 50, 120, 60, "rectangle").Item2;
            
            // Apply styles
            var vpcStyle = Microsoft.FSharp.Collections.MapModule.OfSeq(
                new[] { 
                    new Tuple<string, string>("fillColor", "#f5f5f5"),
                    new Tuple<string, string>("strokeColor", "#666666"),
                    new Tuple<string, string>("strokeWidth", "2")
                });
            
            var publicSubnetStyle = Microsoft.FSharp.Collections.MapModule.OfSeq(
                new[] { 
                    new Tuple<string, string>("fillColor", "#dae8fc"),
                    new Tuple<string, string>("strokeColor", "#6c8ebf")
                });
            
            var privateSubnetStyle = Microsoft.FSharp.Collections.MapModule.OfSeq(
                new[] { 
                    new Tuple<string, string>("fillColor", "#d5e8d4"),
                    new Tuple<string, string>("strokeColor", "#82b366")
                });
            
            var igwStyle = Microsoft.FSharp.Collections.MapModule.OfSeq(
                new[] { 
                    new Tuple<string, string>("fillColor", "#ffe6cc"),
                    new Tuple<string, string>("strokeColor", "#d79b00")
                });
            
            diagram = DiagramManipulation.updateShapeStyle(diagram, vpcId, vpcStyle);
            diagram = DiagramManipulation.updateShapeStyle(diagram, publicSubnetId, publicSubnetStyle);
            diagram = DiagramManipulation.updateShapeStyle(diagram, privateSubnetId, privateSubnetStyle);
            diagram = DiagramManipulation.updateShapeStyle(diagram, igwId, igwStyle);
            
            // Connect shapes
            diagram = DiagramManipulation.connectShapes(diagram, 0, publicSubnetId, vpcId).Item1;
            diagram = DiagramManipulation.connectShapes(diagram, 0, privateSubnetId, vpcId).Item1;
            diagram = DiagramManipulation.connectShapes(diagram, 0, igwId, publicSubnetId).Item1;
            
            SaveDiagram(diagram, fileName);
            
            return diagram;
        }
    }
} 