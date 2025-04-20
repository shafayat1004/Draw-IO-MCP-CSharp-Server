# DrawIO MCP Server - Usage Examples

This document provides examples of how to use the DrawIO MCP Server from different client applications. These examples demonstrate common diagram creation and manipulation workflows.

## Basic Diagram Creation

### Creating a New Diagram

```json
{
  "jsonrpc": "2.0",
  "id": "1",
  "method": "mcp/executeTool",
  "params": {
    "tool": "create_new_diagram",
    "parameters": {
      "name": "system-architecture.drawio"
    }
  }
}
```

### Generating a VPC Diagram Template

```json
{
  "jsonrpc": "2.0",
  "id": "2",
  "method": "mcp/executeTool",
  "params": {
    "tool": "generate_vpc",
    "parameters": {
      "diagram_name": "aws-infrastructure.drawio"
    }
  }
}
```

## Adding Elements to Diagrams

### Adding Shapes

```json
{
  "jsonrpc": "2.0",
  "id": "3",
  "method": "mcp/executeTool",
  "params": {
    "tool": "add_shape",
    "parameters": {
      "diagram": "system-architecture.drawio",
      "value": "API Server",
      "x": 100,
      "y": 100,
      "width": 120,
      "height": 60,
      "shape": "rectangle"
    }
  }
}
```

### Connecting Shapes

First, note the shape IDs returned from the add_shape operations. Then use them to connect the shapes:

```json
{
  "jsonrpc": "2.0",
  "id": "4",
  "method": "mcp/executeTool",
  "params": {
    "tool": "connect_shapes",
    "parameters": {
      "diagram": "system-architecture.drawio",
      "source_id": "shape-id-1",
      "target_id": "shape-id-2"
    }
  }
}
```

## Styling and Positioning

### Styling Shapes

```json
{
  "jsonrpc": "2.0",
  "id": "5",
  "method": "mcp/executeTool",
  "params": {
    "tool": "style_shape",
    "parameters": {
      "diagram": "system-architecture.drawio",
      "shape_id": "shape-id-1",
      "fill_color": "#4CAF50",
      "stroke_color": "#2E7D32"
    }
  }
}
```

### Moving Shapes

```json
{
  "jsonrpc": "2.0",
  "id": "6",
  "method": "mcp/executeTool",
  "params": {
    "tool": "move_shape",
    "parameters": {
      "diagram": "system-architecture.drawio",
      "shape_id": "shape-id-1",
      "x": 200,
      "y": 150
    }
  }
}
```

### Auto-Arranging Diagram Elements

```json
{
  "jsonrpc": "2.0",
  "id": "7",
  "method": "mcp/executeTool",
  "params": {
    "tool": "arrange_diagram",
    "parameters": {
      "diagram": "system-architecture.drawio",
      "layout": "horizontal"
    }
  }
}
```

## Multi-page Diagram Management

### Creating a New Page

```json
{
  "jsonrpc": "2.0",
  "id": "8",
  "method": "mcp/executeTool",
  "params": {
    "tool": "create_diagram_page",
    "parameters": {
      "diagram": "system-architecture.drawio",
      "name": "Network Architecture"
    }
  }
}
```

### Getting Page Details

```json
{
  "jsonrpc": "2.0",
  "id": "9",
  "method": "mcp/executeTool",
  "params": {
    "tool": "get_diagram_page",
    "parameters": {
      "diagram": "system-architecture.drawio",
      "page_index": 1
    }
  }
}
```

### Moving Elements Between Pages

```json
{
  "jsonrpc": "2.0",
  "id": "10",
  "method": "mcp/executeTool",
  "params": {
    "tool": "move_cell_between_pages",
    "parameters": {
      "diagram": "system-architecture.drawio",
      "shape_id": "shape-id-1",
      "source_page_index": 0,
      "target_page_index": 1
    }
  }
}
```

## Exporting and Retrieving Diagrams

### Exporting Diagram as Image

```json
{
  "jsonrpc": "2.0",
  "id": "11",
  "method": "mcp/executeTool",
  "params": {
    "tool": "get_diagram_image",
    "parameters": {
      "diagram": "system-architecture.drawio",
      "format": "png",
      "page": 0
    }
  }
}
```

### Listing All Diagrams

```json
{
  "jsonrpc": "2.0",
  "id": "12",
  "method": "mcp/getResource",
  "params": {
    "resourceId": "diagram-list://all"
  }
}
```

### Retrieving Diagram Content

```json
{
  "jsonrpc": "2.0",
  "id": "13",
  "method": "mcp/getResource",
  "params": {
    "resourceId": "diagram://system-architecture.drawio"
  }
}
```

## Complete Workflow Example

Here's a complete example showing a workflow for creating a client-server architecture diagram:

1. Create a new diagram:
```json
{
  "jsonrpc": "2.0",
  "id": "workflow-1",
  "method": "mcp/executeTool",
  "params": {
    "tool": "create_new_diagram",
    "parameters": {
      "name": "client-server-architecture.drawio"
    }
  }
}
```

2. Add a database shape:
```json
{
  "jsonrpc": "2.0",
  "id": "workflow-2",
  "method": "mcp/executeTool",
  "params": {
    "tool": "add_shape",
    "parameters": {
      "diagram": "client-server-architecture.drawio",
      "value": "Database",
      "x": 100,
      "y": 100,
      "width": 80,
      "height": 100,
      "shape": "cylinder"
    }
  }
}
```

3. Add an API server shape:
```json
{
  "jsonrpc": "2.0",
  "id": "workflow-3",
  "method": "mcp/executeTool",
  "params": {
    "tool": "add_shape",
    "parameters": {
      "diagram": "client-server-architecture.drawio",
      "value": "API Server",
      "x": 300,
      "y": 100,
      "width": 120,
      "height": 60,
      "shape": "rectangle"
    }
  }
}
```

4. Connect database to API server (using IDs returned from previous steps):
```json
{
  "jsonrpc": "2.0",
  "id": "workflow-4",
  "method": "mcp/executeTool",
  "params": {
    "tool": "connect_shapes",
    "parameters": {
      "diagram": "client-server-architecture.drawio",
      "source_id": "database-id-from-step-2",
      "target_id": "api-server-id-from-step-3"
    }
  }
}
```

5. Style the API server shape:
```json
{
  "jsonrpc": "2.0",
  "id": "workflow-5",
  "method": "mcp/executeTool",
  "params": {
    "tool": "style_shape",
    "parameters": {
      "diagram": "client-server-architecture.drawio",
      "shape_id": "api-server-id-from-step-3",
      "fill_color": "#4CAF50",
      "stroke_color": "#2E7D32"
    }
  }
}
```

6. Export the diagram as an image:
```json
{
  "jsonrpc": "2.0",
  "id": "workflow-6",
  "method": "mcp/executeTool",
  "params": {
    "tool": "get_diagram_image",
    "parameters": {
      "diagram": "client-server-architecture.drawio",
      "format": "png",
      "page": 0
    }
  }
}
```

## Using with Cursor IDE

When using the DrawIO MCP Server with Cursor IDE, you can use natural language commands like:

```
Create a diagram showing API gateway connecting to three microservices
```

```
Create an AWS VPC architecture with public and private subnets
```

```
Add a blue database icon to the bottom right of my diagram
```

```
Connect the API Gateway to the Auth Service with a dotted line
```

## Using with MCP CLI

For CLI usage, you can create shell scripts that interact with the MCP server:

```bash
#!/bin/bash

# Connect to the MCP server
MCP_CONN="mcp connect stdio --command 'dotnet run --project /path/to/DrawIO.MCP.STDIO/DrawIO.MCP.STDIO.csproj'"

# Create a new diagram
$MCP_CONN execute-tool create_new_diagram --name architecture.drawio

# Add shapes
$MCP_CONN execute-tool add_shape --diagram architecture.drawio --value "Web Server" --x 100 --y 100 --width 120 --height 60
$MCP_CONN execute-tool add_shape --diagram architecture.drawio --value "Database" --x 300 --y 100 --width 80 --height 100 --shape cylinder

# Get the diagram as an image
$MCP_CONN execute-tool get_diagram_image --diagram architecture.drawio --format png --page 0 > architecture.png
``` 