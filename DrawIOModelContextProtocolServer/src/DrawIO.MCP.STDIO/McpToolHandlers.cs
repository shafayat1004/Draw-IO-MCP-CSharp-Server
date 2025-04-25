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
using Microsoft.Extensions.Logging;

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
                },
                // Waypoint manipulation tools
                new McpToolDefinition
                {
                    Name = "add_waypoint",
                    Description = "Add a waypoint to a connector for precise path control",
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
                                Description = "ID of the connector to add waypoint to",
                                Required = true
                            }
                        },
                        {
                            "x",
                            new McpParameterDefinition
                            {
                                Type = "number",
                                Description = "X position of the waypoint",
                                Required = true
                            }
                        },
                        {
                            "y",
                            new McpParameterDefinition
                            {
                                Type = "number",
                                Description = "Y position of the waypoint",
                                Required = true
                            }
                        },
                        {
                            "is_relative",
                            new McpParameterDefinition
                            {
                                Type = "boolean",
                                Description = "Whether the waypoint position is relative (defaults to false)",
                                Required = false
                            }
                        },
                        {
                            "position",
                            new McpParameterDefinition
                            {
                                Type = "integer",
                                Description = "Index position to insert waypoint (defaults to end of waypoint list)",
                                Required = false
                            }
                        }
                    }
                },
                new McpToolDefinition
                {
                    Name = "remove_waypoint",
                    Description = "Remove a waypoint from a connector",
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
                                Description = "ID of the connector to remove waypoint from",
                                Required = true
                            }
                        },
                        {
                            "waypoint_index",
                            new McpParameterDefinition
                            {
                                Type = "integer",
                                Description = "Index of the waypoint to remove",
                                Required = true
                            }
                        }
                    }
                },
                new McpToolDefinition
                {
                    Name = "update_waypoint",
                    Description = "Update a waypoint's position on a connector",
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
                                Description = "ID of the connector to update waypoint on",
                                Required = true
                            }
                        },
                        {
                            "waypoint_index",
                            new McpParameterDefinition
                            {
                                Type = "integer",
                                Description = "Index of the waypoint to update",
                                Required = true
                            }
                        },
                        {
                            "x",
                            new McpParameterDefinition
                            {
                                Type = "number",
                                Description = "New X position for the waypoint",
                                Required = false
                            }
                        },
                        {
                            "y",
                            new McpParameterDefinition
                            {
                                Type = "number",
                                Description = "New Y position for the waypoint",
                                Required = false
                            }
                        }
                    }
                },
                new McpToolDefinition
                {
                    Name = "get_waypoints",
                    Description = "Get all waypoints on a connector",
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
                                Description = "ID of the connector to get waypoints from",
                                Required = true
                            }
                        }
                    }
                },
                new McpToolDefinition
                {
                    Name = "clear_waypoints",
                    Description = "Remove all waypoints from a connector, resetting to default path",
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
                                Description = "ID of the connector to clear waypoints from",
                                Required = true
                            }
                        }
                    }
                },
                new McpToolDefinition
                {
                    Name = "group_shapes",
                    Description = "Group multiple shapes into a single group",
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
                            "shape_ids",
                            new McpParameterDefinition
                            {
                                Type = "array",
                                Description = "Array of shape IDs to group together",
                                Required = true,
                                Items = new McpParameterDefinition
                                {
                                    Type = "string",
                                    Description = "ID of a shape to include in the group"
                                }
                            }
                        },
                        {
                            "page_index",
                            new McpParameterDefinition
                            {
                                Type = "integer",
                                Description = "Index of the page containing the shapes (defaults to 0)",
                                Required = false
                            }
                        }
                    }
                },
                new McpToolDefinition
                {
                    Name = "ungroup_shapes",
                    Description = "Ungroup shapes from a group",
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
                            "group_id",
                            new McpParameterDefinition
                            {
                                Type = "string",
                                Description = "ID of the group to ungroup",
                                Required = true
                            }
                        },
                        {
                            "page_index",
                            new McpParameterDefinition
                            {
                                Type = "integer",
                                Description = "Index of the page containing the group (defaults to 0)",
                                Required = false
                            }
                        }
                    }
                },
                // Add new tool definitions after the existing tools
                new McpToolDefinition
                {
                    Name = "rotate_shape",
                    Description = "Rotate a shape by a specified angle",
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
                                Description = "ID of the shape to rotate",
                                Required = true
                            }
                        },
                        {
                            "angle",
                            new McpParameterDefinition
                            {
                                Type = "number",
                                Description = "Rotation angle in degrees",
                                Required = true
                            }
                        },
                        {
                            "return_diagram",
                            new McpParameterDefinition
                            {
                                Type = "boolean",
                                Description = "Whether to include the diagram image in the response",
                                Required = false
                            }
                        }
                    }
                },
                new McpToolDefinition
                {
                    Name = "flip_shape",
                    Description = "Flip a shape horizontally or vertically",
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
                                Description = "ID of the shape to flip",
                                Required = true
                            }
                        },
                        {
                            "direction",
                            new McpParameterDefinition
                            {
                                Type = "string",
                                Description = "Direction to flip (horizontal or vertical)",
                                Required = true
                            }
                        },
                        {
                            "return_diagram",
                            new McpParameterDefinition
                            {
                                Type = "boolean",
                                Description = "Whether to include the diagram image in the response",
                                Required = false
                            }
                        }
                    }
                },
                new McpToolDefinition
                {
                    Name = "set_diagram_background",
                    Description = "Set the background color or image for a diagram",
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
                            "background_color",
                            new McpParameterDefinition
                            {
                                Type = "string",
                                Description = "Background color in hex format (e.g., #f5f5f5)",
                                Required = false
                            }
                        },
                        {
                            "background_image",
                            new McpParameterDefinition
                            {
                                Type = "string",
                                Description = "URL or path to background image",
                                Required = false
                            }
                        },
                        {
                            "return_diagram",
                            new McpParameterDefinition
                            {
                                Type = "boolean",
                                Description = "Whether to include the diagram image in the response",
                                Required = false
                            }
                        }
                    }
                },
                new McpToolDefinition
                {
                    Name = "connect_shapes_at_points",
                    Description = "Connect two shapes with an arrow at specific points",
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
                        },
                        {
                            "source_x",
                            new McpParameterDefinition
                            {
                                Type = "number",
                                Description = "X coordinate on the source shape",
                                Required = false
                            }
                        },
                        {
                            "source_y",
                            new McpParameterDefinition
                            {
                                Type = "number",
                                Description = "Y coordinate on the source shape",
                                Required = false
                            }
                        },
                        {
                            "target_x",
                            new McpParameterDefinition
                            {
                                Type = "number",
                                Description = "X coordinate on the target shape",
                                Required = false
                            }
                        },
                        {
                            "target_y",
                            new McpParameterDefinition
                            {
                                Type = "number",
                                Description = "Y coordinate on the target shape",
                                Required = false
                            }
                        },
                        {
                            "return_diagram",
                            new McpParameterDefinition
                            {
                                Type = "boolean",
                                Description = "Whether to include the diagram image in the response",
                                Required = false
                            }
                        }
                    }
                },
            };

            // Add the return_diagram parameter to all relevant tools
            // Define it once to reuse
            var returnDiagramParam = new McpParameterDefinition
            {
                Type = "boolean",
                Description = "Whether to include the diagram image in the response",
                Required = false
            };
            
            // Add it to each tool definition except get_diagram_image
            foreach (var tool in tools.Where(t => t.Name != "get_diagram_image" && t.SchemaInputs.ContainsKey("diagram")))
            {
                tool.SchemaInputs["return_diagram"] = returnDiagramParam;
            }

            // Return a result with the array of tools properly structured
            return Task.FromResult<object>(new { tools });
        }

        /// <summary>
        /// List available shape types that can be used with add_shape
        /// </summary>
        public static Task<object> ListShapeTypesAsync(JsonElement parameters, string diagramsDirectory, TextWriter logWriter, bool verbose)
        {
            try
            {
                // Define common shape categories and their types
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
                    shapeCategories = shapeTypes,
                    content = new[]
                    {
                        new
                        {
                            type = "text",
                            text = "Available shape types for use with add_shape tool"
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                if (verbose)
                {
                    logWriter.WriteLine($"Error listing shape types: {ex.Message}");
                    logWriter.WriteLine(ex.StackTrace);
                }
                
                return Task.FromResult<object>(new
                {
                    status = "error",
                    message = ex.Message,
                    content = new[]
                    {
                        new
                        {
                            type = "text",
                            text = $"Error listing shape types: {ex.Message}"
                        }
                    }
                });
            }
        }
    }
} 