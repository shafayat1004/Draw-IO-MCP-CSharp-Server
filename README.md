# DrawIO MCP Server

A Model Context Protocol (MCP) server implementation for drawio (diagrams.net) diagrams. This server allows AI agents (via IDE plugins or codegen tools) to read, generate, and edit `.drawio` diagram files.

## Features

- Full MCP-compliant server using the official [Model Context Protocol C# SDK](https://github.com/modelcontextprotocol/csharp-sdk)
- **Comprehensive Toolset:** Offers 36+ tools for reading, generating, and editing `.drawio` files via API.
- **High Reliability:** Recent testing shows a >97% success rate across all tools (see Testing Status section).
- Support for different shape types and connectors
- Automatic XML parsing and serialization for DrawIO files
- Support for both STDIO (command-line) and SSE (Server-Sent Events) modes
- VPC diagram generation template (Note: Known issue, see MCP Tools section)
- Robust error handling and detailed logging
- Comprehensive styling options for diagram elements (shapes, text, lines, arrows)
- Diagram auto-arrangement capabilities
- Multi-page diagram support with page management operations
- Image export capabilities

## Project Structure

```
├── src
│   ├── DrawIO.MCP.Core        # F# core library for DrawIO file manipulation
│   ├── DrawIO.MCP.STDIO       # C# STDIO implementation for command-line MCP 
│   └── DrawIO.MCP.SSE         # C# SSE Web API implementation for Cursor integration
├── tests
│   ├── DrawIO.MCP.Core.Tests
│   ├── DrawIO.MCP.STDIO.Tests
│   └── DrawIO.MCP.SSE.Tests
├── diagrams                   # Directory for storing diagram files
└── learning                   # Documentation of lessons learned and best practices
```

## Installation & Running

### Prerequisites

- .NET 8.0 SDK
- Visual Studio 2022, VS Code, or Rider (optional)
- drawio CLI (optional, required for image export)

### Building the Solution

```bash
dotnet build
```

### Running the STDIO Server

```bash
# Run with default settings
dotnet run --project src/DrawIO.MCP.STDIO/DrawIO.MCP.STDIO.csproj

# Run with custom diagrams directory
dotnet run --project src/DrawIO.MCP.STDIO/DrawIO.MCP.STDIO.csproj -- --diagrams-dir /path/to/diagrams

# Run with verbose logging
dotnet run --project src/DrawIO.MCP.STDIO/DrawIO.MCP.STDIO.csproj -- --verbose
```

### Running the SSE Server

```bash
# Run with default settings (http://localhost:5000)
dotnet run --project src/DrawIO.MCP.SSE/DrawIO.MCP.SSE.csproj

# Run with custom settings via environment variables
DIAGRAMS_DIR=/path/to/diagrams ASPNETCORE_URLS="http://localhost:8000" dotnet run --project src/DrawIO.MCP.SSE/DrawIO.MCP.SSE.csproj
```

## MCP Integration

### Using with Cursor IDE

To use the SSE server with Cursor:

1. Start the SSE server:
```bash
dotnet run --project src/DrawIO.MCP.SSE/DrawIO.MCP.SSE.csproj
```

2. Configure Cursor to use the server by creating a `.cursor/mcp.json` file in your project directory or in your home directory:
```json
{
  "mcpServers": {
    "drawio-mcp-server": {
      "url": "http://localhost:5000/mcp/sse"
    }
  }
}
```

3. In Cursor, use the Agent with commands like:
```
Create a diagram showing a client-server architecture
```

### Using the STDIO Server

The STDIO server can be used by any MCP client that supports the STDIO transport method, like the MCP CLI:

```bash
mcp connect stdio --command "dotnet run --project /path/to/DrawIO.MCP.STDIO/DrawIO.MCP.STDIO.csproj"
```

## MCP Resources

The server exposes the following MCP resources:

- `diagram://{diagram_name}` - Retrieve a diagram's content as JSON
- `diagram-list://all` - List all available diagrams

## MCP Tools

The server exposes a comprehensive set of MCP tools, enabling detailed diagram manipulation. Recent testing has verified the functionality of these tools (see Testing Status). List of available tools:

- `create_new_diagram` - Create a new empty diagram file
- `add_shape` - Add a new shape to a diagram
- `connect_shapes` - Connect two shapes with an arrow
- `delete_shape` - Delete a shape from a diagram
- `update_shape` - Update properties of an existing shape
- `style_shape` - Apply styles to shapes (colors, borders, etc.)
- `arrange_diagram` - Automatically organize shapes in a diagram (horizontal, vertical, or grid layout)
- `generate_vpc` - Generate a sample AWS VPC layout diagram
    - **Known Issue:** The current implementation requires a `content` array parameter not defined in the tool schema. Schema update is needed for correct usage.
- `get_diagram_image` - Export a diagram as an image (requires drawio CLI)
- `move_shape` - Move a shape to a new position in the diagram
- `update_shape_style` - Update specific style properties of a shape
- `create_diagram_page` - Create a new page in a multi-page diagram
- `get_diagram_page` - Retrieve details of a specific diagram page
- `update_diagram_page` - Update properties of a diagram page
- `delete_diagram_page` - Delete a page from a diagram
- `move_cell_between_pages` - Move a cell (shape/connector) from one page to another
- `find_elements_by_text` - Find elements containing specified text
- `get_element_info` - Get detailed info about a specific element
- `list_neighbors` - List elements connected to a specific element
- `get_diagram_bounds` - Get the bounding box of all elements
- `resize_shape` - Resize a shape
- `set_text_style` - Apply text styling (font, color, size, style)
- `set_line_style` - Apply line styling (style, width, routing, edge)
- `set_arrow_style` - Apply arrow styling (start/end arrows)
- `reset_connector` - Reset a connector's path
- `reverse_connector` - Reverse a connector's direction
- `add_waypoint` - Add a waypoint to a connector
- `remove_waypoint` - Remove a waypoint from a connector
- `update_waypoint` - Update a waypoint's position
- `get_waypoints` - Get all waypoints on a connector
- `clear_waypoints` - Remove all waypoints from a connector
- `group_shapes` - Group multiple shapes
- `ungroup_shapes` - Ungroup shapes
- `rotate_shape` - Rotate a shape
- `flip_shape` - Flip a shape horizontally or vertically
- `set_diagram_background` - Set diagram background color or image
- `connect_shapes_at_points` - Connect shapes at specific points

## Testing Status (As of 2024-03-27)

Comprehensive testing of all 36+ MCP tools was recently completed. Key results:

- **Success Rate:** >97% (43 out of 44 tests passed).
- **Verified Functionality:** All core diagram creation, manipulation, styling, page management, grouping, and waypoint operations were successful.
- **Known Issue:** The `generate_vpc` tool failed due to a mismatch between its schema definition and implementation requirements (missing `content` array parameter).
- **Detailed Log:** See `drawio_mcp_test_log.md` for detailed results of each tool test.

## Future Enhancements & Recommendations

Based on recent testing and development, key areas for future work include:

- **Tool Schema Alignment:** Update the schema for `generate_vpc` to match its implementation.
- **Enhanced Validation:** Implement more robust parameter validation, potentially including schema validation checks.
- **Improved Documentation:** Further clarify parameter requirements and types, especially for complex operations.
- **Advanced Features:** Explore planned enhancements like component overlap detection, targeted queries, and step-by-step generation.

## Running Tests

```bash
dotnet test
```