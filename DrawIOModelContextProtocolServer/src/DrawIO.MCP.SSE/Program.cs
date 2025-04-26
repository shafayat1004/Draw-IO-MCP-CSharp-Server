using System.Reflection;
using DrawIO.MCP.SSE;
using Microsoft.FSharp.Core;
using CoreTypes = DrawIO.MCP.Core.Types;

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

builder.Services.AddSingleton<DrawIoService>(_ => 
    new DrawIoService(diagramsDir));

// Register tool services - find all classes that implement Tool
builder.Services.AddSingleton<DrawIoService>();
builder.Services.AddSingleton<AddShapeTool>();
builder.Services.AddSingleton<ConnectShapesTool>();
builder.Services.AddSingleton<GetDiagramImageTool>();
// Add each tool class explicitly instead of using Scrutor scanning

// Add a tool parameter transformer to ensure return_diagram is required
builder.Services.AddTransient<IStartupFilter, ToolParameterStartupFilter>();

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
    mcpBuilder.RegisterTool<GetDiagramImageTool>();
});

// Add additional endpoints
app.MapGet("/", () => "DrawIO MCP Server - SSE Mode");
app.MapGet("/health", () => "OK");

app.Run();

// Define the Program class to allow WebApplicationFactory<Program> to work in tests
public partial class Program { }

// MCP Resource Provider
public class DiagramResourceProvider(DrawIoService drawIoService, ILogger<DiagramResourceProvider> logger)
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
            var filename = resourceId.Substring("diagram://".Length);
            var diagram = drawIoService.GetDiagram(filename);
            
            // Convert the diagram to a format suitable for the MCP protocol
            var pages = new List<object>();

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
                        
                    if (FSharpOption<string>.get_IsSome(cell.Source))
                    {
                        cellObj["source"] = cell.Source.Value;
                    }
                        
                    if (FSharpOption<string>.get_IsSome(cell.Target))
                    {
                        cellObj["target"] = cell.Target.Value;
                    }
                        
                    if (FSharpOption<CoreTypes.Geometry>.get_IsSome(cell.Geometry))
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

            return Task.FromResult(new Resource
            {
                Id = resourceId,
                Type = "diagram",
                Content = new Dictionary<string, object>
                {
                    ["modified"] = diagram.Modified.ToString("o"),
                    ["pages"] = pages
                }
            });
        }

        if (resourceId == "diagram-list://all")
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
        
        return Task.FromResult(resources);
    }
}

// MCP Tools
public class CreateNewDiagramTool(DrawIoService drawIoService, ILogger<CreateNewDiagramTool> logger)
    : Tool
{
    public override string Name => "create_new_diagram";

    public override string Description => "Create a new empty diagram file";

    public override ToolParameter[] Parameters =>
    [
        new()
        {
            Name = "name",
            Type = "string",
            Description = "Name of the diagram file to create",
            Required = true
        }
    ];

    public override async Task<object> ExecuteAsync(ToolParameters parameters)
    {
        var name = parameters.GetValue<string>("name") ?? throw new ArgumentException("Diagram name is required");
        
        if (string.IsNullOrEmpty(Path.GetExtension(name)))
        {
            name += ".drawio";
        }
        
        try
        {
            drawIoService.CreateDiagram(name);
            
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

public class AddShapeTool(DrawIoService drawIoService, ILogger<AddShapeTool> logger) : Tool
{
    public override string Name => "add_shape";

    public override string Description => "Add a new shape to a diagram";

    public override ToolParameter[] Parameters => _parameters ??=
    [
        new ToolParameter
        {
            Name = "diagram",
            Type = "string",
            Description = "Diagram filename",
            Required = true
        },
        new ToolParameter
        {
            Name = "value",
            Type = "string",
            Description = "Shape label/text",
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
            Description = "Width of shape",
            Required = true
        },
        new ToolParameter
        {
            Name = "height",
            Type = "number",
            Description = "Height of shape",
            Required = true
        },
        new ToolParameter
        {
            Name = "shape",
            Type = "string",
            Description = "Shape type (rectangle, ellipse, etc.)",
            Required = false
        },
        new ToolParameter
        {
            Name = "returnDiagram",
            Type = "boolean",
            Description = "Whether to include the diagram image in the response",
            Required = true
        }
    ];

    public override async Task<object> ExecuteAsync(ToolParameters parameters)
    {
        var diagram = parameters.GetValue<string>("diagram") ?? throw new ArgumentException("Diagram name is required");
        var value = parameters.GetValue<string>("value") ?? ""; 
        var x = parameters.GetValue<float>("x");
        var y = parameters.GetValue<float>("y");
        var width = parameters.GetValue<float>("width", 80);
        var height = parameters.GetValue<float>("height", 40);
        var shape = parameters.GetValue<string>("shape", "rectangle");
        
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

public class ConnectShapesTool(DrawIoService drawIoService, ILogger<ConnectShapesTool> logger)
    : Tool
{
    public override string Name => "connect_shapes";

    public override string Description => "Connect two shapes with an arrow";

    public override ToolParameter[] Parameters => _parameters ??=
    [
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
            Name = "returnDiagram",
            Type = "boolean",
            Description = "Whether to include the diagram image in the response",
            Required = true
        }
    ];

    public override async Task<object> ExecuteAsync(ToolParameters parameters)
    {
        var diagram = parameters.GetValue<string>("diagram") ?? throw new ArgumentException("Diagram name is required");
        var sourceId = parameters.GetValue<string>("sourceId") ?? throw new ArgumentException("Source ID is required");
        var targetId = parameters.GetValue<string>("targetId") ?? throw new ArgumentException("Target ID is required");
        
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

public class GenerateVpcDiagramTool(DrawIoService drawIoService, ILogger<GenerateVpcDiagramTool> logger)
    : Tool
{
    public override string Name => "generate_vpc";

    public override string Description => "Generate a sample AWS VPC diagram";

    public override ToolParameter[] Parameters =>
    [
        new()
        {
            Name = "name",
            Type = "string",
            Description = "Name for the new diagram",
            Required = true
        }
    ];

    public override async Task<object> ExecuteAsync(ToolParameters parameters)
    {
        var name = parameters.GetValue<string>("name", "vpc.drawio");
        
        if (string.IsNullOrEmpty(Path.GetExtension(name)))
        {
            name += ".drawio";
        }
        
        try
        {
            drawIoService.GenerateVpcDiagram(name);
            
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

public class DeleteShapeTool(DrawIoService drawIoService, ILogger<DeleteShapeTool> logger)
    : Tool
{
    public override string Name => "delete_shape";

    public override string Description => "Delete a shape from a diagram";

    public override ToolParameter[] Parameters =>
    [
        new()
        {
            Name = "diagram",
            Type = "string",
            Description = "Diagram file name",
            Required = true
        },
        new()
        {
            Name = "shapeId",
            Type = "string",
            Description = "ID of the shape to delete",
            Required = true
        }
    ];

    public override async Task<object> ExecuteAsync(ToolParameters parameters)
    {
        var diagram = parameters.GetValue<string>("diagram") ?? throw new ArgumentException("Diagram name is required");
        var shapeId = parameters.GetValue<string>("shapeId") ?? throw new ArgumentException("Shape ID is required");
        
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

public class UpdateShapeTool(DrawIoService drawIoService, ILogger<UpdateShapeTool> logger)
    : Tool
{
    public override string Name => "update_shape";

    public override string Description => "Update an existing shape in a diagram";

    public override ToolParameter[] Parameters =>
    [
        new()
        {
            Name = "diagram",
            Type = "string",
            Description = "Diagram file name",
            Required = true
        },
        new()
        {
            Name = "shapeId",
            Type = "string",
            Description = "ID of the shape to update",
            Required = true
        },
        new()
        {
            Name = "value",
            Type = "string",
            Description = "New text label for the shape",
            Required = false
        },
        new()
        {
            Name = "x",
            Type = "number",
            Description = "New X position",
            Required = false
        },
        new()
        {
            Name = "y",
            Type = "number",
            Description = "New Y position",
            Required = false
        },
        new()
        {
            Name = "width",
            Type = "number",
            Description = "New width of the shape",
            Required = false
        },
        new()
        {
            Name = "height",
            Type = "number",
            Description = "New height of the shape",
            Required = false
        },
        new()
        {
            Name = "style",
            Type = "string",
            Description = "New style for the shape",
            Required = false
        }
    ];

    public override async Task<object> ExecuteAsync(ToolParameters parameters)
    {
        var diagram = parameters.GetValue<string>("diagram") ?? throw new ArgumentException("Diagram name is required");
        var shapeId = parameters.GetValue<string>("shapeId") ?? throw new ArgumentException("Shape ID is required");
        var value = parameters.GetValue<string>("value") ?? "";
        
        float? x = parameters.HasValue("x") ? parameters.GetValue<float>("x") : null;
        float? y = parameters.HasValue("y") ? parameters.GetValue<float>("y") : null;
        float? width = parameters.HasValue("width") ? parameters.GetValue<float>("width") : null;
        float? height = parameters.HasValue("height") ? parameters.GetValue<float>("height") : null;
        var style = parameters.GetValue<string>("style") ?? "";
        
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

public class StyleShapeTool(DrawIoService drawIoService, ILogger<StyleShapeTool> logger)
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

    public override ToolParameter[] Parameters =>
    [
        new()
        {
            Name = "diagram",
            Type = "string",
            Description = "Diagram file name",
            Required = true
        },
        new()
        {
            Name = "shapeId",
            Type = "string",
            Description = "ID of the shape to style",
            Required = true
        },
        new()
        {
            Name = "style",
            Type = "string",
            Description = "Predefined style name (aws-ec2, aws-s3, aws-lambda, aws-rds, azure-vm, gcp-compute, database, server, router, switch, firewall, cloud, success, warning, error, info)",
            Required = true
        }
    ];

    public override async Task<object> ExecuteAsync(ToolParameters parameters)
    {
        var diagram = parameters.GetValue<string>("diagram") ?? throw new ArgumentException("Diagram name is required");
        var shapeId = parameters.GetValue<string>("shapeId") ?? throw new ArgumentException("Shape ID is required");
        var styleName = parameters.GetValue<string>("style") ?? throw new ArgumentException("Style name is required");
        var fillColor = parameters.GetValue<string>("fill_color") ?? "";
        var strokeColor = parameters.GetValue<string>("stroke_color") ?? "";
        
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
                
                var customStyle = string.Join(";", styleProps);
                
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

public class ArrangeDiagramTool(DrawIoService drawIoService, ILogger<ArrangeDiagramTool> logger)
    : Tool
{
    public override string Name => "arrange_diagram";

    public override string Description => "Arrange shapes in a diagram";

    public override ToolParameter[] Parameters =>
    [
        new()
        {
            Name = "diagram",
            Type = "string",
            Description = "Diagram file name",
            Required = true
        },
        new()
        {
            Name = "layout",
            Type = "string",
            Description = "Layout type (horizontal, vertical, etc.)",
            Required = false
        }
    ];

    public override async Task<object> ExecuteAsync(ToolParameters parameters)
    {
        var diagram = parameters.GetValue<string>("diagram") ?? throw new ArgumentException("Diagram name is required");
        var layout = parameters.GetValue<string>("layout") ?? "horizontal";
        
        // Validate layout
        if (!new[] { "horizontal", "vertical", "radial" }.Contains(layout.ToLowerInvariant()))
        {
            layout = "horizontal";
        }
        
        try
        {
            drawIoService.ArrangeDiagram(diagram, layout);
            
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

public class RotateShapeTool(DrawIoService drawIoService, ILogger<RotateShapeTool> logger)
    : Tool
{
    public override string Name => "rotate_shape";

    public override string Description => "Rotate a shape by a specified angle";

    public override ToolParameter[] Parameters =>
    [
        new()
        {
            Name = "diagram",
            Type = "string",
            Description = "Diagram filename",
            Required = true
        },
        new()
        {
            Name = "shapeId",
            Type = "string",
            Description = "ID of the shape to rotate",
            Required = true
        },
        new()
        {
            Name = "angle",
            Type = "number",
            Description = "Rotation angle in degrees",
            Required = true
        },
        new()
        {
            Name = "returnDiagram",
            Type = "boolean",
            Description = "Whether to include the diagram image in the response",
            Required = false
        }
    ];

    public override async Task<object> ExecuteAsync(ToolParameters parameters)
    {
        var diagram = parameters.GetValue<string>("diagram") ?? throw new ArgumentException("Diagram name is required");
        var shapeId = parameters.GetValue<string>("shapeId") ?? throw new ArgumentException("Shape ID is required");
        var angle = parameters.GetValue<float>("angle");
        
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

public class FlipShapeTool(DrawIoService drawIoService, ILogger<FlipShapeTool> logger)
    : Tool
{
    public override string Name => "flip_shape";

    public override string Description => "Flip a shape horizontally or vertically";

    public override ToolParameter[] Parameters =>
    [
        new()
        {
            Name = "diagram",
            Type = "string",
            Description = "Diagram filename",
            Required = true
        },
        new()
        {
            Name = "shapeId",
            Type = "string",
            Description = "ID of the shape to flip",
            Required = true
        },
        new()
        {
            Name = "direction",
            Type = "string",
            Description = "Direction to flip (horizontal or vertical)",
            Required = true
        },
        new()
        {
            Name = "returnDiagram",
            Type = "boolean",
            Description = "Whether to include the diagram image in the response",
            Required = false
        }
    ];

    public override async Task<object> ExecuteAsync(ToolParameters parameters)
    {
        var diagram = parameters.GetValue<string>("diagram") ?? throw new ArgumentException("Diagram name is required");
        var shapeId = parameters.GetValue<string>("shapeId") ?? throw new ArgumentException("Shape ID is required");
        var direction = parameters.GetValue<string>("direction") ?? throw new ArgumentException("Flip direction is required");
        
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

public class SetDiagramBackgroundTool(DrawIoService drawIoService, ILogger<SetDiagramBackgroundTool> logger)
    : Tool
{
    public override string Name => "set_diagram_background";

    public override string Description => "Set the background color or image for a diagram";

    public override ToolParameter[] Parameters =>
    [
        new()
        {
            Name = "diagram",
            Type = "string",
            Description = "Diagram filename",
            Required = true
        },
        new()
        {
            Name = "backgroundColor",
            Type = "string",
            Description = "Background color in hex format (e.g., #f5f5f5)",
            Required = false
        },
        new()
        {
            Name = "backgroundImage",
            Type = "string",
            Description = "URL or path to background image",
            Required = false
        },
        new()
        {
            Name = "returnDiagram",
            Type = "boolean",
            Description = "Whether to include the diagram image in the response",
            Required = false
        }
    ];

    public override async Task<object> ExecuteAsync(ToolParameters parameters)
    {
        var diagram = parameters.GetValue<string>("diagram") ?? throw new ArgumentException("Diagram name is required");
        var backgroundColor = parameters.GetValue<string>("backgroundColor") ?? "";
        var backgroundImage = parameters.GetValue<string>("backgroundImage") ?? "";
        
        if (string.IsNullOrEmpty(backgroundColor) && string.IsNullOrEmpty(backgroundImage))
        {
            throw new ArgumentException("Either backgroundColor or backgroundImage must be provided");
        }
        
        try
        {
            drawIoService.SetDiagramBackground(diagram, backgroundImage, backgroundColor);
            
            var message = "";
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

public class ConnectShapesAtPointsTool(DrawIoService drawIoService, ILogger<ConnectShapesAtPointsTool> logger)
    : Tool
{
    public override string Name => "connect_shapes_at_points";

    public override string Description => "Connect two shapes with an arrow at specific points";

    public override ToolParameter[] Parameters =>
    [
        new()
        {
            Name = "diagram",
            Type = "string",
            Description = "Diagram filename",
            Required = true
        },
        new()
        {
            Name = "sourceId",
            Type = "string",
            Description = "ID of the source shape",
            Required = true
        },
        new()
        {
            Name = "targetId",
            Type = "string",
            Description = "ID of the target shape",
            Required = true
        },
        new()
        {
            Name = "sourceX",
            Type = "number",
            Description = "X coordinate on the source shape",
            Required = false
        },
        new()
        {
            Name = "sourceY",
            Type = "number",
            Description = "Y coordinate on the source shape",
            Required = false
        },
        new()
        {
            Name = "targetX",
            Type = "number",
            Description = "X coordinate on the target shape",
            Required = false
        },
        new()
        {
            Name = "targetY",
            Type = "number",
            Description = "Y coordinate on the target shape",
            Required = false
        },
        new()
        {
            Name = "returnDiagram",
            Type = "boolean",
            Description = "Whether to include the diagram image in the response",
            Required = false
        }
    ];

    public override async Task<object> ExecuteAsync(ToolParameters parameters)
    {
        var diagram = parameters.GetValue<string>("diagram") ?? throw new ArgumentException("Diagram name is required");
        var sourceId = parameters.GetValue<string>("sourceId") ?? throw new ArgumentException("Source ID is required");
        var targetId = parameters.GetValue<string>("targetId") ?? throw new ArgumentException("Target ID is required");
        
        var sourceX = parameters.GetValue<float?>("sourceX");
        var sourceY = parameters.GetValue<float?>("sourceY");
        var targetX = parameters.GetValue<float?>("targetX");
        var targetY = parameters.GetValue<float?>("targetY");
        
        try
        {
            var result = drawIoService.ConnectShapesAtPoints(
                diagram, sourceId, targetId, sourceX, sourceY, targetX, targetY);

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
        DrawIoService drawIoService)
    {
        // If return_diagram is true and diagram parameter exists, add image to response
        var returnDiagram = parameters.GetValue<bool>("return_diagram");
        var diagramName = parameters.GetValue<string>("diagram");
        
        if (returnDiagram && !string.IsNullOrEmpty(diagramName))
        {
            // Get page index if specified
            var pageIndex = parameters.GetValue("page_index", 
                          parameters.GetValue("page", 0));
            
            // Get image data
            var imageData = await Task.FromResult(drawIoService.GetDiagramImageAsBase64(diagramName, pageIndex));

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
                        else
                        {
                            // Create new content with original and image
                            respDict["content"] = new List<object> { existingContent, imageData };
                        }
                    }
                    else
                    {
                        // Add new content with just the image
                        respDict["content"] = new List<object> { imageData };
                    }
                    
                    return respDict;
                }

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
        
        return response;
    }
}

// Add a ListShapeTypesTool class similar to other tool classes
#pragma warning disable CS9113 // Parameter is unread
public class ListShapeTypesTool(DrawIoService _, ILogger<ListShapeTypesTool> logger)
    : Tool
#pragma warning restore CS9113
{
    private readonly ILogger<ListShapeTypesTool> _logger = logger;

    public override string Name => "list_shape_types";

    public override string Description => "List available shape types that can be used with add_shape";

    public override ToolParameter[] Parameters => [];

    public override Task<object> ExecuteAsync(ToolParameters parameters)
    {
        try
        {
            var shapeTypes = new Dictionary<string, List<string>>
            {
                ["Basic"] = ["rectangle", "ellipse", "circle", "triangle", "rhombus", "hexagon"],
                ["Flowchart"] = ["decision", "data", "predefined", "stored-data", "process"],
                ["UML"] = ["class", "interface", "package", "actor"],
                ["Network"] = ["server", "database", "cloud", "cloud-service"],
                ["Containers"] = ["document", "note", "cylinder", "diamond"]
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

// Add this class at the end of the file
public class ToolParameterStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            using (var scope = app.ApplicationServices.CreateScope())
            {
                var tools = scope.ServiceProvider.GetServices<Tool>().ToList();
                
                // Make return_diagram a required parameter for all tools that have diagram parameter
                foreach (var tool in tools)
                {
                    // Skip get_diagram_image tool
                    if (tool.Name == "get_diagram_image")
                        continue;
                        
                    var parameters = tool.Parameters.ToList();
                    
                    // Check if tool has diagram parameter
                    if (parameters.Any(p => p.Name == "diagram"))
                    {
                        var parametersUpdated = false;
                        
                        // Check if tool already has return_diagram parameter
                        for (var i = 0; i < parameters.Count; i++)
                        {
                            if (parameters[i].Name == "returnDiagram")
                            {
                                // Create a new ToolParameter with Required=true
                                parameters[i] = new ToolParameter
                                {
                                    Name = "returnDiagram",
                                    Type = parameters[i].Type,
                                    Description = parameters[i].Description,
                                    Required = true
                                };
                                parametersUpdated = true;
                                break;
                            }
                        }
                        
                        if (!parametersUpdated)
                        {
                            // Add return_diagram parameter
                            parameters.Add(new ToolParameter
                            {
                                Name = "returnDiagram",
                                Type = "boolean",
                                Description = "Whether to include the diagram image in the response",
                                Required = true
                            });
                        }
                        
                        // Now we can directly access the _parameters field
                        var parametersField = typeof(Tool).GetField("_parameters", 
                            BindingFlags.NonPublic | 
                            BindingFlags.Instance);
                            
                        if (parametersField != null)
                        {
                            parametersField.SetValue(tool, parameters.ToArray());
                        }
                    }
                }
            }
            
            next(app);
        };
    }
}

public class GetDiagramImageTool(DrawIoService drawIoService, ILogger<GetDiagramImageTool> logger) : Tool
{
    public override string Name => "get_diagram_image";

    public override string Description => "Get a diagram as a base64-encoded image";

    public override ToolParameter[] Parameters => _parameters ??=
    [
        new ToolParameter
        {
            Name = "diagram",
            Type = "string",
            Description = "Diagram filename",
            Required = true
        },
        new ToolParameter
        {
            Name = "format",
            Type = "string",
            Description = "Image format (png, jpeg, etc.)",
            Required = false
        },
        new ToolParameter
        {
            Name = "page",
            Type = "integer",
            Description = "Page index (defaults to 0)",
            Required = false
        }
    ];

    public override async Task<object> ExecuteAsync(ToolParameters parameters)
    {
        try
        {
            var diagramName = parameters.GetValue<string>("diagram") ?? throw new ArgumentNullException("diagram", "Diagram name is required");
            var format = parameters.GetValue<string>("format", "png");
            var pageIndex = parameters.GetValue("page", 0);
            
            logger.LogInformation($"Getting diagram image for {diagramName}, page {pageIndex}, format {format}");
            
            var imageData = await Task.FromResult(drawIoService.GetDiagramImageAsBase64(diagramName, pageIndex, format));

            return new Dictionary<string, object>
            {
                ["status"] = "success",
                ["content"] = new[]
                {
                    imageData
                }
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Error in get_diagram_image tool: {ex.Message}");
            
            return new Dictionary<string, object>
            {
                ["isError"] = true,
                ["content"] = new[]
                {
                    new Dictionary<string, string>
                    {
                        ["type"] = "text",
                        ["text"] = $"Error: {ex.Message}"
                    }
                }
            };
        }
    }
}