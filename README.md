# DrawIO MCP Server

A Model Context Protocol (MCP) server implementation for draw.io (diagrams.net) diagrams. This server allows AI agents (via IDE plugins or codegen tools) to read, generate, and edit `.drawio` diagram files.

## Features

- Full MCP-compliant server using the official [Model Context Protocol C# SDK](https://github.com/modelcontextprotocol/csharp-sdk)
- Read, generate, and edit `.drawio` files via API
- Support for different shape types and connectors
- Automatic XML parsing and serialization for DrawIO files
- Support for both STDIO (command-line) and SSE (Server-Sent Events) modes
- VPC diagram generation template
- Robust error handling and detailed logging
- Comprehensive styling options for diagram elements
- Diagram auto-arrangement capabilities

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
└── diagrams                   # Directory for storing diagram files
```

## Installation & Running

### Prerequisites

- .NET 8.0 SDK
- Visual Studio 2022, VS Code, or Rider (optional)

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

The server exposes the following MCP tools:

- `create_new_diagram` - Create a new empty diagram file
- `add_shape` - Add a new shape to a diagram
- `connect_shapes` - Connect two shapes with an arrow
- `delete_shape` - Delete a shape from a diagram
- `update_shape` - Update properties of an existing shape
- `style_shape` - Apply predefined styles to shapes (AWS, Azure, GCP, and common diagram styles)
- `arrange_diagram` - Automatically organize shapes in a diagram
- `generate_vpc` - Generate a sample AWS VPC layout diagram

## Running Tests

```bash
dotnet test
```

## License

MIT

## Acknowledgements

- [Model Context Protocol Project](https://modelcontextprotocol.io)
- [draw.io](https://www.drawio.com)