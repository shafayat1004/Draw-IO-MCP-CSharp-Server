using DrawIO.MCP.SSE;
using Microsoft.FSharp.Core;
using CoreTypes = DrawIO.MCP.Core.Types;
using CoreFileOps = DrawIO.MCP.Core.FileOperations;
using CoreDiagramOps = DrawIO.MCP.Core.DiagramManipulation;

var builder = WebApplication.CreateBuilder(args);

// Configure services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyHeader()
              .AllowAnyMethod()
              .AllowAnyOrigin();
    });
});

// Add required MCP services
builder.Services.AddMcpServer();

// Add custom services
var diagramsDir = Environment.GetEnvironmentVariable("DIAGRAMS_DIR") ?? 
                  Path.Combine(Directory.GetCurrentDirectory(), "diagrams");

if (!Directory.Exists(diagramsDir))
{
    Directory.CreateDirectory(diagramsDir);
}

builder.Services.AddSingleton<DrawIO.MCP.SSE.DrawIoService>(sp => 
    new DrawIO.MCP.SSE.DrawIoService(diagramsDir));

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseDeveloperExceptionPage();
}

app.UseRouting();
app.UseCors();

// Configure MCP server
app.UseMcp(mcpBuilder =>
{
    // Register resources
    mcpBuilder.RegisterResourceProvider<DiagramResourceProvider>();
    
    // Register tools
    mcpBuilder.RegisterTool<CreateNewDiagramTool>();
    mcpBuilder.RegisterTool<AddShapeTool>();
    mcpBuilder.RegisterTool<ConnectShapesTool>();
    mcpBuilder.RegisterTool<GenerateVpcDiagramTool>();
    mcpBuilder.RegisterTool<DeleteShapeTool>();
    mcpBuilder.RegisterTool<UpdateShapeTool>();
    mcpBuilder.RegisterTool<StyleShapeTool>();
    mcpBuilder.RegisterTool<ArrangeDiagramTool>();
    mcpBuilder.RegisterTool<RotateShapeTool>();
    mcpBuilder.RegisterTool<FlipShapeTool>();
    mcpBuilder.RegisterTool<SetDiagramBackgroundTool>();
    mcpBuilder.RegisterTool<ConnectShapesAtPointsTool>();
    mcpBuilder.RegisterTool<ListShapeTypesTool>();
});

// Add additional endpoints
app.MapGet("/", () => "DrawIO MCP Server - SSE Mode");
app.MapGet("/health", () => "OK");

app.Run();

// Define the Program class to allow WebApplicationFactory<Program> to work in tests
public partial class Program { }

// MCP Resource Provider
public class DiagramResourceProvider(DrawIO.MCP.SSE.DrawIoService drawIoService, ILogger<DiagramResourceProvider> logger)
    : ResourceProvider
{
    private readonly ILogger<DiagramResourceProvider> _logger = logger;

    public override bool CanProvide(string resourceId)
    {
        return resourceId.StartsWith("diagram://") || resourceId == "diagram-list://all";
    }

    public override Task<Resource> GetResourceAsync(string resourceId, ResourceQuery? query = null)
    {
        if (resourceId.StartsWith("diagram://"))
        {
            string filename = resourceId.Substring("diagram://".Length);
            var diagram = drawIoService.GetDiagram(filename);
            
            // Convert the diagram to a format suitable for the MCP protocol
            var pages = new List<object>();
            
            if (diagram != null)
            {
                foreach (var page in diagram.Pages)
                {
                    var cells = new List<object>();
                    
                    foreach (var cell in page.Cells)
                    {
                        var cellObj = new Dictionary<string, object>
                        {
                            ["id"] = cell.Id,
                            ["value"] = cell.Value,
                            ["style"] = cell.Style,
                            ["isVertex"] = cell.IsVertex,
                            ["isEdge"] = cell.IsEdge,
                            ["parent"] = cell.Parent
                        };
                        
                        if (Microsoft.FSharp.Core.FSharpOption<string>.get_IsSome(cell.Source))
                        {
                            cellObj["source"] = cell.Source.Value;
                        }
                        
                        if (Microsoft.FSharp.Core.FSharpOption<string>.get_IsSome(cell.Target))
                        {
                            cellObj["target"] = cell.Target.Value;
                        }
                        
                        if (Microsoft.FSharp.Core.FSharpOption<CoreTypes.Geometry>.get_IsSome(cell.Geometry))
                        {
                            var geo = cell.Geometry.Value;
                            cellObj["geometry"] = new Dictionary<string, object>
                            {
                                ["x"] = geo.Position.X,
                                ["y"] = geo.Position.Y,
                                ["width"] = geo.Size.Width,
                                ["height"] = geo.Size.Height,
                                ["relative"] = geo.Relative
                            };
                        }
                        
                        cells.Add(cellObj);
                    }
                    
                    pages.Add(new Dictionary<string, object>
                    {
                        ["id"] = page.Id,
                        ["name"] = page.Name, 
                        ["cells"] = cells
                    });
                }
            }
            
            return Task.FromResult(new Resource
            {
                Id = resourceId,
                Type = "diagram",
                Content = new Dictionary<string, object>
                {
                    ["modified"] = diagram?.Modified.ToString("o") ?? DateTime.Now.ToString("o"),
                    ["pages"] = pages
                }
            });
        }
        else if (resourceId == "diagram-list://all")
        {
            // Return a list of all diagrams
            var diagrams = drawIoService.GetAllDiagrams();
            
            return Task.FromResult(new Resource
            {
                Id = resourceId,
                Type = "diagram-list",
                Content = diagrams
            });
        }
        
        throw new ArgumentException($"Unsupported resource ID: {resourceId}");
    }

    public override Task<IEnumerable<ResourceInfo>> ListResourcesAsync(ResourceQuery? query = null)
    {
        var diagrams = drawIoService.GetAllDiagrams();
        var resources = diagrams.Select(d => new ResourceInfo 
        {
            Id = $"diagram://{d}",
            Type = "diagram",
            Title = d
        });
        
        return Task.FromResult<IEnumerable<ResourceInfo>>(resources);
    }
}

// MCP Tools
public class CreateNewDiagramTool(DrawIO.MCP.SSE.DrawIoService drawIoService, ILogger<CreateNewDiagramTool> logger)
    : Tool
{
    public override string Name => "create_new_diagram";

    public override string Description => "Create a new empty diagram file";

    public override ToolParameter[] Parameters => new[]
    {
        new ToolParameter
        {
            Name = "name",
            Type = "string",
            Description = "Name of the diagram file to create",
            Required = true
        }
    };

    public override async Task<object> ExecuteAsync(ToolParameters parameters)
    {
        string name = parameters.GetValue<string>("name") ?? throw new ArgumentException("Diagram name is required");
        
        if (string.IsNullOrEmpty(Path.GetExtension(name)))
        {
            name += ".drawio";
        }
        
        try
        {
            var diagram = drawIoService.CreateDiagram(name);
            
            var response = new
            {
                Status = "created",
                DiagramId = $"diagram://{name}",
                FileName = name
            };
            
            return await ToolExtensions.AddDiagramImageToResponseAsync(response, parameters, drawIoService);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating new diagram");
            throw;
        }
    }
}

public class AddShapeTool(DrawIO.MCP.SSE.DrawIoService drawIoService, ILogger<AddShapeTool> logger) : Tool
{
    public override string Name => "add_shape";

    public override string Description => "Add a new shape to a diagram";

    public override ToolParameter[] Parameters => new[]
    {
        new ToolParameter
        {
            Name = "diagram",
            Type = "string",
            Description = "Diagram file name",
            Required = true
        },
        new ToolParameter
        {
            Name = "value",
            Type = "string",
            Description = "Text label for the shape",
            Required = true
        },
        new ToolParameter
        {
            Name = "x",
            Type = "number",
            Description = "X position",
            Required = true
        },
        new ToolParameter
        {
            Name = "y",
            Type = "number",
            Description = "Y position",
            Required = true
        },
        new ToolParameter
        {
            Name = "width",
            Type = "number",
            Description = "Width of the shape",
            Required = true
        },
        new ToolParameter
        {
            Name = "height",
            Type = "number",
            Description = "Height of the shape",
            Required = true
        },
        new ToolParameter
        {
            Name = "shape",
            Type = "string",
            Description = "Shape type (rectangle, ellipse, etc.)",
            Required = false
        }
    }.AddCommonParameters();

    public override async Task<object> ExecuteAsync(ToolParameters parameters)
    {
        string diagram = parameters.GetValue<string>("diagram") ?? throw new ArgumentException("Diagram name is required");
        string value = parameters.GetValue<string>("value") ?? ""; 
        float x = parameters.GetValue<float>("x");
        float y = parameters.GetValue<float>("y");
        float width = parameters.GetValue<float>("width", 80);
        float height = parameters.GetValue<float>("height", 40);
        string shape = parameters.GetValue<string>("shape", "rectangle");
        
        try
        {
            var (_, newId) = drawIoService.AddShape(diagram, value, x, y, width, height, shape);
            
            var response = new
            {
                Status = "success", 
                ShapeId = newId,
                DiagramId = $"diagram://{diagram}"
            };
            
            return await ToolExtensions.AddDiagramImageToResponseAsync(response, parameters, drawIoService);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error adding shape to diagram");
            throw;
        }
    }
}

public class ConnectShapesTool(DrawIO.MCP.SSE.DrawIoService drawIoService, ILogger<ConnectShapesTool> logger)
    : Tool
{
    public override string Name => "connect_shapes";

    public override string Description => "Connect two shapes with an arrow";

    public override ToolParameter[] Parameters => new[]
    {
        new ToolParameter
        {
            Name = "diagram",
            Type = "string",
            Description = "Diagram file name",
            Required = true
        },
        new ToolParameter
        {
            Name = "sourceId",
            Type = "string",
            Description = "ID of the source shape",
            Required = true
        },
        new ToolParameter
        {
            Name = "targetId",
            Type = "string",
            Description = "ID of the target shape",
            Required = true
        }
    };

    public override async Task<object> ExecuteAsync(ToolParameters parameters)
    {
        string diagram = parameters.GetValue<string>("diagram") ?? throw new ArgumentException("Diagram name is required");
        string sourceId = parameters.GetValue<string>("sourceId") ?? throw new ArgumentException("Source ID is required");
        string targetId = parameters.GetValue<string>("targetId") ?? throw new ArgumentException("Target ID is required");
        
        try
        {
            var (_, newId) = drawIoService.ConnectShapes(diagram, sourceId, targetId);
            
            var response = new
            {
                Status = "success",
                ConnectorId = newId,
                DiagramId = $"diagram://{diagram}"
            };
            
            return await ToolExtensions.AddDiagramImageToResponseAsync(response, parameters, drawIoService);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error connecting shapes in diagram");
            throw;
        }
    }
}

public class GenerateVpcDiagramTool(DrawIO.MCP.SSE.DrawIoService drawIoService, ILogger<GenerateVpcDiagramTool> logger)
    : Tool
{
    public override string Name => "generate_vpc";

    public override string Description => "Generate a sample AWS VPC diagram";

    public override ToolParameter[] Parameters => new[]
    {
        new ToolParameter
        {
            Name = "name",
            Type = "string",
            Description = "Name for the new diagram",
            Required = true
        }
    };

    public override async Task<object> ExecuteAsync(ToolParameters parameters)
    {
        string name = parameters.GetValue<string>("name", "vpc.drawio");
        
        if (string.IsNullOrEmpty(Path.GetExtension(name)))
        {
            name += ".drawio";
        }
        
        try
        {
            var diagram = drawIoService.GenerateVpcDiagram(name);
            
            var response = new
            {
                Status = "created",
                DiagramId = $"diagram://{name}",
                FileName = name
            };
            
            return await ToolExtensions.AddDiagramImageToResponseAsync(response, parameters, drawIoService);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating VPC diagram");
            throw;
        }
    }
}

public class DeleteShapeTool(DrawIO.MCP.SSE.DrawIoService drawIoService, ILogger<DeleteShapeTool> logger)
    : Tool
{
    public override string Name => "delete_shape";

    public override string Description => "Delete a shape from a diagram";

    public override ToolParameter[] Parameters => new[]
    {
        new ToolParameter
        {
            Name = "diagram",
            Type = "string",
            Description = "Diagram file name",
            Required = true
        },
        new ToolParameter
        {
            Name = "shapeId",
            Type = "string",
            Description = "ID of the shape to delete",
            Required = true
        }
    };

    public override async Task<object> ExecuteAsync(ToolParameters parameters)
    {
        string diagram = parameters.GetValue<string>("diagram") ?? throw new ArgumentException("Diagram name is required");
        string shapeId = parameters.GetValue<string>("shapeId") ?? throw new ArgumentException("Shape ID is required");
        
        try
        {
            drawIoService.DeleteShape(diagram, shapeId);
            
            var response = new
            {
                Status = "success",
                Message = $"Shape {shapeId} deleted from {diagram}",
                DiagramId = $"diagram://{diagram}"
            };
            
            return await ToolExtensions.AddDiagramImageToResponseAsync(response, parameters, drawIoService);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting shape from diagram");
            throw;
        }
    }
}

public class UpdateShapeTool(DrawIO.MCP.SSE.DrawIoService drawIoService, ILogger<UpdateShapeTool> logger)
    : Tool
{
    public override string Name => "update_shape";

    public override string Description => "Update an existing shape in a diagram";

    public override ToolParameter[] Parameters => new[]
    {
        new ToolParameter
        {
            Name = "diagram",
            Type = "string",
            Description = "Diagram file name",
            Required = true
        },
        new ToolParameter
        {
            Name = "shapeId",
            Type = "string",
            Description = "ID of the shape to update",
            Required = true
        },
        new ToolParameter
        {
            Name = "value",
            Type = "string",
            Description = "New text label for the shape",
            Required = false
        },
        new ToolParameter
        {
            Name = "x",
            Type = "number",
            Description = "New X position",
            Required = false
        },
        new ToolParameter
        {
            Name = "y",
            Type = "number",
            Description = "New Y position",
            Required = false
        },
        new ToolParameter
        {
            Name = "width",
            Type = "number",
            Description = "New width of the shape",
            Required = false
        },
        new ToolParameter
        {
            Name = "height",
            Type = "number",
            Description = "New height of the shape",
            Required = false
        },
        new ToolParameter
        {
            Name = "style",
            Type = "string",
            Description = "New style for the shape",
            Required = false
        }
    };

    public override async Task<object> ExecuteAsync(ToolParameters parameters)
    {
        string diagram = parameters.GetValue<string>("diagram") ?? throw new ArgumentException("Diagram name is required");
        string shapeId = parameters.GetValue<string>("shapeId") ?? throw new ArgumentException("Shape ID is required");
        string value = parameters.GetValue<string>("value") ?? "";
        
        float? x = parameters.HasValue("x") ? parameters.GetValue<float>("x") : null;
        float? y = parameters.HasValue("y") ? parameters.GetValue<float>("y") : null;
        float? width = parameters.HasValue("width") ? parameters.GetValue<float>("width") : null;
        float? height = parameters.HasValue("height") ? parameters.GetValue<float>("height") : null;
        string style = parameters.GetValue<string>("style") ?? "";
        
        try
        {
            drawIoService.UpdateShape(diagram, shapeId, value, x, y, width, height, style);
            
            var response = new
            {
                Status = "success",
                Message = $"Shape {shapeId} updated in {diagram}",
                DiagramId = $"diagram://{diagram}"
            };
            
            return await ToolExtensions.AddDiagramImageToResponseAsync(response, parameters, drawIoService);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating shape in diagram");
            throw;
        }
    }
}

public class StyleShapeTool(DrawIO.MCP.SSE.DrawIoService drawIoService, ILogger<StyleShapeTool> logger)
    : Tool
{
    // Dictionary of predefined styles
    private static readonly Dictionary<string, string> PredefinedStyles = new()
    {
        { "aws-ec2", "shape=mxgraph.aws4.ec2;fillColor=#FF9900;strokeColor=#FF9900;" },
        { "aws-s3", "shape=mxgraph.aws4.s3;fillColor=#E05243;strokeColor=#E05243;" },
        { "aws-lambda", "shape=mxgraph.aws4.lambda_function;fillColor=#F58534;strokeColor=#F58534;" },
        { "aws-rds", "shape=mxgraph.aws4.rds;fillColor=#3C7FC0;strokeColor=#3C7FC0;" },
        { "azure-vm", "shape=mxgraph.azure.virtual_machine;fillColor=#0072C6;strokeColor=#0072C6;" },
        { "gcp-compute", "shape=mxgraph.gcp2.compute_engine;fillColor=#4285F4;strokeColor=#4285F4;" },
        { "database", "shape=cylinder;fillColor=#f5f5f5;strokeColor=#666666;" },
        { "server", "shape=mxgraph.rack.general.server_1;fillColor=#f5f5f5;strokeColor=#666666;" },
        { "router", "shape=mxgraph.cisco.routers.router;fillColor=#f5f5f5;strokeColor=#666666;" },
        { "switch", "shape=mxgraph.cisco.switches.layer_3_switch;fillColor=#f5f5f5;strokeColor=#666666;" },
        { "firewall", "shape=mxgraph.cisco.security.firewall;fillColor=#f5f5f5;strokeColor=#666666;" },
        { "cloud", "shape=cloud;fillColor=#F0F0F0;strokeColor=#000000;" },
        { "success", "fillColor=#d5e8d4;strokeColor=#82b366;" },
        { "warning", "fillColor=#fff2cc;strokeColor=#d6b656;" },
        { "error", "fillColor=#f8cecc;strokeColor=#b85450;" },
        { "info", "fillColor=#dae8fc;strokeColor=#6c8ebf;" }
    };

    public override string Name => "style_shape";

    public override string Description => "Apply a predefined style to a shape in a diagram";

    public override ToolParameter[] Parameters => new[]
    {
        new ToolParameter
        {
            Name = "diagram",
            Type = "string",
            Description = "Diagram file name",
            Required = true
        },
        new ToolParameter
        {
            Name = "shapeId",
            Type = "string",
            Description = "ID of the shape to style",
            Required = true
        },
        new ToolParameter
        {
            Name = "style",
            Type = "string",
            Description = "Predefined style name (aws-ec2, aws-s3, aws-lambda, aws-rds, azure-vm, gcp-compute, database, server, router, switch, firewall, cloud, success, warning, error, info)",
            Required = true
        }
    };

    public override async Task<object> ExecuteAsync(ToolParameters parameters)
    {
        string diagram = parameters.GetValue<string>("diagram") ?? throw new ArgumentException("Diagram name is required");
        string shapeId = parameters.GetValue<string>("shapeId") ?? throw new ArgumentException("Shape ID is required");
        string styleName = parameters.GetValue<string>("style") ?? throw new ArgumentException("Style name is required");
        string fillColor = parameters.GetValue<string>("fill_color") ?? "";
        string strokeColor = parameters.GetValue<string>("stroke_color") ?? "";
        
        try
        {
            if (PredefinedStyles.TryGetValue(styleName, out var styleString))
            {
                // Use predefined style
                drawIoService.UpdateShape(diagram, shapeId, "", null, null, null, null, styleString);
            }
            else
            {
                // Use custom colors
                if (string.IsNullOrEmpty(fillColor) && string.IsNullOrEmpty(strokeColor))
                {
                    throw new ArgumentException("Either a predefined style, fill color, or stroke color must be provided");
                }
                
                var styleProps = new List<string>();
                
                if (!string.IsNullOrEmpty(fillColor))
                {
                    styleProps.Add($"fillColor={fillColor}");
                }
                
                if (!string.IsNullOrEmpty(strokeColor))
                {
                    styleProps.Add($"strokeColor={strokeColor}");
                }
                
                string customStyle = string.Join(";", styleProps);
                
                drawIoService.UpdateShape(diagram, shapeId, "", null, null, null, null, customStyle);
            }
            
            var response = new
            {
                Status = "success",
                Message = $"Style '{styleName}' applied to shape {shapeId} in {diagram}",
                DiagramId = $"diagram://{diagram}"
            };
            
            return await ToolExtensions.AddDiagramImageToResponseAsync(response, parameters, drawIoService);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error applying style to shape");
            throw;
        }
    }
}

public class ArrangeDiagramTool(DrawIO.MCP.SSE.DrawIoService drawIoService, ILogger<ArrangeDiagramTool> logger)
    : Tool
{
    public override string Name => "arrange_diagram";

    public override string Description => "Arrange shapes in a diagram";

    public override ToolParameter[] Parameters => new[]
    {
        new ToolParameter
        {
            Name = "diagram",
            Type = "string",
            Description = "Diagram file name",
            Required = true
        },
        new ToolParameter
        {
            Name = "layout",
            Type = "string",
            Description = "Layout type (horizontal, vertical, etc.)",
            Required = false
        }
    };

    public override async Task<object> ExecuteAsync(ToolParameters parameters)
    {
        string diagram = parameters.GetValue<string>("diagram") ?? throw new ArgumentException("Diagram name is required");
        string layout = parameters.GetValue<string>("layout") ?? "horizontal";
        
        // Validate layout
        if (!new[] { "horizontal", "vertical", "radial" }.Contains(layout.ToLowerInvariant()))
        {
            layout = "horizontal";
        }
        
        try
        {
            var updatedDiagram = drawIoService.ArrangeDiagram(diagram, layout);
            
            var response = new
            {
                Status = "success",
                DiagramId = $"diagram://{diagram}",
                FileName = diagram
            };
            
            return await ToolExtensions.AddDiagramImageToResponseAsync(response, parameters, drawIoService);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error arranging diagram");
            throw;
        }
    }
}

public class RotateShapeTool(DrawIO.MCP.SSE.DrawIoService drawIoService, ILogger<RotateShapeTool> logger)
    : Tool
{
    public override string Name => "rotate_shape";

    public override string Description => "Rotate a shape by a specified angle";

    public override ToolParameter[] Parameters => new[]
    {
        new ToolParameter
        {
            Name = "diagram",
            Type = "string",
            Description = "Diagram filename",
            Required = true
        },
        new ToolParameter
        {
            Name = "shapeId",
            Type = "string",
            Description = "ID of the shape to rotate",
            Required = true
        },
        new ToolParameter
        {
            Name = "angle",
            Type = "number",
            Description = "Rotation angle in degrees",
            Required = true
        },
        new ToolParameter
        {
            Name = "returnDiagram",
            Type = "boolean",
            Description = "Whether to include the diagram image in the response",
            Required = false
        }
    };

    public override async Task<object> ExecuteAsync(ToolParameters parameters)
    {
        string diagram = parameters.GetValue<string>("diagram") ?? throw new ArgumentException("Diagram name is required");
        string shapeId = parameters.GetValue<string>("shapeId") ?? throw new ArgumentException("Shape ID is required");
        float angle = parameters.GetValue<float>("angle");
        
        try
        {
            drawIoService.RotateShape(diagram, shapeId, angle);
            
            var response = new
            {
                Status = "success",
                Message = $"Shape {shapeId} rotated by {angle} degrees",
                DiagramId = $"diagram://{diagram}"
            };
            
            return await ToolExtensions.AddDiagramImageToResponseAsync(response, parameters, drawIoService);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error rotating shape");
            throw;
        }
    }
}

public class FlipShapeTool(DrawIO.MCP.SSE.DrawIoService drawIoService, ILogger<FlipShapeTool> logger)
    : Tool
{
    public override string Name => "flip_shape";

    public override string Description => "Flip a shape horizontally or vertically";

    public override ToolParameter[] Parameters => new[]
    {
        new ToolParameter
        {
            Name = "diagram",
            Type = "string",
            Description = "Diagram filename",
            Required = true
        },
        new ToolParameter
        {
            Name = "shapeId",
            Type = "string",
            Description = "ID of the shape to flip",
            Required = true
        },
        new ToolParameter
        {
            Name = "direction",
            Type = "string",
            Description = "Direction to flip (horizontal or vertical)",
            Required = true
        },
        new ToolParameter
        {
            Name = "returnDiagram",
            Type = "boolean",
            Description = "Whether to include the diagram image in the response",
            Required = false
        }
    };

    public override async Task<object> ExecuteAsync(ToolParameters parameters)
    {
        string diagram = parameters.GetValue<string>("diagram") ?? throw new ArgumentException("Diagram name is required");
        string shapeId = parameters.GetValue<string>("shapeId") ?? throw new ArgumentException("Shape ID is required");
        string direction = parameters.GetValue<string>("direction") ?? throw new ArgumentException("Flip direction is required");
        
        // Validate direction
        if (!string.Equals(direction, "horizontal", StringComparison.OrdinalIgnoreCase) && 
            !string.Equals(direction, "vertical", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Direction must be either 'horizontal' or 'vertical'");
        }
        
        try
        {
            drawIoService.FlipShape(diagram, shapeId, direction);
            
            var response = new
            {
                Status = "success",
                Message = $"Shape {shapeId} flipped {direction}",
                DiagramId = $"diagram://{diagram}"
            };
            
            return await ToolExtensions.AddDiagramImageToResponseAsync(response, parameters, drawIoService);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error flipping shape");
            throw;
        }
    }
}

public class SetDiagramBackgroundTool(DrawIO.MCP.SSE.DrawIoService drawIoService, ILogger<SetDiagramBackgroundTool> logger)
    : Tool
{
    public override string Name => "set_diagram_background";

    public override string Description => "Set the background color or image for a diagram";

    public override ToolParameter[] Parameters => new[]
    {
        new ToolParameter
        {
            Name = "diagram",
            Type = "string",
            Description = "Diagram filename",
            Required = true
        },
        new ToolParameter
        {
            Name = "backgroundColor",
            Type = "string",
            Description = "Background color in hex format (e.g., #f5f5f5)",
            Required = false
        },
        new ToolParameter
        {
            Name = "backgroundImage",
            Type = "string",
            Description = "URL or path to background image",
            Required = false
        },
        new ToolParameter
        {
            Name = "returnDiagram",
            Type = "boolean",
            Description = "Whether to include the diagram image in the response",
            Required = false
        }
    };

    public override async Task<object> ExecuteAsync(ToolParameters parameters)
    {
        string diagram = parameters.GetValue<string>("diagram") ?? throw new ArgumentException("Diagram name is required");
        string backgroundColor = parameters.GetValue<string>("backgroundColor") ?? "";
        string backgroundImage = parameters.GetValue<string>("backgroundImage") ?? "";
        
        if (string.IsNullOrEmpty(backgroundColor) && string.IsNullOrEmpty(backgroundImage))
        {
            throw new ArgumentException("Either backgroundColor or backgroundImage must be provided");
        }
        
        try
        {
            drawIoService.SetDiagramBackground(diagram, backgroundImage, backgroundColor);
            
            string message = "";
            if (!string.IsNullOrEmpty(backgroundColor))
            {
                message += $"Background color set to {backgroundColor}. ";
            }
            
            if (!string.IsNullOrEmpty(backgroundImage))
            {
                message += $"Background image set to {backgroundImage}. ";
            }
            
            var response = new
            {
                Status = "success",
                Message = message.Trim()
            };
            
            return await ToolExtensions.AddDiagramImageToResponseAsync(response, parameters, drawIoService);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error setting diagram background");
            throw;
        }
    }
}

public class ConnectShapesAtPointsTool(DrawIO.MCP.SSE.DrawIoService drawIoService, ILogger<ConnectShapesAtPointsTool> logger)
    : Tool
{
    public override string Name => "connect_shapes_at_points";

    public override string Description => "Connect two shapes with an arrow at specific points";

    public override ToolParameter[] Parameters => new[]
    {
        new ToolParameter
        {
            Name = "diagram",
            Type = "string",
            Description = "Diagram filename",
            Required = true
        },
        new ToolParameter
        {
            Name = "sourceId",
            Type = "string",
            Description = "ID of the source shape",
            Required = true
        },
        new ToolParameter
        {
            Name = "targetId",
            Type = "string",
            Description = "ID of the target shape",
            Required = true
        },
        new ToolParameter
        {
            Name = "sourceX",
            Type = "number",
            Description = "X coordinate on the source shape",
            Required = false
        },
        new ToolParameter
        {
            Name = "sourceY",
            Type = "number",
            Description = "Y coordinate on the source shape",
            Required = false
        },
        new ToolParameter
        {
            Name = "targetX",
            Type = "number",
            Description = "X coordinate on the target shape",
            Required = false
        },
        new ToolParameter
        {
            Name = "targetY",
            Type = "number",
            Description = "Y coordinate on the target shape",
            Required = false
        },
        new ToolParameter
        {
            Name = "returnDiagram",
            Type = "boolean",
            Description = "Whether to include the diagram image in the response",
            Required = false
        }
    };

    public override async Task<object> ExecuteAsync(ToolParameters parameters)
    {
        string diagram = parameters.GetValue<string>("diagram") ?? throw new ArgumentException("Diagram name is required");
        string sourceId = parameters.GetValue<string>("sourceId") ?? throw new ArgumentException("Source ID is required");
        string targetId = parameters.GetValue<string>("targetId") ?? throw new ArgumentException("Target ID is required");
        
        float? sourceX = parameters.GetValue<float?>("sourceX");
        float? sourceY = parameters.GetValue<float?>("sourceY");
        float? targetX = parameters.GetValue<float?>("targetX");
        float? targetY = parameters.GetValue<float?>("targetY");
        
        try
        {
            var result = drawIoService.ConnectShapesAtPoints(
                diagram, sourceId, targetId, sourceX, sourceY, targetX, targetY);
            
            var updatedDiagram = result.Item1;
            var connectorId = result.Item2;
            
            var response = new
            {
                Status = "success",
                ConnectorId = connectorId,
                DiagramId = $"diagram://{diagram}"
            };
            
            return await ToolExtensions.AddDiagramImageToResponseAsync(response, parameters, drawIoService);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error connecting shapes at points in diagram");
            throw;
        }
    }
}

// Extension method for ToolParameters to check if a parameter has a value
public static class ToolParametersExtensions
{
    public static bool HasValue(this ToolParameters parameters, string name)
    {
        try
        {
            var value = parameters.GetValue<object>(name);
            return value != null;
        }
        catch
        {
            return false;
        }
    }
}

// Tool base class extension for the current file
public static class ToolExtensions
{
    // Helper method to add diagram image to response if requested
    public static async Task<object> AddDiagramImageToResponseAsync(
        object response, 
        ToolParameters parameters, 
        DrawIO.MCP.SSE.DrawIoService drawIoService)
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

// Add a ListShapeTypesTool class similar to other tool classes
#pragma warning disable CS9113 // Parameter is unread
public class ListShapeTypesTool(DrawIO.MCP.SSE.DrawIoService _, ILogger<ListShapeTypesTool> logger)
    : Tool
#pragma warning restore CS9113
{
    private readonly ILogger<ListShapeTypesTool> _logger = logger;

    public override string Name => "list_shape_types";

    public override string Description => "List available shape types that can be used with add_shape";

    public override ToolParameter[] Parameters => Array.Empty<ToolParameter>();

    public override Task<object> ExecuteAsync(ToolParameters parameters)
    {
        try
        {
            var shapeTypes = new Dictionary<string, List<string>>
            {
                ["Basic"] = new() { "rectangle", "ellipse", "circle", "triangle", "rhombus", "hexagon" },
                ["Flowchart"] = new() { "decision", "data", "predefined", "stored-data", "process" },
                ["UML"] = new() { "class", "interface", "package", "actor" },
                ["Network"] = new() { "server", "database", "cloud", "cloud-service" },
                ["Containers"] = new() { "document", "note", "cylinder", "diamond" }
            };

            return Task.FromResult<object>(new
            {
                status = "success",
                shapeCategories = shapeTypes
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing shape types");
            return Task.FromResult<object>(new
            {
                status = "error",
                message = ex.Message
            });
        }
    }
}