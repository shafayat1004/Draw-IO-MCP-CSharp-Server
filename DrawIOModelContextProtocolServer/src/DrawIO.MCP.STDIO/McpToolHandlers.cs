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
    /// Provides extension methods for McpRequestDispatcher for tool handling
    /// </summary>
    public static class McpToolHandlers
    {
        /// <summary>
        /// Lists available tools
        /// </summary>
        public static Task<object> ListToolsAsync(this McpRequestDispatcher dispatcher, JsonElement parameters, TextWriter logWriter, bool verbose)
        {
            if (verbose)
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                logWriter.WriteLine($"[{timestamp}] INFO: Listing tools");
            }
            
            var tools = new List<object>
            {
                new {
                    name = "create_new_diagram",
                    description = "Create a new empty diagram file",
                    inputSchema = new {
                        type = "object",
                        properties = new {
                            name = new { type = "string", description = "Name of the diagram to create (will add .drawio if missing)" }
                        },
                        required = new[] { "name" }
                    }
                },
                new {
                    name = "generate_vpc",
                    description = "Generate a sample AWS VPC layout diagram",
                    inputSchema = new {
                        type = "object",
                        properties = new {
                            diagram_name = new { 
                                type = "string",
                                description = "Name for the diagram file (will add .drawio if missing)"
                            }
                        },
                        required = new[] { "diagram_name" }
                    }
                },
                new {
                    name = "add_shape",
                    description = "Add a new shape to a diagram",
                    inputSchema = new {
                        type = "object",
                        properties = new {
                            diagram = new { type = "string", description = "Diagram filename" },
                            value = new { type = "string", description = "Shape label/text" },
                            x = new { type = "number", description = "X position" },
                            y = new { type = "number", description = "Y position" },
                            width = new { type = "number", description = "Width of shape" },
                            height = new { type = "number", description = "Height of shape" },
                            shape = new { type = "string", description = "Shape type (rectangle, ellipse, etc.)" }
                        },
                        required = new[] { "diagram", "value", "x", "y", "width", "height" }
                    }
                },
                new {
                    name = "connect_shapes",
                    description = "Connect two shapes with an arrow",
                    inputSchema = new {
                        type = "object",
                        properties = new {
                            diagram = new { type = "string", description = "Diagram filename" },
                            source_id = new { type = "string", description = "ID of the source shape" },
                            target_id = new { type = "string", description = "ID of the target shape" }
                        },
                        required = new[] { "diagram", "source_id", "target_id" }
                    }
                },
                new {
                    name = "get_diagram_image",
                    description = "Get a diagram as a base64-encoded image",
                    inputSchema = new {
                        type = "object",
                        properties = new {
                            diagram = new { type = "string", description = "Diagram filename" },
                            page = new { type = "integer", description = "Page index (defaults to 0)" },
                            format = new { type = "string", description = "Image format (png, jpeg, etc.)" }
                        },
                        required = new[] { "diagram" }
                    }
                },
                new {
                    name = "delete_shape",
                    description = "Delete a shape from a diagram",
                    inputSchema = new {
                        type = "object",
                        properties = new {
                            diagram = new { type = "string", description = "Diagram filename" },
                            shape_id = new { type = "string", description = "ID of the shape to delete" }
                        },
                        required = new[] { "diagram", "shape_id" }
                    }
                },
                new {
                    name = "update_shape",
                    description = "Update a shape's properties in a diagram",
                    inputSchema = new {
                        type = "object",
                        properties = new {
                            diagram = new { type = "string", description = "Diagram filename" },
                            shape_id = new { type = "string", description = "ID of the shape to update" },
                            value = new { type = "string", description = "New label/text for the shape" }
                        },
                        required = new[] { "diagram", "shape_id", "value" }
                    }
                },
                new {
                    name = "style_shape",
                    description = "Apply style to a shape in a diagram",
                    inputSchema = new {
                        type = "object",
                        properties = new {
                            diagram = new { type = "string", description = "Diagram filename" },
                            shape_id = new { type = "string", description = "ID of the shape to style" },
                            fill_color = new { type = "string", description = "Fill color (hex format)" },
                            stroke_color = new { type = "string", description = "Stroke color (hex format)" }
                        },
                        required = new[] { "diagram", "shape_id" }
                    }
                },
                new {
                    name = "arrange_diagram",
                    description = "Auto-arrange the layout of a diagram",
                    inputSchema = new {
                        type = "object",
                        properties = new {
                            diagram = new { type = "string", description = "Diagram filename" },
                            layout = new { type = "string", description = "Layout algorithm to use (horizontal, vertical, radial)" }
                        },
                        required = new[] { "diagram", "layout" }
                    }
                },
                new {
                    name = "move_shape",
                    description = "Move a shape to a new position in the diagram",
                    inputSchema = new {
                        type = "object",
                        properties = new {
                            diagram = new { type = "string", description = "Diagram filename" },
                            shape_id = new { type = "string", description = "ID of the shape to move" },
                            x = new { type = "number", description = "New X position" },
                            y = new { type = "number", description = "New Y position" }
                        },
                        required = new[] { "diagram", "shape_id", "x", "y" }
                    }
                },
                new {
                    name = "update_shape_style",
                    description = "Update a shape's style properties",
                    inputSchema = new {
                        type = "object",
                        properties = new {
                            diagram = new { type = "string", description = "Diagram filename" },
                            shape_id = new { type = "string", description = "ID of the shape to update" },
                            style_properties = new { type = "object", description = "Style properties to update" }
                        },
                        required = new[] { "diagram", "shape_id", "style_properties" }
                    }
                },
                new {
                    name = "create_diagram_page",
                    description = "Create a new page in a diagram",
                    inputSchema = new {
                        type = "object",
                        properties = new {
                            diagram = new { type = "string", description = "Diagram filename" },
                            name = new { type = "string", description = "Name for the new page" }
                        },
                        required = new[] { "diagram", "name" }
                    }
                },
                new {
                    name = "get_diagram_page",
                    description = "Get the details of a diagram page",
                    inputSchema = new {
                        type = "object",
                        properties = new {
                            diagram = new { type = "string", description = "Diagram filename" },
                            page_index = new { type = "integer", description = "Index of the page to retrieve" }
                        },
                        required = new[] { "diagram", "page_index" }
                    }
                },
                new {
                    name = "update_diagram_page",
                    description = "Update a diagram page's properties",
                    inputSchema = new {
                        type = "object",
                        properties = new {
                            diagram = new { type = "string", description = "Diagram filename" },
                            page_index = new { type = "integer", description = "Index of the page to update" },
                            name = new { type = "string", description = "New name for the page" }
                        },
                        required = new[] { "diagram", "page_index", "name" }
                    }
                },
                new {
                    name = "delete_diagram_page",
                    description = "Delete a page from a diagram",
                    inputSchema = new {
                        type = "object",
                        properties = new {
                            diagram = new { type = "string", description = "Diagram filename" },
                            page_index = new { type = "integer", description = "Index of the page to delete" }
                        },
                        required = new[] { "diagram", "page_index" }
                    }
                },
                new {
                    name = "move_cell_between_pages",
                    description = "Move a cell (shape or connector) from one page to another",
                    inputSchema = new {
                        type = "object",
                        properties = new {
                            diagram = new { type = "string", description = "Diagram filename" },
                            shape_id = new { type = "string", description = "ID of the cell to move" },
                            source_page_index = new { type = "integer", description = "Index of the source page" },
                            target_page_index = new { type = "integer", description = "Index of the target page" }
                        },
                        required = new[] { "diagram", "shape_id", "source_page_index", "target_page_index" }
                    }
                }
            };
            
            return Task.FromResult<object>(new
            {
                tools = tools.ToArray()
            });
        }
    }
} 