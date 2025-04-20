# MCP Draw.io Server

A Model Context Protocol (MCP) server implementation for draw.io (diagrams.net) diagrams. This server allows AI agents (via IDE plugins or codegen tools) to read, generate, and edit `.drawio` diagram files.

> **Important Note:** The Server-Sent Events (SSE) functionality is **NOT YET IMPLEMENTED**. Only the basic HTTP API functionality for diagram manipulation is working as expected. The server provides direct API endpoints but the MCP protocol integration via SSE is still under development.

## Features

- Full MCP-compliant server using the official [MCP Python SDK](https://github.com/modelcontextprotocol/python-sdk)
- Full read, generate, and edit capabilities for `.drawio` files
- Advanced positioning, styling, and component manipulation
- Support for custom icons and images
- Automatic connector repair functionality
- Sample diagram generation capabilities (e.g., AWS VPC layout)
- Base64 image export for LLM visualization and analysis
- Docker containerization for easy deployment
- Customizable port configuration for both server and client
- Comprehensive testing suite with direct DrawIO file validation
- Multi-page diagram support with page management operations
- Connection and relationship management between diagram elements
- Prompt templates for common diagram creation tasks
- Easy-to-use diagram export utilities

## Project Structure

```
├── Dockerfile
├── docker-compose.yaml
├── requirements.txt
├── requirements-dev.txt
├── run_local.sh
├── run_tests.sh
├── export_diagram.py     # Utility to create and export diagrams
├── test_api_endpoints.py # Comprehensive API testing script
├── mcp_client_test.py
├── comprehensive_test.py
├── mcp_drawio
│   ├── __init__.py
│   ├── server.py        # Main entry point
│   ├── server_new.py    # MCP SDK implementation
│   ├── api/             # Legacy API definitions
│   ├── models/
│   ├── services/
│   └── core/
├── tests
│   ├── unit/
│   ├── integration/
│   ├── utils/
│   └── conftest.py
└── diagrams/
    └── ... (directory for storing .drawio diagram files)
```

## Updates

### MCP SDK Integration

This server uses the official MCP Python SDK for API functionality. This brings several benefits:

- Standardized implementation of the MCP protocol
- Built-in support for all MCP primitives (resources, tools, prompts)
- Improved debugging and error handling

> **Note:** While the MCP SDK is integrated, the SSE endpoint for Cursor IDE integration is not yet fully implemented.

### Diagram Export Functionality

The repository includes a dedicated script for exporting diagrams:

```bash
# Run the export script to create a sample diagram and export it as PNG
python export_diagram.py
```

This script demonstrates:
- Creating a new diagram
- Adding multiple shapes with different properties
- Creating connectors between shapes
- Exporting the diagram as a PNG image

## Installation & Running

### Required Dependencies

```bash
pip install mcp httpx
```

### Using Docker (Recommended)

1. Clone this repository
2. Build and run using Docker Compose:

```bash
docker-compose build
docker-compose up
```

The server will be available at `http://localhost:8001`.

### Running Locally (For Development)

1. Clone this repository
2. Install Python dependencies:

```bash
pip install -r requirements.txt
```

3. Use the provided script to manage the server:

```bash
# Start the server (default port 8001)
./run_local.sh start

# Start the server on custom port
./run_local.sh start -p 8888

# Check server status
./run_local.sh status

# View live server logs
./run_local.sh logs

# Restart the server
./run_local.sh restart

# Restart the server on custom port
./run_local.sh restart -p 8888

# Stop the server
./run_local.sh stop
```

The script provides the following features:
- Tracks server process using PID file
- Provides server status information
- Creates the diagrams directory if it doesn't exist
- Redirects server logs to `/tmp/mcp_drawio_server.log`
- Supports custom port configuration via `-p` or `--port` option

Running without arguments will display usage information.

### Manual Start

Alternatively, you can start the server directly with auto-reload enabled:

```bash
# Default port (8001)
python -m mcp_drawio.server --port 8001 --reload

# Custom port
python -m mcp_drawio.server --port 8888 --reload
```

## MCP Integration

The MCP Draw.io Server implements the Model Context Protocol, a standardized way for AI models to interact with external tools and data sources. As an MCP server, it allows AI assistants to:

1. Discover available diagram resources (`diagram://` resources)
2. Create and modify diagram files through standardized tools
3. Generate visualizations that AI models can incorporate into their responses
4. Access prompt templates for diagram creation tasks

For detailed information about MCP integration options, see [README-MCP.md](README-MCP.md).

### MCP Compatibility

This server is compatible with:
- MCP-enabled IDE plugins (VSCode, JetBrains)
- Claude Desktop and other MCP-compliant AI assistants
- Custom MCP client implementations
- Cursor IDE via SSE connection

### SSE Transport Support

The server implements Server-Sent Events (SSE) transport for the MCP protocol, allowing AI tools like Cursor to connect to it over the network:

```json
{
  "mcpServers": {
    "drawio-mcp-cli": {
      "url": "http://localhost:8001/mcp/sse"
    }
  }
}
```

When connected via SSE, the server automatically:
- Provides the list of available tools
- Sends diagram resource information
- Maintains a persistent connection with heartbeats
- Responds to tool execution requests

### Using with Cursor IDE

> **Important:** The SSE functionality is not yet implemented. The configuration below is for future reference once SSE is properly implemented.

To use this server with Cursor (once SSE is implemented):

1. Start the MCP DrawIO Server
```bash
python -m mcp_drawio.server --port 8001
```

2. Configure Cursor to use the server by creating a `.cursor/mcp.json` file in your project directory or `~/.cursor/mcp.json` in your home directory:
```json
{
  "mcpServers": {
    "drawio-mcp-cli": {
      "command": "python",
      "args": ["basic_mcp_server.py"],
      "env": {}
    }
  }
}
```

3. In Cursor, use the Agent with commands like:
```
Create a diagram showing a client-server architecture
```

> **Current Limitation:** Until SSE is fully implemented, you should use the HTTP API endpoints directly as described in the testing sections.

## MCP Resources

The server exposes the following MCP resources:

- `diagram://{diagram_name}` - Retrieve a diagram's content as JSON
- `diagram-list://all` - List all available diagrams
- `diagram-image://{diagram_name}` - Get a diagram as a base64-encoded image

## MCP Tools

The server exposes the following MCP tools:

### Basic Diagram Operations
- `create_new_diagram` - Create a new empty diagram file
- `generate_vpc` - Generate a sample AWS VPC layout diagram
- `get_diagram_image` - Get the current diagram as a base64-encoded image

### Shape Management
- `add_shape` - Add a new shape to a diagram
- `move_shape` - Move a shape to a new position
- `update_shape` - Update shape's position, size, text, and style
- `update_shape_style` - Update shape's colors and visual styling
- `get_relative_position` - Get relative position between two shapes

### Connector Management
- `connect_shapes` - Connect two shapes with an arrow
- `update_connector` - Update connector's properties 
- `update_connector_style` - Update connector's arrow types and styles
- `repair_connector` - Fix arrows that appear connected but aren't properly linked
- `auto_connect_all` - Fix all connectors in a diagram

### Document Management
- `update_document_size` - Update the document/page size

### Page Management
- `create_diagram_page` - Create a new empty page in an existing diagram
- `get_diagram_page` - Retrieve a specific page from a diagram by index or ID
- `update_diagram_page` - Update a page's properties including name and dimensions
- `delete_diagram_page` - Delete a page from a diagram
- `move_cell_between_pages` - Move a cell (shape or connector) from one page to another

### Advanced Features
- `add_custom_image_tool` - Add a custom image/icon to the diagram (special handling via multipart/form-data)

## MCP Prompts

The server provides the following MCP prompts:

- `create_vpc_diagram_prompt` - Create a sample AWS VPC diagram
- `create_web_architecture_prompt` - Create a standard web application architecture diagram
- `create_flowchart_prompt` - Create a flowchart for a business process

## Testing the Server

### Running the Test Suite

The server includes a comprehensive test suite that validates all core functionality:

```bash
# Run all tests
./run_tests.sh

# Run only unit tests
./run_tests.sh unit

# Run only integration tests
./run_tests.sh integration
```

The test suite covers all major components of the server, including diagram creation, shape manipulation, page management, connector operations, and MCP compliance.

### API Endpoint Testing

The repository includes a comprehensive API testing script:

```bash
# Run the API endpoint test script
python test_api_endpoints.py
```

This script tests all major API endpoints:
- Health endpoint
- Diagram creation, listing, and retrieval
- Shape addition, movement, and styling
- Connector creation, styling, and repair
- Page management operations
- Image export functionality

### Diagram Export Testing

Use the export_diagram.py script to test the diagram export functionality:

```bash
python export_diagram.py
```

This creates a test diagram with shapes and connectors and exports it as a PNG image.

## Docker Support

The MCP Draw.io Server can be run in a Docker container. This is the recommended way to deploy the service, as it ensures all dependencies are correctly installed and configured.

### Building and Running with Docker Compose

The easiest way to run the server is using Docker Compose:

```bash
# Build and start the container
docker-compose up -d

# View logs
docker-compose logs -f
```

The server will be available at http://localhost:8001.

## License

MIT 