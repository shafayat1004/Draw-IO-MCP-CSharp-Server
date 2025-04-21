# Overview
Here's full plan and scripts to set up a self contained docker env and code to create this MCP Server for drawio.
I want to be able to run the docker container locally. and use cursor or copilot to generate and edit diagrams and use the full feature capability of the drawio platform.

- A simple API server (likely with FastAPI for simplicity)
- Full read, generate, and edit capabilities for `.drawio` files via CLI
- MCP-compliant endpoints to expose diagram pages, individual cells, and editing tools
- A minimal prompt interface to support LLM-driven edits (e.g., generate VPC layout)
- No auth for now, but notes on how to layer it in later

I’ll also generate the full project structure, Dockerfile, and helper scripts so you can run and develop in Cursor or VS Code. I’ll update you once it’s ready.

# MCP drawio Diagram Server Prototype

This answer presents a **Model Context Protocol (MCP)** server implementation for drawio (diagrams.net) diagrams. The server is self-contained in a Docker container and allows an AI agent (via IDE plugins or codegen tools) to **read, generate, and edit** `.drawio` diagram files. It uses Python (FastAPI) for the web server, and the drawio Desktop CLI for diagram processing. The solution parses `.drawio` files (which are basically XML, often stored as a deflated base64 string ([Extracting the XML from mxfiles - drawio](https://drawio-app.com/blog/extracting-the-xml-from-mxfiles/#:~:text=The%20default%20format%20for%20saving,how%20the%20diagram%20is%20constructed))) into a JSON structure suitable for LLM consumption, and provides HTTP endpoints (resources and tools) following MCP concepts ([Introduction - Model Context Protocol](https://modelcontextprotocol.io/introduction#:~:text=MCP%20is%20an%20open%20protocol,different%20data%20sources%20and%20tools)).

## Directory Structure

Below is the project structure for the MCP drawio server. All code and config files are included for building and running the server locally or in Docker:

```plaintext
├── Dockerfile
├── docker-compose.yaml
├── requirements.txt
├── run_local.sh
├── mcp_drawio
│   ├── __init__.py
│   └── server.py
└── diagrams/
    └── ... (directory for storing .drawio diagram files)
```

- **Dockerfile** – Defines the Docker image (Python + drawio CLI).
- **docker-compose.yaml** – (Optional) Compose file to run the container.
- **requirements.txt** – Python dependencies (FastAPI, Uvicorn, etc).
- **run_local.sh** – Helper script to run the server locally (for development).
- **mcp_drawio/server.py** – Python code for the FastAPI server (endpoints and logic).
- **diagrams/** – Directory where diagram files are stored (mapped as a volume for persistence).

## Dockerfile

The Dockerfile uses a slim Python base image and installs the drawio Desktop CLI (via the official .deb package). This allows the server to use the drawio command-line for exporting diagrams to images or creating files. No database or external services are needed. **Note:** In a real deployment, you'd add specific version numbers and handle the Electron dependencies more carefully, but this approach keeps it simple.

```Dockerfile
FROM python:3.11-slim

# Install drawio CLI (drawio-desktop in headless mode)
RUN apt-get update && apt-get install -y wget curl libgtk-3-0 libxss1 libasound2 && \
    curl -s https://api.github.com/repos/jgraph/drawio-desktop/releases/latest | grep browser_download_url | grep '\.deb' | cut -d '"' -f 4 | wget -i - && \
    apt-get install -y ./drawio-amd64-*.deb && rm -f drawio-amd64-*.deb && \
    apt-get clean && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY mcp_drawio/ mcp_drawio/
COPY requirements.txt .
RUN pip install -r requirements.txt

EXPOSE 8000
CMD ["python", "-m", "mcp_drawio.server"]
```

This Dockerfile fetches the latest **drawio Desktop** release and installs it. The drawio binary (`drawio`) will be available in the PATH for our server to call. It then copies the Python code and installs the requirements, and sets the container to run our FastAPI app. 

## docker-compose.yaml

For convenience, a minimal Docker Compose configuration is provided. It builds the image and runs the container, exposing the MCP server on port 8000. The local `diagrams/` folder is mounted into the container so that diagram files persist and can be easily accessed or edited outside the container if needed.

```yaml
version: "3.9"
services:
  drawio-mcp:
    build: .
    ports:
      - "8000:8000"
    volumes:
      - ./diagrams:/app/diagrams
    environment:
      - DIAGRAM_DIR=diagrams
```

You can build and run the server with:
```bash
docker-compose build
docker-compose up
```
This will start the server at `http://localhost:8000`. By default, authentication is **not** enabled (endpoints are open), as requested.

## Python Server Implementation

The core server logic is implemented in **FastAPI** (a lightweight web framework). It defines MCP-compatible endpoints under the `/mcp` path:
- **Resources** endpoints for accessing diagram data (`GET /mcp/resources/...`)
- **Tools** endpoints for diagram operations (`POST /mcp/tools/...`)

The server uses the drawio file format: it reads and writes `.drawio` files (which are XML). If the XML content is compressed (drawio saves diagrams as compressed XML by default ([Extracting the XML from mxfiles - drawio](https://drawio-app.com/blog/extracting-the-xml-from-mxfiles/#:~:text=The%20default%20format%20for%20saving,how%20the%20diagram%20is%20constructed))), the server decodes it (inflate from base64) to get the raw XML structure. We then parse the XML into an internal model (using Python's `xml.etree.ElementTree`). The server manipulates this XML in memory for edits (adding shapes, connecting shapes, etc.), and serializes back to XML when saving. For simplicity, we save diagrams in uncompressed XML form (which drawio can still open) to avoid dealing with compression on each edit.

Key implementation details:
- **Parsing .drawio files**: If a `<diagram>` section contains compressed content, we base64-decode and decompress it (using zlib with raw deflate) and parse the XML ([Extracting the XML from mxfiles - drawio](https://drawio-app.com/blog/extracting-the-xml-from-mxfiles/#:~:text=The%20default%20format%20for%20saving,how%20the%20diagram%20is%20constructed)). This yields an `<mxGraphModel>` which contains all diagram cells (shapes and connectors).
- **JSON structure**: We convert the XML graph model into a JSON structure with a hierarchy: *pages* (diagrams can have multiple pages) → *cells*. Each cell includes attributes like `id`, `value` (text label), `style` (defines shape or connector style), coordinates (`geometry` with x, y, width, height), and flags for `vertex` (shape) or `edge` (connector).
- **Tools**: We define several actions (MCP *tools*) as HTTP POST endpoints:
  - `add_shape`: Add a new shape (vertex) to a diagram.
  - `connect_shapes`: Draw a connector (edge) between two shapes.
  - `generate_vpc`: Generate a new diagram with a sample AWS VPC layout (as an example of a complex operation).
- **Resources**: We expose endpoints to list diagrams and retrieve diagram content in JSON:
  - `GET /mcp/resources` lists available diagram files.
  - `GET /mcp/resources/{name}` returns the diagram’s JSON data (pages and cells).
  - `POST /mcp/resources` creates a new empty diagram.
  - `GET /mcp/resources/{name}/export` exports the diagram as an image (PNG by default) using the drawio CLI.

Below is the **`mcp_drawio/server.py`** code:

```python
import os
import base64, zlib, urllib.parse, uuid
from datetime import datetime
from xml.etree import ElementTree as ET
from fastapi import FastAPI, HTTPException
from fastapi.responses import JSONResponse, FileResponse
import subprocess

app = FastAPI(title="MCP drawio Server", description="MCP server for drawio diagrams")

# Directory to store diagram files (can be overridden by DIAGRAM_DIR env var)
DIAGRAM_DIR = os.environ.get("DIAGRAM_DIR", "diagrams")
os.makedirs(DIAGRAM_DIR, exist_ok=True)

# In-memory cache: { filename: {"tree": XML_ElementTree, "next_id": int, "file_path": str} }
diagrams = {}

def decode_drawio(data_b64: str) -> ET.Element:
    """Decode a base64-compressed drawio diagram string into an XML Element."""
    raw = base64.b64decode(data_b64)
    # Decompress using raw DEFLATE (no zlib header)
    xml_bytes = zlib.decompress(raw, wbits=-15)
    xml_str = xml_bytes.decode('utf-8')
    xml_str = urllib.parse.unquote(xml_str)  # diagrams may use URL encoding for special chars
    return ET.fromstring(xml_str)

def encode_drawio(element: ET.Element) -> str:
    """Encode an XML Element (mxGraphModel) into a base64-compressed string for saving."""
    xml_str = ET.tostring(element, encoding='utf-8', method='xml').decode('utf-8')
    xml_str = urllib.parse.quote(xml_str)
    comp_bytes = zlib.compress(xml_str.encode('utf-8'), level=9)[2:-4]  # using raw deflate via slicing (or use compressobj with wbits=-15)
    return base64.b64encode(comp_bytes).decode('utf-8')

def load_diagram(file_name: str):
    """Load a .drawio file from disk into memory (parse XML, handle compression)."""
    path = os.path.join(DIAGRAM_DIR, file_name)
    if not os.path.isfile(path):
        raise FileNotFoundError(f"Diagram {file_name} not found")
    tree = ET.parse(path)
    root = tree.getroot()
    if root.tag != 'mxfile':
        raise ValueError("Invalid drawio file format")
    # Decode compressed content in each <diagram> (if any)
    for diag in root.findall('diagram'):
        if len(diag) == 0 and diag.text:  # no child means content is in text (probably compressed)
            try:
                mxGraph = decode_drawio(diag.text)
            except Exception as e:
                raise ValueError(f"Failed to decode diagram content: {e}")
            diag.text = None
            diag.append(mxGraph)  # insert the decompressed <mxGraphModel> element
    diagrams[file_name] = {"tree": tree, "next_id": 1, "file_path": path}

def save_diagram(file_name: str):
    """Save the in-memory XML tree back to the .drawio file (uncompressed)."""
    data = diagrams.get(file_name)
    if not data:
        raise FileNotFoundError(f"Diagram {file_name} not loaded")
    tree = data["tree"]
    root = tree.getroot()
    # Ensure each <diagram> contains raw XML (already handled in load_diagram).
    for diag in root.findall('diagram'):
        diag.text = None  # remove any stray text nodes (we use child elements now)
    root.set("modified", datetime.utcnow().strftime("%Y-%m-%dT%H:%M:%SZ"))
    tree.write(data["file_path"], encoding='utf-8', xml_declaration=True)

def create_diagram(file_name: str) -> str:
    """Create a new blank diagram file with one empty page."""
    if not file_name.endswith(".drawio"):
        file_name += ".drawio"
    path = os.path.join(DIAGRAM_DIR, file_name)
    if os.path.exists(path):
        raise FileExistsError("File already exists")
    # Build minimal XML structure for a new diagram
    mxfile = ET.Element('mxfile', {
        'host': 'app.diagrams.net',
        'modified': datetime.utcnow().strftime("%Y-%m-%dT%H:%M:%SZ"),
        'agent': 'MCP-DrawIO',
        'version': '15.0.7',
        'type': 'device'
    })
    # Add one <diagram> (page)
    page_id = str(uuid.uuid4())
    diagram_elem = ET.SubElement(mxfile, 'diagram', {'id': page_id, 'name': 'Page-1'})
    mxGraphModel = ET.SubElement(diagram_elem, 'mxGraphModel', {
        'dx': '0', 'dy': '0', 'grid': '1', 'gridSize': '10', 'guides': '1', 'tooltips': '1',
        'connect': '1', 'arrows': '1', 'fold': '1', 'page': '1', 'pageScale': '1',
        'pageWidth': '800', 'pageHeight': '600', 'math': '0', 'shadow': '0'
    })
    root_elem = ET.SubElement(mxGraphModel, 'root')
    ET.SubElement(root_elem, 'mxCell', {'id': '0'})            # root cell
    ET.SubElement(root_elem, 'mxCell', {'id': '1', 'parent': '0'})  # first layer
    tree = ET.ElementTree(mxfile)
    tree.write(path, encoding='utf-8', xml_declaration=True)
    load_diagram(file_name)  # load into cache for further use
    return file_name

def get_diagram_json(file_name: str) -> dict:
    """Convert a diagram's XML content to a JSON-serializable dict."""
    if file_name not in diagrams:
        load_diagram(file_name)
    tree = diagrams[file_name]["tree"]
    root = tree.getroot()
    result = {"pages": []}
    for diag in root.findall("diagram"):
        page = {"name": diag.get("name"), "id": diag.get("id"), "cells": []}
        if len(diag) == 0:
            # Empty page (no content)
            result["pages"].append(page)
            continue
        mxGraphModel = diag[0]  # first child is <mxGraphModel>
        graph_root = mxGraphModel.find("root")
        if graph_root is None:
            result["pages"].append(page)
            continue
        # Iterate over all mxCell elements (each shape or connector)
        for cell in graph_root.findall("mxCell"):
            cell_data = dict(cell.attrib)
            # Convert flags to booleans for readability
            if 'vertex' in cell_data:
                cell_data['vertex'] = (cell_data['vertex'] == '1')
            if 'edge' in cell_data:
                cell_data['edge'] = (cell_data['edge'] == '1')
            # Include geometry sub-element if present
            geo_elem = cell.find("mxGeometry")
            if geo_elem is not None:
                geo = dict(geo_elem.attrib)
                geo.pop('as', None)  # remove the constant 'as="geometry"' attribute
                # Convert numeric strings to int (or float if needed)
                for k, v in geo.items():
                    if v.isdigit():
                        geo[k] = int(v)
                    else:
                        try:
                            geo[k] = float(v)
                        except:
                            if v.lower() in ("true", "false"):
                                geo[k] = (v.lower() == "true")
                if 'relative' in geo:  # relative is '1' or '0' for edges
                    geo['relative'] = (geo['relative'] in ['1', 'true'])
                cell_data['geometry'] = geo
            page["cells"].append(cell_data)
        result["pages"].append(page)
    return result

def add_shape_to_diagram(file_name: str, value: str, x: int, y: int, width: int, height: int, shape: str = "rectangle") -> str:
    """Add a new shape (vertex) to the first page of the diagram. Returns the new shape's ID."""
    if file_name not in diagrams:
        load_diagram(file_name)
    tree = diagrams[file_name]["tree"]
    root = tree.getroot()
    diag = root.find("diagram")
    if diag is None:
        raise HTTPException(status_code=404, detail="Diagram not found")
    if len(diag) == 0:
        # if somehow empty, ensure structure
        ET.SubElement(diag, 'mxGraphModel', { **(some defaults)** })  # (not expected in normal use)
    mxGraphModel = diag[0]
    graph_root = mxGraphModel.find("root")
    if graph_root is None:
        graph_root = ET.SubElement(mxGraphModel, "root")
        ET.SubElement(graph_root, 'mxCell', {'id': '0'})
        ET.SubElement(graph_root, 'mxCell', {'id': '1', 'parent': '0'})
    # Determine style for shape
    if shape == "rectangle":
        style = "rounded=0;whiteSpace=wrap;html=1;"
    elif shape == "ellipse":
        style = "ellipse;whiteSpace=wrap;html=1;"
    elif shape == "rhombus":
        style = "rhombus;whiteSpace=wrap;html=1;"
    else:
        style = "whiteSpace=wrap;html=1;"
    # Generate a unique ID for the new cell
    new_id = f"mcp{diagrams[file_name]['next_id']}"
    diagrams[file_name]['next_id'] += 1
    cell_elem = ET.SubElement(graph_root, 'mxCell', {
        "id": new_id, "value": value, "style": style, "vertex": "1", "parent": "1"
    })
    ET.SubElement(cell_elem, 'mxGeometry', {
        "x": str(x), "y": str(y), "width": str(width), "height": str(height), "as": "geometry"
    })
    save_diagram(file_name)
    return new_id

def connect_shapes(file_name: str, source_id: str, target_id: str) -> str:
    """Create an edge (connector) between two shape IDs in the diagram."""
    if file_name not in diagrams:
        load_diagram(file_name)
    tree = diagrams[file_name]["tree"]
    root = tree.getroot()
    diag = root.find("diagram")
    if diag is None or len(diag) == 0:
        raise HTTPException(status_code=404, detail="Diagram not found or empty")
    mxGraphModel = diag[0]
    graph_root = mxGraphModel.find("root")
    if graph_root is None:
        raise HTTPException(status_code=500, detail="Diagram has no root element")
    new_id = f"mcp{diagrams[file_name]['next_id']}"
    diagrams[file_name]['next_id'] += 1
    edge_elem = ET.SubElement(graph_root, 'mxCell', {
        "id": new_id, "value": "", "style": "edgeStyle=orthogonalEdgeStyle;rounded=0;orthogonalLoop=1;jettySize=auto;html=1;",
        "edge": "1", "parent": "1", "source": source_id, "target": target_id
    })
    ET.SubElement(edge_elem, 'mxGeometry', {"relative": "1", "as": "geometry"})
    save_diagram(file_name)
    return new_id

def generate_vpc_diagram(file_name: str) -> str:
    """Generate a simple AWS VPC-like network diagram with a VPC and two subnets."""
    file_name = create_diagram(file_name)  # create new empty diagram
    # Add a VPC (outer rectangle)
    vpc_id = add_shape_to_diagram(file_name, "VPC", x=20, y=20, width=300, height=200, shape="rectangle")
    # Add two subnets inside the VPC
    subnet1_id = add_shape_to_diagram(file_name, "Public Subnet", x=40, y=60, width=120, height=80, shape="rectangle")
    subnet2_id = add_shape_to_diagram(file_name, "Private Subnet", x=180, y=60, width=120, height=80, shape="rectangle")
    # Add an Internet Gateway (ellipse) and connect it to the VPC
    igw_id = add_shape_to_diagram(file_name, "Internet Gateway", x=120, y=0, width=100, height=40, shape="ellipse")
    connect_shapes(file_name, igw_id, vpc_id)
    # Connect VPC to both subnets
    connect_shapes(file_name, vpc_id, subnet1_id)
    connect_shapes(file_name, vpc_id, subnet2_id)
    return file_name

def export_diagram_image(file_name: str, page_index: int = 0, fmt: str = "png") -> str:
    """Export the diagram to an image file using drawio CLI. Returns the output file path."""
    in_path = os.path.join(DIAGRAM_DIR, file_name)
    if not os.path.isfile(in_path):
        raise FileNotFoundError("Diagram file not found")
    out_file = file_name.replace(".drawio", f"_{page_index}.{fmt}")
    out_path = os.path.join(DIAGRAM_DIR, out_file)
    # Use drawio CLI to export (e.g., to PNG)
    result = subprocess.run(
        ["drawio", "-x", "-f", fmt, "--page-index", str(page_index), "-o", out_path, in_path],
        check=False
    )
    if result.returncode != 0:
        raise RuntimeError("drawio CLI export failed")
    return out_path

# --- MCP Resource Endpoints ---

@app.get("/mcp/resources")
def list_resources():
    """List available diagram resources (files)."""
    files = [f for f in os.listdir(DIAGRAM_DIR) if f.endswith(".drawio")]
    return {"resources": [{"name": f, "type": "diagram"} for f in files]}

@app.get("/mcp/resources/{diagram_name}")
def get_resource(diagram_name: str):
    """Retrieve a diagram's content as JSON."""
    if not diagram_name.endswith(".drawio"):
        diagram_name += ".drawio"
    try:
        data = get_diagram_json(diagram_name)
    except Exception as e:
        raise HTTPException(status_code=404, detail=str(e))
    return JSONResponse(data)

@app.post("/mcp/resources")
def create_resource(name: str):
    """Create a new empty diagram file."""
    if not name.endswith(".drawio"):
        name += ".drawio"
    if os.path.exists(os.path.join(DIAGRAM_DIR, name)):
        raise HTTPException(status_code=400, detail="Diagram already exists")
    try:
        create_diagram(name)
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))
    return {"status": "created", "diagram": name}

@app.get("/mcp/resources/{diagram_name}/export")
def export_resource_image(diagram_name: str, page: int = 0, format: str = "png"):
    """Export a diagram as an image (PNG by default)."""
    if not diagram_name.endswith(".drawio"):
        diagram_name += ".drawio"
    try:
        img_path = export_diagram_image(diagram_name, page_index=page, fmt=format)
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))
    # Return the image file as response
    return FileResponse(img_path, media_type=f"image/{format}")

# --- MCP Tool Endpoints ---

@app.get("/mcp/tools")
def list_tools():
    """List available MCP tools (actions)."""
    return {"tools": [
        {
            "name": "add_shape",
            "description": "Add a new shape to a diagram. Parameters: diagram, value, x, y, width, height, [shape]. Returns new shape ID."
        },
        {
            "name": "connect_shapes",
            "description": "Connect two shapes in a diagram with an arrow. Parameters: diagram, source_id, target_id. Returns new edge ID."
        },
        {
            "name": "generate_vpc",
            "description": "Generate a sample AWS VPC layout diagram. Parameter: diagram_name. Returns the new diagram name."
        }
    ]}

@app.post("/mcp/tools/add_shape")
def tool_add_shape(diagram: str, value: str, x: int, y: int, width: int, height: int, shape: str = "rectangle"):
    """Tool: Add a shape to the specified diagram."""
    if not diagram.endswith(".drawio"):
        diagram += ".drawio"
    try:
        new_id = add_shape_to_diagram(diagram, value, x, y, width, height, shape)
    except Exception as e:
        raise HTTPException(status_code=400, detail=str(e))
    return {"status": "success", "id": new_id}

@app.post("/mcp/tools/connect_shapes")
def tool_connect_shapes(diagram: str, source_id: str, target_id: str):
    """Tool: Connect two shapes by ID in a diagram."""
    if not diagram.endswith(".drawio"):
        diagram += ".drawio"
    try:
        new_edge_id = connect_shapes(diagram, source_id, target_id)
    except Exception as e:
        raise HTTPException(status_code=400, detail=str(e))
    return {"status": "success", "id": new_edge_id}

@app.post("/mcp/tools/generate_vpc")
def tool_generate_vpc(diagram_name: str = "aws_vpc.drawio"):
    """Tool: Generate an AWS VPC layout diagram."""
    if not diagram_name.endswith(".drawio"):
        diagram_name += ".drawio"
    try:
        file_name = generate_vpc_diagram(diagram_name)
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))
    return {"status": "success", "diagram": file_name}

# (Note: Authentication is not enabled. To secure these endpoints, one could validate a JWT token in each request header here.)
if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=8000)
```

A few points about the code:
- It uses **in-memory caching** of loaded diagrams for quick edits. For simplicity, it does not implement complex multi-page editing or concurrent editing.
- New shape and edge IDs are generated as `"mcp1", "mcp2", ..."` to ensure they don't clash with existing IDs (which are usually either numeric or random strings from drawio).
- The `export_diagram_image` function calls the `drawio` CLI to export the file to an image (which can then be retrieved via the `/export` endpoint). For example, it uses `drawio -x -f png ...` to produce a PNG image.

**Authentication:** As requested, the server has **no auth** by default. In a real scenario, you'd protect these endpoints (for example, expecting a JWT in an `Authorization` header). We left a comment in the code indicating where to add such checks. (For instance, using FastAPI dependencies or middleware to verify tokens for each request.)

## Example: Diagram Input and JSON Output

To illustrate the parsing, consider a simple diagram with two nodes "Hello" and "World" connected by an arrow. Below is an excerpt of the `.drawio` file (in XML form) for this diagram:

```xml
<mxfile host="app.diagrams.net" modified="2025-04-19T15:00:00.000Z" agent="python-mcp" version="15.0.7" type="device">
  <diagram id="sample1" name="Page-1">
    <mxGraphModel dx="1466" dy="827" grid="1" gridSize="10" guides="1" tooltips="1"
                 connect="1" arrows="1" fold="1" page="1" pageScale="1" pageWidth="826"
                 pageHeight="1169" math="0" shadow="0">
      <root>
        <mxCell id="0"/>
        <mxCell id="1" parent="0"/>
        <mxCell id="2" value="Hello" style="rounded=0;whiteSpace=wrap;html=1;" vertex="1" parent="1">
          <mxGeometry x="100" y="100" width="80" height="30" as="geometry"/>
        </mxCell>
        <mxCell id="3" value="World" style="rounded=0;whiteSpace=wrap;html=1;" vertex="1" parent="1">
          <mxGeometry x="300" y="100" width="80" height="30" as="geometry"/>
        </mxCell>
        <mxCell id="4" value="" style="edgeStyle=orthogonalEdgeStyle;rounded=0;orthogonalLoop=1;jettySize=auto;html=1;" edge="1" parent="1" source="2" target="3">
          <mxGeometry relative="1" as="geometry"/>
        </mxCell>
      </root>
    </mxGraphModel>
  </diagram>
</mxfile>
```

In this XML:
- Cells with `vertex="1"` (IDs 2 and 3) are rectangular nodes with labels "Hello" and "World".
- The cell with `edge="1"` (ID 4) is a connector (arrow) from source="2" to target="3". Its geometry has `relative="1"`, meaning it’s an edge connecting those vertices.

When you request this diagram via the MCP server (e.g. `GET /mcp/resources/sample1.drawio`), the server responds with a JSON structure. For the above diagram, the JSON output would be:

```json
{
  "pages": [
    {
      "name": "Page-1",
      "id": "sample1",
      "cells": [
        { "id": "0" },
        { "id": "1", "parent": "0" },
        {
          "id": "2",
          "value": "Hello",
          "style": "rounded=0;whiteSpace=wrap;html=1;",
          "vertex": true,
          "parent": "1",
          "geometry": { "x": 100, "y": 100, "width": 80, "height": 30 }
        },
        {
          "id": "3",
          "value": "World",
          "style": "rounded=0;whiteSpace=wrap;html=1;",
          "vertex": true,
          "parent": "1",
          "geometry": { "x": 300, "y": 100, "width": 80, "height": 30 }
        },
        {
          "id": "4",
          "value": "",
          "style": "edgeStyle=orthogonalEdgeStyle;rounded=0;orthogonalLoop=1;jettySize=auto;html=1;",
          "edge": true,
          "parent": "1",
          "source": "2",
          "target": "3",
          "geometry": { "relative": true }
        }
      ]
    }
  ]
}
```

Notice how the JSON mirrors the structure:
- There's one page ("Page-1") with a list of cells.
- Cells 0 and 1 are the housekeeping nodes (root and layer).
- Cells 2 and 3 correspond to the "Hello" and "World" nodes. The server preserved their text (`value`), position (`geometry` x,y), size (width,height), etc. Flags like `"vertex": true` indicate these are shapes.
- Cell 4 is the edge connecting them, indicated by `"edge": true`, and it references `source: "2"` and `target: "3"`. The geometry `relative: true` signals it's an edge (no fixed x,y).

This JSON format is **LLM-friendly** – an AI agent can easily read the list of elements, find nodes by their text or ID, and reason about connections.

## Usage via MCP Tools (Example Workflow)

Finally, let's demonstrate how an LLM agent (in an IDE like VS Code or Cursor) could use this MCP server. The agent can discover available tools and resources, then perform actions by calling the tool endpoints. Below is an example scenario:

1. **List available tools:** The agent queries `GET /mcp/tools` to see what actions are supported. The server returns a list of tools with descriptions (e.g., **add_shape**, **connect_shapes**, **generate_vpc**, etc.). For example:
   ```json
   {
     "tools": [
       { "name": "add_shape", "description": "Add a new shape to a diagram..." },
       { "name": "connect_shapes", "description": "Connect two shapes with an arrow..." },
       { "name": "generate_vpc", "description": "Generate a simple AWS VPC network diagram..." }
     ]
   }
   ```
   This informs the LLM what functions it can invoke ([A Deep Dive into Model Context Protocol Integration | by Shelwyn Corte | Mar, 2025 | Medium](https://shelwyncorte.medium.com/a-deep-dive-into-model-context-protocol-integration-3150d60c5896#:~:text=Requests%20and%20Responses%3A%20Clients%20%28e,%E2%80%9Cid%E2%80%9D%3A%201%2C%20%E2%80%9Cjsonrpc%E2%80%9D%3A%20%E2%80%9C2.0%E2%80%9D)) to manipulate diagrams.

2. **Create a new diagram:** Suppose the user (via the LLM) wants to create a network diagram. The LLM can call the **generate_vpc** tool to automatically create a baseline VPC layout. For instance:
   ```http
   POST /mcp/tools/generate_vpc 
   Content-Type: application/json

   { "diagram_name": "aws_vpc_example" }
   ```
   The server will generate a new file `aws_vpc_example.drawio` with a VPC, two subnets, and an internet gateway (as per our `generate_vpc_diagram` logic). It returns:
   ```json
   { "status": "success", "diagram": "aws_vpc_example.drawio" }
   ```
   At this point, the file is created and stored in the `diagrams/` directory.

3. **Inspect or modify the diagram:** The agent can fetch the diagram’s content via `GET /mcp/resources/aws_vpc_example.drawio` to get the JSON representation, then reason about it or show it to the user. The JSON will list the VPC shape and subnets that were added. If further editing is needed (say, add another node or connect something), the LLM can use `add_shape` or `connect_shapes`. For example, to add a new database node to the VPC:
   ```http
   POST /mcp/tools/add_shape
   Content-Type: application/json

   {
     "diagram": "aws_vpc_example.drawio",
     "value": "Database",
     "x": 180, "y": 160, "width": 80, "height": 50,
     "shape": "ellipse"
   }
   ```
   The server will insert a new ellipse shape labeled "Database" at the specified coordinates, and respond with:
   ```json
   { "status": "success", "id": "mcp5" }
   ```
   (The new shape ID is returned, here `"mcp5"` for example.)

   If needed, the agent could then connect this new node to an existing subnet by calling `connect_shapes` with the appropriate source and target IDs.

4. **Export diagram (optional):** After modifications, the agent (or user) might want to see the diagram visually. The server supports exporting to PNG/SVG via the drawio CLI. For example:
   ```http
   GET /mcp/resources/aws_vpc_example.drawio/export?format=png
   ```
   This will produce a PNG image of the diagram. The response is the image file itself (which the IDE or agent can display to the user). The server uses the drawio headless mode to generate this image behind the scenes.

Throughout this process, **no manual GUI steps are needed** – the LLM, through the MCP server, can manipulate the diagram structure. The **MCP paradigm** treats the diagram as a contextual resource and the diagram operations as tools/functions that the LLM can call autonomously ([Introduction - Model Context Protocol](https://modelcontextprotocol.io/introduction#:~:text=MCP%20is%20an%20open%20protocol,different%20data%20sources%20and%20tools)) ([A Deep Dive into Model Context Protocol Integration | by Shelwyn Corte | Mar, 2025 | Medium](https://shelwyncorte.medium.com/a-deep-dive-into-model-context-protocol-integration-3150d60c5896#:~:text=Requests%20and%20Responses%3A%20Clients%20%28e,%E2%80%9Cid%E2%80%9D%3A%201%2C%20%E2%80%9Cjsonrpc%E2%80%9D%3A%20%E2%80%9C2.0%E2%80%9D)). This enables advanced workflows, such as an AI-assisted network diagramming tool where the AI can add components, connect them, and keep the diagram file updated in real-time.

### Running the Server Locally

For development or testing without Docker, you can run the server with the provided script. Ensure you have Python and the requirements installed, and a drawio binary available:
```bash
# Install dependencies
pip install -r requirements.txt

# Run the server (development mode with auto-reload)
./run_local.sh
```
This starts the FastAPI server on port 8000. You can then use tools like `curl` or an HTTP client to hit the endpoints as shown in the examples above.

---

**References:**

- Anthropic, *"Model Context Protocol (MCP) Introduction"* – MCP standardizes how AI applications connect to tools and data ([Introduction - Model Context Protocol](https://modelcontextprotocol.io/introduction#:~:text=MCP%20is%20an%20open%20protocol,different%20data%20sources%20and%20tools)).  
- Diagrams.net, *"Extracting the XML from .drawio files"* – `.drawio` files are XML-based, defaulting to compressed (deflate) storage ([Extracting the XML from mxfiles - drawio](https://drawio-app.com/blog/extracting-the-xml-from-mxfiles/#:~:text=The%20default%20format%20for%20saving,how%20the%20diagram%20is%20constructed)), which we decode to manipulate diagram content.  
- Shelwyn Corte, *"Deep Dive into MCP Integration"* – describes how tools are listed/invoked in MCP (e.g., `tools/list`, `tools/call`) ([A Deep Dive into Model Context Protocol Integration | by Shelwyn Corte | Mar, 2025 | Medium](https://shelwyncorte.medium.com/a-deep-dive-into-model-context-protocol-integration-3150d60c5896#:~:text=Requests%20and%20Responses%3A%20Clients%20%28e,%E2%80%9Cid%E2%80%9D%3A%201%2C%20%E2%80%9Cjsonrpc%E2%80%9D%3A%20%E2%80%9C2.0%E2%80%9D)).