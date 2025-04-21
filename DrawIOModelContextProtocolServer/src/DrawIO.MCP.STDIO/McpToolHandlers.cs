using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
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
            var tools = new List<McpToolDefinition>
            {
                // Essential creation and editing tools
                new McpToolDefinition
                {
                    Name = "create_new_diagram",
                    Description = "Create a new empty diagram file",
                    SchemaInputs = new Dictionary<string, McpParameterDefinition>
                    {
                        {
                            "name",
                            new McpParameterDefinition
                            {
                                Type = "string",
                                Description = "Name of the diagram to create (will add .drawio if missing)",
                                Required = true
                            }
                        }
                    }
                },
                new McpToolDefinition
                {
                    Name = "generate_vpc",
                    Description = "Generate a sample AWS VPC layout diagram",
                    SchemaInputs = new Dictionary<string, McpParameterDefinition>
                    {
                        {
                            "diagram_name",
                            new McpParameterDefinition
                            {
                                Type = "string",
                                Description = "Name for the diagram file (will add .drawio if missing)",
                                Required = true
                            }
                        }
                    }
                },
                new McpToolDefinition
                {
                    Name = "add_shape",
                    Description = "Add a new shape to a diagram",
                    SchemaInputs = new Dictionary<string, McpParameterDefinition>
                    {
                        {
                            "diagram",
                            new McpParameterDefinition
                            {
                                Type = "string",
                                Description = "Diagram filename",
                                Required = true
                            }
                        },
                        {
                            "value",
                            new McpParameterDefinition
                            {
                                Type = "string",
                                Description = "Shape label/text",
                                Required = true
                            }
                        },
                        {
                            "x",
                            new McpParameterDefinition
                            {
                                Type = "number",
                                Description = "X position",
                                Required = true
                            }
                        },
                        {
                            "y",
                            new McpParameterDefinition
                            {
                                Type = "number",
                                Description = "Y position",
                                Required = true
                            }
                        },
                        {
                            "width",
                            new McpParameterDefinition
                            {
                                Type = "number",
                                Description = "Width of shape",
                                Required = true
                            }
                        },
                        {
                            "height",
                            new McpParameterDefinition
                            {
                                Type = "number",
                                Description = "Height of shape",
                                Required = true
                            }
                        },
                        {
                            "shape",
                            new McpParameterDefinition
                            {
                                Type = "string",
                                Description = "Shape type (rectangle, ellipse, etc.)",
                                Required = false
                            }
                        }
                    }
                },
                new McpToolDefinition
                {
                    Name = "connect_shapes",
                    Description = "Connect two shapes with an arrow",
                    SchemaInputs = new Dictionary<string, McpParameterDefinition>
                    {
                        {
                            "diagram",
                            new McpParameterDefinition
                            {
                                Type = "string",
                                Description = "Diagram filename",
                                Required = true
                            }
                        },
                        {
                            "source_id",
                            new McpParameterDefinition
                            {
                                Type = "string",
                                Description = "ID of the source shape",
                                Required = true
                            }
                        },
                        {
                            "target_id",
                            new McpParameterDefinition
                            {
                                Type = "string",
                                Description = "ID of the target shape",
                                Required = true
                            }
                        }
                    }
                },
                new McpToolDefinition
                {
                    Name = "get_diagram_image",
                    Description = "Get a diagram as a base64-encoded image",
                    SchemaInputs = new Dictionary<string, McpParameterDefinition>
                    {
                        {
                            "diagram",
                            new McpParameterDefinition
                            {
                                Type = "string",
                                Description = "Diagram filename",
                                Required = true
                            }
                        },
                        {
                            "format",
                            new McpParameterDefinition
                            {
                                Type = "string",
                                Description = "Image format (png, jpeg, etc.)",
                                Required = false
                            }
                        },
                        {
                            "page",
                            new McpParameterDefinition
                            {
                                Type = "integer",
                                Description = "Page index (defaults to 0)",
                                Required = false
                            }
                        }
                    }
                },
                new McpToolDefinition
                {
                    Name = "delete_shape",
                    Description = "Delete a shape from a diagram",
                    SchemaInputs = new Dictionary<string, McpParameterDefinition>
                    {
                        {
                            "diagram",
                            new McpParameterDefinition
                            {
                                Type = "string",
                                Description = "Diagram filename",
                                Required = true
                            }
                        },
                        {
                            "shape_id",
                            new McpParameterDefinition
                            {
                                Type = "string",
                                Description = "ID of the shape to delete",
                                Required = true
                            }
                        }
                    }
                },
                new McpToolDefinition
                {
                    Name = "update_shape",
                    Description = "Update a shape's properties in a diagram",
                    SchemaInputs = new Dictionary<string, McpParameterDefinition>
                    {
                        {
                            "diagram",
                            new McpParameterDefinition
                            {
                                Type = "string",
                                Description = "Diagram filename",
                                Required = true
                            }
                        },
                        {
                            "shape_id",
                            new McpParameterDefinition
                            {
                                Type = "string",
                                Description = "ID of the shape to update",
                                Required = true
                            }
                        },
                        {
                            "value",
                            new McpParameterDefinition
                            {
                                Type = "string",
                                Description = "New label/text for the shape",
                                Required = true
                            }
                        }
                    }
                },
                new McpToolDefinition
                {
                    Name = "style_shape",
                    Description = "Apply style to a shape in a diagram",
                    SchemaInputs = new Dictionary<string, McpParameterDefinition>
                    {
                        {
                            "diagram",
                            new McpParameterDefinition
                            {
                                Type = "string",
                                Description = "Diagram filename",
                                Required = true
                            }
                        },
                        {
                            "shape_id",
                            new McpParameterDefinition
                            {
                                Type = "string",
                                Description = "ID of the shape to style",
                                Required = true
                            }
                        },
                        {
                            "fill_color",
                            new McpParameterDefinition
                            {
                                Type = "string",
                                Description = "Fill color (hex format)",
                                Required = false
                            }
                        },
                        {
                            "stroke_color",
                            new McpParameterDefinition
                            {
                                Type = "string",
                                Description = "Stroke color (hex format)",
                                Required = false
                            }
                        }
                    }
                },
                new McpToolDefinition
                {
                    Name = "arrange_diagram",
                    Description = "Auto-arrange the layout of a diagram",
                    SchemaInputs = new Dictionary<string, McpParameterDefinition>
                    {
                        {
                            "diagram",
                            new McpParameterDefinition
                            {
                                Type = "string",
                                Description = "Diagram filename",
                                Required = true
                            }
                        },
                        {
                            "layout",
                            new McpParameterDefinition
                            {
                                Type = "string",
                                Description = "Layout algorithm to use (horizontal, vertical, radial)",
                                Required = true
                            }
                        }
                    }
                },
                new McpToolDefinition
                {
                    Name = "move_shape",
                    Description = "Move a shape to a new position in the diagram",
                    SchemaInputs = new Dictionary<string, McpParameterDefinition>
                    {
                        {
                            "diagram",
                            new McpParameterDefinition
                            {
                                Type = "string",
                                Description = "Diagram filename",
                                Required = true
                            }
                        },
                        {
                            "shape_id",
                            new McpParameterDefinition
                            {
                                Type = "string",
                                Description = "ID of the shape to move",
                                Required = true
                            }
                        },
                        {
                            "x",
                            new McpParameterDefinition
                            {
                                Type = "number",
                                Description = "New X position",
                                Required = true
                            }
                        },
                        {
                            "y",
                            new McpParameterDefinition
                            {
                                Type = "number",
                                Description = "New Y position",
                                Required = true
                            }
                        }
                    }
                },
                new McpToolDefinition
                {
                    Name = "update_shape_style",
                    Description = "Update a shape's style properties",
                    SchemaInputs = new Dictionary<string, McpParameterDefinition>
                    {
                        {
                            "diagram",
                            new McpParameterDefinition
                            {
                                Type = "string",
                                Description = "Diagram filename",
                                Required = true
                            }
                        },
                        {
                            "shape_id",
                            new McpParameterDefinition
                            {
                                Type = "string",
                                Description = "ID of the shape to update",
                                Required = true
                            }
                        },
                        {
                            "style_properties",
                            new McpParameterDefinition
                            {
                                Type = "object",
                                Description = "Style properties to update",
                                Required = true
                            }
                        }
                    }
                },
                new McpToolDefinition
                {
                    Name = "create_diagram_page",
                    Description = "Create a new page in a diagram",
                    SchemaInputs = new Dictionary<string, McpParameterDefinition>
                    {
                        {
                            "diagram",
                            new McpParameterDefinition
                            {
                                Type = "string",
                                Description = "Diagram filename",
                                Required = true
                            }
                        },
                        {
                            "name",
                            new McpParameterDefinition
                            {
                                Type = "string",
                                Description = "Name for the new page",
                                Required = true
                            }
                        }
                    }
                },
                new McpToolDefinition
                {
                    Name = "get_diagram_page",
                    Description = "Get the details of a diagram page",
                    SchemaInputs = new Dictionary<string, McpParameterDefinition>
                    {
                        {
                            "diagram",
                            new McpParameterDefinition
                            {
                                Type = "string",
                                Description = "Diagram filename",
                                Required = true
                            }
                        },
                        {
                            "page_index",
                            new McpParameterDefinition
                            {
                                Type = "integer",
                                Description = "Index of the page to retrieve",
                                Required = true
                            }
                        }
                    }
                },
                new McpToolDefinition
                {
                    Name = "update_diagram_page",
                    Description = "Update a diagram page's properties",
                    SchemaInputs = new Dictionary<string, McpParameterDefinition>
                    {
                        {
                            "diagram",
                            new McpParameterDefinition
                            {
                                Type = "string",
                                Description = "Diagram filename",
                                Required = true
                            }
                        },
                        {
                            "page_index",
                            new McpParameterDefinition
                            {
                                Type = "integer",
                                Description = "Index of the page to update",
                                Required = true
                            }
                        },
                        {
                            "name",
                            new McpParameterDefinition
                            {
                                Type = "string",
                                Description = "New name for the page",
                                Required = true
                            }
                        }
                    }
                },
                new McpToolDefinition
                {
                    Name = "delete_diagram_page",
                    Description = "Delete a page from a diagram",
                    SchemaInputs = new Dictionary<string, McpParameterDefinition>
                    {
                        {
                            "diagram",
                            new McpParameterDefinition
                            {
                                Type = "string",
                                Description = "Diagram filename",
                                Required = true
                            }
                        },
                        {
                            "page_index",
                            new McpParameterDefinition
                            {
                                Type = "integer",
                                Description = "Index of the page to delete",
                                Required = true
                            }
                        }
                    }
                },
                new McpToolDefinition
                {
                    Name = "move_cell_between_pages",
                    Description = "Move a cell (shape or connector) from one page to another",
                    SchemaInputs = new Dictionary<string, McpParameterDefinition>
                    {
                        {
                            "diagram",
                            new McpParameterDefinition
                            {
                                Type = "string",
                                Description = "Diagram filename",
                                Required = true
                            }
                        },
                        {
                            "shape_id",
                            new McpParameterDefinition
                            {
                                Type = "string",
                                Description = "ID of the cell to move",
                                Required = true
                            }
                        },
                        {
                            "source_page_index",
                            new McpParameterDefinition
                            {
                                Type = "integer",
                                Description = "Index of the source page",
                                Required = true
                            }
                        },
                        {
                            "target_page_index",
                            new McpParameterDefinition
                            {
                                Type = "integer",
                                Description = "Index of the target page",
                                Required = true
                            }
                        }
                    }
                },
                // New query tools added here
                new McpToolDefinition
                {
                    Name = "find_elements_by_text",
                    Description = "Find diagram elements containing the specified text",
                    SchemaInputs = new Dictionary<string, McpParameterDefinition>
                    {
                        {
                            "diagram",
                            new McpParameterDefinition
                            {
                                Type = "string",
                                Description = "Diagram filename",
                                Required = true
                            }
                        },
                        {
                            "page_index",
                            new McpParameterDefinition
                            {
                                Type = "integer",
                                Description = "Index of the page to search in (defaults to 0)",
                                Required = false
                            }
                        },
                        {
                            "search_text",
                            new McpParameterDefinition
                            {
                                Type = "string",
                                Description = "Text to search for in element labels (case-insensitive)",
                                Required = true
                            }
                        }
                    }
                },
                new McpToolDefinition
                {
                    Name = "get_element_info",
                    Description = "Get detailed information about a specific diagram element",
                    SchemaInputs = new Dictionary<string, McpParameterDefinition>
                    {
                        {
                            "diagram",
                            new McpParameterDefinition
                            {
                                Type = "string",
                                Description = "Diagram filename",
                                Required = true
                            }
                        },
                        {
                            "page_index",
                            new McpParameterDefinition
                            {
                                Type = "integer",
                                Description = "Index of the page containing the element (defaults to 0)",
                                Required = false
                            }
                        },
                        {
                            "element_id",
                            new McpParameterDefinition
                            {
                                Type = "string",
                                Description = "ID of the element to get information about",
                                Required = true
                            }
                        }
                    }
                },
                new McpToolDefinition
                {
                    Name = "list_neighbors",
                    Description = "List all elements connected to the specified element",
                    SchemaInputs = new Dictionary<string, McpParameterDefinition>
                    {
                        {
                            "diagram",
                            new McpParameterDefinition
                            {
                                Type = "string",
                                Description = "Diagram filename",
                                Required = true
                            }
                        },
                        {
                            "page_index",
                            new McpParameterDefinition
                            {
                                Type = "integer",
                                Description = "Index of the page containing the element (defaults to 0)",
                                Required = false
                            }
                        },
                        {
                            "element_id",
                            new McpParameterDefinition
                            {
                                Type = "string",
                                Description = "ID of the element to find neighbors for",
                                Required = true
                            }
                        }
                    }
                },
                new McpToolDefinition
                {
                    Name = "get_diagram_bounds",
                    Description = "Get the bounding box coordinates of all elements in the diagram",
                    SchemaInputs = new Dictionary<string, McpParameterDefinition>
                    {
                        {
                            "diagram",
                            new McpParameterDefinition
                            {
                                Type = "string",
                                Description = "Diagram filename",
                                Required = true
                            }
                        },
                        {
                            "page_index",
                            new McpParameterDefinition
                            {
                                Type = "integer",
                                Description = "Index of the page to get bounds for (defaults to 0)",
                                Required = false
                            }
                        }
                    }
                },
                new McpToolDefinition
                {
                    Name = "resize_shape",
                    Description = "Resize a shape to specified dimensions",
                    SchemaInputs = new Dictionary<string, McpParameterDefinition>
                    {
                        {
                            "diagram",
                            new McpParameterDefinition
                            {
                                Type = "string",
                                Description = "Diagram filename",
                                Required = true
                            }
                        },
                        {
                            "shape_id",
                            new McpParameterDefinition
                            {
                                Type = "string",
                                Description = "ID of the shape to resize",
                                Required = true
                            }
                        },
                        {
                            "width",
                            new McpParameterDefinition
                            {
                                Type = "number",
                                Description = "New width for the shape",
                                Required = true
                            }
                        },
                        {
                            "height",
                            new McpParameterDefinition
                            {
                                Type = "number",
                                Description = "New height for the shape",
                                Required = true
                            }
                        }
                    }
                },
                new McpToolDefinition
                {
                    Name = "set_text_style",
                    Description = "Set text styling properties for a shape",
                    SchemaInputs = new Dictionary<string, McpParameterDefinition>
                    {
                        {
                            "diagram",
                            new McpParameterDefinition
                            {
                                Type = "string",
                                Description = "Diagram filename",
                                Required = true
                            }
                        },
                        {
                            "shape_id",
                            new McpParameterDefinition
                            {
                                Type = "string",
                                Description = "ID of the shape to style",
                                Required = true
                            }
                        },
                        {
                            "font_color",
                            new McpParameterDefinition
                            {
                                Type = "string",
                                Description = "Text color (hex format)",
                                Required = false
                            }
                        },
                        {
                            "font_size",
                            new McpParameterDefinition
                            {
                                Type = "number",
                                Description = "Font size in points",
                                Required = false
                            }
                        },
                        {
                            "font_style",
                            new McpParameterDefinition
                            {
                                Type = "string",
                                Description = "Font style (normal, bold, italic, bolditalic)",
                                Required = false
                            }
                        }
                    }
                },
                new McpToolDefinition
                {
                    Name = "set_line_style",
                    Description = "Set line style properties for a connector",
                    SchemaInputs = new Dictionary<string, McpParameterDefinition>
                    {
                        {
                            "diagram",
                            new McpParameterDefinition
                            {
                                Type = "string",
                                Description = "Diagram filename",
                                Required = true
                            }
                        },
                        {
                            "connector_id",
                            new McpParameterDefinition
                            {
                                Type = "string",
                                Description = "ID of the connector to style",
                                Required = true
                            }
                        },
                        {
                            "line_style",
                            new McpParameterDefinition
                            {
                                Type = "string",
                                Description = "Line style (solid, dashed, dotted)",
                                Required = false
                            }
                        },
                        {
                            "line_width",
                            new McpParameterDefinition
                            {
                                Type = "number",
                                Description = "Line width in pixels",
                                Required = false
                            }
                        },
                        {
                            "edge_style",
                            new McpParameterDefinition
                            {
                                Type = "string",
                                Description = "Edge style (sharp, rounded, curved)",
                                Required = false
                            }
                        },
                        {
                            "routing_style",
                            new McpParameterDefinition
                            {
                                Type = "string",
                                Description = "Line routing style (straight, orthogonal, curved)",
                                Required = false
                            }
                        },
                        {
                            "jump_style",
                            new McpParameterDefinition
                            {
                                Type = "string",
                                Description = "Line jump style (overlapped, arc, gap)",
                                Required = false
                            }
                        }
                    }
                },
                new McpToolDefinition
                {
                    Name = "set_arrow_style",
                    Description = "Set arrow style properties for a connector",
                    SchemaInputs = new Dictionary<string, McpParameterDefinition>
                    {
                        {
                            "diagram",
                            new McpParameterDefinition
                            {
                                Type = "string",
                                Description = "Diagram filename",
                                Required = true
                            }
                        },
                        {
                            "connector_id",
                            new McpParameterDefinition
                            {
                                Type = "string",
                                Description = "ID of the connector to style",
                                Required = true
                            }
                        },
                        {
                            "start_arrow",
                            new McpParameterDefinition
                            {
                                Type = "string",
                                Description = "Start arrow style (none, classic, diamond, oval, open, block)",
                                Required = false
                            }
                        },
                        {
                            "end_arrow",
                            new McpParameterDefinition
                            {
                                Type = "string",
                                Description = "End arrow style (none, classic, diamond, oval, open, block)",
                                Required = false
                            }
                        }
                    }
                },
                new McpToolDefinition
                {
                    Name = "reset_connector",
                    Description = "Reset a connector to its default path",
                    SchemaInputs = new Dictionary<string, McpParameterDefinition>
                    {
                        {
                            "diagram",
                            new McpParameterDefinition
                            {
                                Type = "string",
                                Description = "Diagram filename",
                                Required = true
                            }
                        },
                        {
                            "connector_id",
                            new McpParameterDefinition
                            {
                                Type = "string",
                                Description = "ID of the connector to reset",
                                Required = true
                            }
                        }
                    }
                },
                new McpToolDefinition
                {
                    Name = "reverse_connector",
                    Description = "Reverse the direction of a connector",
                    SchemaInputs = new Dictionary<string, McpParameterDefinition>
                    {
                        {
                            "diagram",
                            new McpParameterDefinition
                            {
                                Type = "string",
                                Description = "Diagram filename",
                                Required = true
                            }
                        },
                        {
                            "connector_id",
                            new McpParameterDefinition
                            {
                                Type = "string",
                                Description = "ID of the connector to reverse",
                                Required = true
                            }
                        }
                    }
                }
            };

            // Return a result with the array of tools properly structured
            return Task.FromResult<object>(new { tools });
        }
    }
} 