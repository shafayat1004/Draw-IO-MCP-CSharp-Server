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
builder.Services.AddSingleton<DrawIoService>();

var diagramsDir = Environment.GetEnvironmentVariable("DIAGRAMS_DIR") ?? 
                  Path.Combine(Directory.GetCurrentDirectory(), "diagrams");

if (!Directory.Exists(diagramsDir))
{
    Directory.CreateDirectory(diagramsDir);
}

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
});

// Add additional endpoints
app.MapGet("/", () => "DrawIO MCP Server - SSE Mode");
app.MapGet("/health", () => "OK");

app.Run();

// Define the Program class to allow WebApplicationFactory<Program> to work in tests
public partial class Program { }

// Service to manage DrawIO diagram operations
public class DrawIoService
{
    private readonly ILogger<DrawIoService> _logger;
    private readonly string _diagramsDirectory;

    public DrawIoService(ILogger<DrawIoService> logger)
    {
        _logger = logger;
        _diagramsDirectory = Environment.GetEnvironmentVariable("DIAGRAMS_DIR") ?? 
                             Path.Combine(Directory.GetCurrentDirectory(), "diagrams");
        
        if (!Directory.Exists(_diagramsDirectory))
        {
            Directory.CreateDirectory(_diagramsDirectory);
        }
    }

    public string DiagramsDirectory => _diagramsDirectory;

    public List<string> GetAllDiagrams()
    {
        return Directory.GetFiles(_diagramsDirectory, "*.drawio")
            .Select(file => Path.GetFileName(file) ?? string.Empty)
            .Where(name => !string.IsNullOrEmpty(name))
            .ToList();
    }

    public CoreTypes.Diagram GetDiagram(string fileName)
    {
        if (!fileName.EndsWith(".drawio", StringComparison.OrdinalIgnoreCase))
        {
            fileName += ".drawio";
        }
        
        string filePath = Path.Combine(_diagramsDirectory, fileName);
        
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Diagram file not found: {fileName}");
        }
        
        return CoreFileOps.loadDiagram(filePath);
    }

    public CoreTypes.Diagram CreateDiagram(string fileName)
    {
        if (!fileName.EndsWith(".drawio", StringComparison.OrdinalIgnoreCase))
        {
            fileName += ".drawio";
        }
        
        string filePath = Path.Combine(_diagramsDirectory, fileName);
        
        if (File.Exists(filePath))
        {
            throw new InvalidOperationException($"Diagram '{fileName}' already exists");
        }
        
        return CoreFileOps.createNewDiagram(filePath);
    }

    public (CoreTypes.Diagram, string) AddShape(string fileName, string value, float x, float y, float width, float height, string shape = "rectangle")
    {
        if (!fileName.EndsWith(".drawio", StringComparison.OrdinalIgnoreCase))
        {
            fileName += ".drawio";
        }
        
        string filePath = Path.Combine(_diagramsDirectory, fileName);
        
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Diagram file not found: {fileName}");
        }
        
        var diagram = CoreFileOps.loadDiagram(filePath);
        (CoreTypes.Diagram updatedDiagram, string newId) = CoreDiagramOps.addShape(diagram, 0, value, x, y, width, height, shape);
        CoreFileOps.saveDiagram(updatedDiagram, filePath);
        
        return (updatedDiagram, newId);
    }

    public (CoreTypes.Diagram, string) ConnectShapes(string fileName, string sourceId, string targetId)
    {
        if (!fileName.EndsWith(".drawio", StringComparison.OrdinalIgnoreCase))
        {
            fileName += ".drawio";
        }
        
        string filePath = Path.Combine(_diagramsDirectory, fileName);
        
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Diagram file not found: {fileName}");
        }
        
        var diagram = CoreFileOps.loadDiagram(filePath);
        (CoreTypes.Diagram updatedDiagram, string newId) = CoreDiagramOps.connectShapes(diagram, 0, sourceId, targetId);
        CoreFileOps.saveDiagram(updatedDiagram, filePath);
        
        return (updatedDiagram, newId);
    }

    public CoreTypes.Diagram DeleteShape(string fileName, string shapeId)
    {
        if (!fileName.EndsWith(".drawio", StringComparison.OrdinalIgnoreCase))
        {
            fileName += ".drawio";
        }
        
        string filePath = Path.Combine(_diagramsDirectory, fileName);
        
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Diagram file not found: {fileName}");
        }
        
        var diagram = CoreFileOps.loadDiagram(filePath);
        var updatedDiagram = CoreDiagramOps.deleteShape(diagram, 0, shapeId);
        CoreFileOps.saveDiagram(updatedDiagram, filePath);
        
        return updatedDiagram;
    }

    public CoreTypes.Diagram UpdateShape(string fileName, string shapeId, string value, float? x = null, float? y = null, float? width = null, float? height = null, string? style = null)
    {
        if (!fileName.EndsWith(".drawio", StringComparison.OrdinalIgnoreCase))
        {
            fileName += ".drawio";
        }
        
        string filePath = Path.Combine(_diagramsDirectory, fileName);
        
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Diagram file not found: {fileName}");
        }
        
        var diagram = CoreFileOps.loadDiagram(filePath);
        var updatedDiagram = CoreDiagramOps.updateShape(diagram, 0, shapeId, value, x, y, width, height, style);
        CoreFileOps.saveDiagram(updatedDiagram, filePath);
        
        return updatedDiagram;
    }

    public CoreTypes.Diagram ArrangeDiagram(string fileName, string layout = "horizontal")
    {
        if (!fileName.EndsWith(".drawio", StringComparison.OrdinalIgnoreCase))
        {
            fileName += ".drawio";
        }
        
        string filePath = Path.Combine(_diagramsDirectory, fileName);
        
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Diagram file not found: {fileName}");
        }
        
        var diagram = CoreFileOps.loadDiagram(filePath);
        var updatedDiagram = CoreDiagramOps.arrangeLayout(diagram, 0, layout);
        CoreFileOps.saveDiagram(updatedDiagram, filePath);
        
        return updatedDiagram;
    }

    public CoreTypes.Diagram GenerateVpcDiagram(string fileName)
    {
        if (!fileName.EndsWith(".drawio", StringComparison.OrdinalIgnoreCase))
        {
            fileName += ".drawio";
        }
        
        string filePath = Path.Combine(_diagramsDirectory, fileName);
        
        if (File.Exists(filePath))
        {
            throw new InvalidOperationException($"Diagram '{fileName}' already exists");
        }
        
        // Create a new diagram
        var diagram = CoreDiagramOps.createEmptyDiagram();
        
        // Add VPC components
        (CoreTypes.Diagram diagramWithVpc, string vpcId) = CoreDiagramOps.addShape(diagram, 0, "VPC", 20, 20, 400, 300, "rectangle");
        (CoreTypes.Diagram diagramWithPublicSubnet, string publicSubnetId) = CoreDiagramOps.addShape(diagramWithVpc, 0, "Public Subnet", 40, 60, 150, 120, "rectangle");
        (CoreTypes.Diagram diagramWithPrivateSubnet, string privateSubnetId) = CoreDiagramOps.addShape(diagramWithPublicSubnet, 0, "Private Subnet", 240, 60, 150, 120, "rectangle");
        (CoreTypes.Diagram diagramWithIgw, string igwId) = CoreDiagramOps.addShape(diagramWithPrivateSubnet, 0, "Internet Gateway", 180, 0, 80, 40, "ellipse");
        
        // Connect components
        (CoreTypes.Diagram diagramWithConnector1, string _) = CoreDiagramOps.connectShapes(diagramWithIgw, 0, igwId, vpcId);
        (CoreTypes.Diagram diagramWithConnector2, string _) = CoreDiagramOps.connectShapes(diagramWithConnector1, 0, vpcId, publicSubnetId);
        (CoreTypes.Diagram diagramWithConnector3, string _) = CoreDiagramOps.connectShapes(diagramWithConnector2, 0, vpcId, privateSubnetId);
        
        // Save the diagram
        CoreFileOps.saveDiagram(diagramWithConnector3, filePath);
        
        return diagramWithConnector3;
    }
}

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
        if (resourceId == "diagram-list://all")
        {
            var diagrams = drawIoService.GetAllDiagrams();
            return Task.FromResult(new Resource
            {
                Id = resourceId,
                Type = "diagram-list",
                Content = diagrams
            });
        }
        else if (resourceId.StartsWith("diagram://"))
        {
            string filename = resourceId.Substring("diagram://".Length);
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
                        { "id", cell.Id },
                        { "value", cell.Value },
                        { "style", cell.Style },
                        { "isVertex", cell.IsVertex },
                        { "isEdge", cell.IsEdge },
                        { "parent", cell.Parent }
                    };
                    
                    if (FSharpOption<string>.get_IsSome(cell.Source))
                    {
                        cellObj.Add("source", cell.Source.Value);
                    }
                    
                    if (FSharpOption<string>.get_IsSome(cell.Target))
                    {
                        cellObj.Add("target", cell.Target.Value);
                    }
                    
                    if (FSharpOption<CoreTypes.Geometry>.get_IsSome(cell.Geometry))
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
            
            return Task.FromResult(new Resource
            {
                Id = resourceId,
                Type = "diagram",
                Content = new
                {
                    modified = diagram.Modified,
                    pages = pages
                }
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
public class CreateNewDiagramTool(DrawIoService drawIoService, ILogger<CreateNewDiagramTool> logger)
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

    public override Task<object> ExecuteAsync(ToolParameters parameters)
    {
        string name = parameters.GetValue<string>("name") ?? "new-diagram.drawio";
        
        try
        {
            var diagram = drawIoService.CreateDiagram(name);
            
            return Task.FromResult<object>(new
            {
                Status = "created",
                DiagramId = $"diagram://{name}",
                FileName = name
            });
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
    };

    public override Task<object> ExecuteAsync(ToolParameters parameters)
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
            
            return Task.FromResult<object>(new
            {
                Status = "success", 
                ShapeId = newId,
                DiagramId = $"diagram://{diagram}"
            });
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

    public override Task<object> ExecuteAsync(ToolParameters parameters)
    {
        string diagram = parameters.GetValue<string>("diagram") ?? throw new ArgumentException("Diagram name is required");
        string sourceId = parameters.GetValue<string>("sourceId") ?? throw new ArgumentException("Source ID is required");
        string targetId = parameters.GetValue<string>("targetId") ?? throw new ArgumentException("Target ID is required");
        
        try
        {
            var (_, newId) = drawIoService.ConnectShapes(diagram, sourceId, targetId);
            
            return Task.FromResult<object>(new
            {
                Status = "success",
                ConnectorId = newId,
                DiagramId = $"diagram://{diagram}"
            });
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

    public override Task<object> ExecuteAsync(ToolParameters parameters)
    {
        string name = parameters.GetValue<string>("name", "vpc.drawio");
        
        try
        {
            var diagram = drawIoService.GenerateVpcDiagram(name);
            
            return Task.FromResult<object>(new
            {
                Status = "created",
                DiagramId = $"diagram://{name}",
                FileName = name
            });
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

    public override Task<object> ExecuteAsync(ToolParameters parameters)
    {
        string diagram = parameters.GetValue<string>("diagram") ?? throw new ArgumentException("Diagram name is required");
        string shapeId = parameters.GetValue<string>("shapeId") ?? throw new ArgumentException("Shape ID is required");
        
        try
        {
            drawIoService.DeleteShape(diagram, shapeId);
            
            return Task.FromResult<object>(new
            {
                Status = "success",
                Message = $"Shape {shapeId} deleted from {diagram}",
                DiagramId = $"diagram://{diagram}"
            });
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

    public override Task<object> ExecuteAsync(ToolParameters parameters)
    {
        string diagram = parameters.GetValue<string>("diagram") ?? throw new ArgumentException("Diagram name is required");
        string shapeId = parameters.GetValue<string>("shapeId") ?? throw new ArgumentException("Shape ID is required");
        string value = parameters.GetValue<string>("value") ?? string.Empty;
        
        float? x = parameters.HasValue("x") ? parameters.GetValue<float>("x") : null;
        float? y = parameters.HasValue("y") ? parameters.GetValue<float>("y") : null;
        float? width = parameters.HasValue("width") ? parameters.GetValue<float>("width") : null;
        float? height = parameters.HasValue("height") ? parameters.GetValue<float>("height") : null;
        string? style = parameters.HasValue("style") ? parameters.GetValue<string>("style") : null;
        
        try
        {
            drawIoService.UpdateShape(diagram, shapeId, value, x, y, width, height, style);
            
            return Task.FromResult<object>(new
            {
                Status = "success",
                Message = $"Shape {shapeId} updated in {diagram}",
                DiagramId = $"diagram://{diagram}"
            });
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

    public override Task<object> ExecuteAsync(ToolParameters parameters)
    {
        string diagram = parameters.GetValue<string>("diagram") ?? throw new ArgumentException("Diagram name is required");
        string shapeId = parameters.GetValue<string>("shapeId") ?? throw new ArgumentException("Shape ID is required");
        string styleName = parameters.GetValue<string>("style") ?? throw new ArgumentException("Style name is required");
        
        if (!PredefinedStyles.TryGetValue(styleName, out var styleString))
        {
            throw new ArgumentException($"Unknown style: {styleName}. Available styles: {string.Join(", ", PredefinedStyles.Keys)}");
        }
        
        try
        {
            drawIoService.UpdateShape(diagram, shapeId, value: string.Empty, null, null, null, null, styleString);
            
            return Task.FromResult<object>(new
            {
                Status = "success",
                Message = $"Style '{styleName}' applied to shape {shapeId} in {diagram}",
                DiagramId = $"diagram://{diagram}"
            });
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

    public override Task<object> ExecuteAsync(ToolParameters parameters)
    {
        string diagram = parameters.GetValue<string>("diagram") ?? throw new ArgumentException("Diagram name is required");
        string layout = parameters.GetValue<string>("layout", "horizontal");
        
        try
        {
            var updatedDiagram = drawIoService.ArrangeDiagram(diagram, layout);
            
            return Task.FromResult<object>(new
            {
                Status = "success",
                DiagramId = $"diagram://{diagram}",
                FileName = diagram
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error arranging diagram");
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