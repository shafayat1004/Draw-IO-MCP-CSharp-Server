# DrawIO MCP Server Options

This document explains the different ways to run the DrawIO MCP server and how to connect it to Cursor IDE.

## Server Transport Options

The DrawIO MCP server supports two transport mechanisms:

### 1. HTTP/SSE Transport (Network-based)

This is the main server implementation that runs as a web service. It provides:
- REST API endpoints for diagram manipulation
- MCP SSE endpoint for network-based MCP clients
- Web interface for viewing API documentation

**File:** `mcp_drawio/server_new.py` (implementation) and `mcp_drawio/server.py` (entry point)

**Run with:**
```bash
python -m mcp_drawio.server --port 8000
```

**Access at:** http://localhost:8000

### 2. stdio Transport (Direct Process)

This implementation works with Cursor IDE's direct process communication. Cursor starts the server and communicates with it via standard input/output.

**File:** `drawio_mcp_stdio.py`

**Not meant to be run directly** - Cursor will start this process based on your .cursor/mcp.json configuration.

## Cursor Configuration

To use the DrawIO MCP server with Cursor, you need to configure it in your `.cursor/mcp.json` file:

### stdio Transport Configuration (Recommended)

```json
{
  "mcpServers": {
    "drawio-mcp": {
      "command": "python",
      "args": ["drawio_mcp_stdio.py"],
      "env": {}
    }
  }
}
```

### HTTP/SSE Transport Configuration (Experimental)

Note: SSE functionality is not fully implemented yet, so this option may not work correctly.

```json
{
  "mcpServers": {
    "drawio-mcp-remote": {
      "url": "http://localhost:8000/mcp/sse"
    }
  }
}
```

## Available MCP Tools

The DrawIO MCP server provides the following tools:

1. `create_new_diagram` - Create a new empty diagram file
2. `generate_vpc` - Generate a sample AWS VPC layout diagram
3. `add_shape` - Add a new shape to a diagram
4. `connect_shapes_tool` - Connect two shapes with an arrow
5. `get_diagram_image_tool` - Get a diagram as a base64-encoded image

## Available MCP Resources

The server exposes these resources:

1. `diagram-list://all` - List all available diagrams
2. `diagram://{diagram_name}` - Access a specific diagram's content

## Development Notes

To add more tools or resources, modify:
- For HTTP/SSE: `mcp_drawio/server_new.py` 
- For stdio: `drawio_mcp_stdio.py`

When adding functionality, ensure it works for both transport types to maintain consistency. 