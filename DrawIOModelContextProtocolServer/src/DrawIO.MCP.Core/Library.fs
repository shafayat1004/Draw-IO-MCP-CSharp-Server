namespace DrawIO.MCP.Core

open System
open System.IO
open System.Xml
open System.Xml.Linq
open System.Text
open System.IO.Compression

/// Types for representing DrawIO diagram elements
module Types =
    /// A position in the diagram
    type Position = {
        X: float
        Y: float
    }

    /// Size of an element
    type Size = {
        Width: float
        Height: float
    }

    /// Geometry of an element
    type Geometry = {
        Position: Position
        Size: Size
        Relative: bool
    }

    /// An element in the diagram
    type Element = {
        Id: string
        Value: string
        Style: string
        IsVertex: bool
        IsEdge: bool
        Parent: string
        Source: string option
        Target: string option
        Geometry: Geometry option
    }
    
    /// A diagram page
    type Page = {
        Id: string
        Name: string
        Cells: Element list
    }
    
    /// A complete diagram
    type Diagram = {
        Modified: DateTime
        Pages: Page list
    }
    
    /// Query result for element info
    type ElementInfo = {
        Id: string
        Type: string
        Value: string
        Position: Position option
        Size: Size option
        Style: string
        Parent: string
        Connections: (string * string) list
    }
    
    /// Bounding box of a diagram
    type BoundingBox = {
        MinX: float
        MinY: float
        MaxX: float
        MaxY: float
        Width: float
        Height: float
    }

/// Functions for parsing and manipulating DrawIO XML
module XmlParser =
    open Types
    
    /// Decode a base64-compressed draw.io diagram string into an XML Element
    let decodeDrawioXml (data: string) =
        try
            let rawBytes = Convert.FromBase64String(data)
            use stream = new MemoryStream(rawBytes)
            use deflateStream = new DeflateStream(stream, CompressionMode.Decompress)
            use reader = new StreamReader(deflateStream, Encoding.UTF8)
            let xml = reader.ReadToEnd()
            XDocument.Parse(xml)
        with ex ->
            raise <| Exception($"Failed to decode diagram content: {ex.Message}", ex)
    
    /// Parse geometry attributes from an mxCell element
    let parseGeometry (geometryElement: XElement) =
        if geometryElement = null then None
        else
            try
                let x = 
                    match geometryElement.Attribute(XName.Get("x")) with 
                    | null -> 0.0 
                    | attr -> Double.Parse(attr.Value)
                
                let y = 
                    match geometryElement.Attribute(XName.Get("y")) with 
                    | null -> 0.0 
                    | attr -> Double.Parse(attr.Value)
                
                let width = 
                    match geometryElement.Attribute(XName.Get("width")) with 
                    | null -> 0.0 
                    | attr -> Double.Parse(attr.Value)
                
                let height = 
                    match geometryElement.Attribute(XName.Get("height")) with 
                    | null -> 0.0 
                    | attr -> Double.Parse(attr.Value)
                
                let relative = 
                    match geometryElement.Attribute(XName.Get("relative")) with 
                    | null -> false 
                    | attr -> attr.Value = "1"
                
                Some {
                    Position = { X = x; Y = y }
                    Size = { Width = width; Height = height }
                    Relative = relative
                }
            with ex ->
                None
    
    /// Parse an mxCell element into a DrawIO element
    let parseCell (cellElement: XElement) =
        let id = 
            match cellElement.Attribute(XName.Get("id")) with 
            | null -> Guid.NewGuid().ToString() 
            | attr -> attr.Value
        
        let value = 
            match cellElement.Attribute(XName.Get("value")) with 
            | null -> "" 
            | attr -> attr.Value
        
        let style = 
            match cellElement.Attribute(XName.Get("style")) with 
            | null -> "" 
            | attr -> attr.Value
        
        let isVertex = 
            match cellElement.Attribute(XName.Get("vertex")) with 
            | null -> false 
            | attr -> attr.Value = "1"
        
        let isEdge = 
            match cellElement.Attribute(XName.Get("edge")) with 
            | null -> false 
            | attr -> attr.Value = "1"
        
        let parent = 
            if id = "0" then ""  // Root cell should never have a parent
            else 
                match cellElement.Attribute(XName.Get("parent")) with 
                | null -> "1" 
                | attr -> attr.Value
        
        let source = 
            match cellElement.Attribute(XName.Get("source")) with 
            | null -> None 
            | attr -> Some attr.Value
        
        let target = 
            match cellElement.Attribute(XName.Get("target")) with 
            | null -> None 
            | attr -> Some attr.Value
        
        let geometry = 
            cellElement.Element(XName.Get("mxGeometry"))
            |> parseGeometry
        
        {
            Id = id
            Value = value
            Style = style
            IsVertex = isVertex
            IsEdge = isEdge
            Parent = parent
            Source = source
            Target = target
            Geometry = geometry
        }
    
    /// Parse a diagram page from an mxGraphModel element
    let parsePage (pageElement: XElement) (name: string) (pageId: string) =
        let rootElement = pageElement.Element(XName.Get("root"))
        
        let cells =
            if rootElement = null then []
            else
                rootElement.Elements(XName.Get("mxCell"))
                |> Seq.map parseCell
                |> Seq.toList
        
        {
            Id = pageId
            Name = name
            Cells = cells
        }
    
    /// Parse a DrawIO diagram file
    let parseDiagram (xmlContent: string) =
        try
            let doc = XDocument.Parse(xmlContent)
            let root = doc.Root
            
            if root.Name.LocalName <> "mxfile" then
                raise <| Exception("Invalid draw.io file format")
            
            let modified = 
                match root.Attribute(XName.Get("modified")) with
                | null -> DateTime.Now
                | attr -> DateTime.Parse(attr.Value)
            
            let pages = 
                root.Elements(XName.Get("diagram"))
                |> Seq.map (fun diag ->
                    let pageId = 
                        match diag.Attribute(XName.Get("id")) with
                        | null -> Guid.NewGuid().ToString()
                        | attr -> attr.Value
                    
                    let pageName = 
                        match diag.Attribute(XName.Get("name")) with
                        | null -> "Page-" + pageId
                        | attr -> attr.Value
                    
                    let graphModel = 
                        if diag.HasElements then
                            diag.Element(XName.Get("mxGraphModel"))
                        else if not (String.IsNullOrEmpty(diag.Value)) then
                            // Handle compressed content
                            let decoded = decodeDrawioXml(diag.Value)
                            decoded.Root
                        else
                            null
                    
                    match graphModel with
                    | null -> 
                        // Create empty page
                        { Id = pageId; Name = pageName; Cells = [] }
                    | model -> 
                        parsePage model pageName pageId
                )
                |> Seq.toList
            
            { 
                Modified = modified
                Pages = pages
            }
        with ex ->
            raise <| Exception($"Error parsing diagram: {ex.Message}", ex)

/// Functions for diagram manipulation operations
module DiagramManipulation =
    open Types
    open System

    /// Generate a new element ID
    let generateId () = "mcp_" + Guid.NewGuid().ToString("N").Substring(0, 8)
    
    /// Create a new vertex element
    let createVertex (id: string) (value: string) (x: float) (y: float) (width: float) (height: float) (style: string) (parent: string) =
        {
            Id = id
            Value = value
            Style = style
            IsVertex = true
            IsEdge = false
            Parent = parent
            Source = None
            Target = None
            Geometry = Some {
                Position = { X = x; Y = y }
                Size = { Width = width; Height = height }
                Relative = false
            }
        }
    
    /// Create a new edge element
    let createEdge (id: string) (sourceId: string) (targetId: string) (style: string) (parent: string) =
        {
            Id = id
            Value = ""
            Style = style
            IsVertex = false
            IsEdge = true
            Parent = parent
            Source = Some sourceId
            Target = Some targetId
            Geometry = Some {
                Position = { X = 0.0; Y = 0.0 }
                Size = { Width = 0.0; Height = 0.0 }
                Relative = true
            }
        }
    
    /// Creates an empty diagram with a single page
    let createEmptyDiagram() =
        {
            Modified = DateTime.Now
            Pages = [
                {
                    Id = Guid.NewGuid().ToString()
                    Name = "Page-1"
                    Cells = [
                        // Root cell
                        {
                            Id = "0"
                            Value = ""
                            Style = ""
                            IsVertex = false
                            IsEdge = false
                            Parent = ""
                            Source = None
                            Target = None
                            Geometry = None
                        }
                        // Default layer
                        {
                            Id = "1"
                            Value = ""
                            Style = ""
                            IsVertex = true
                            IsEdge = false
                            Parent = "0"
                            Source = None
                            Target = None
                            Geometry = None
                        }
                    ]
                }
            ]
        }

    /// Adds a shape to a diagram page
    let addShape (diagram: Diagram) (pageIndex: int) (value: string) (x: float) (y: float) (width: float) (height: float) (shape: string) =
        if pageIndex < 0 || pageIndex >= diagram.Pages.Length then
            raise <| IndexOutOfRangeException("Page index out of range")
        
        let page = diagram.Pages.[pageIndex]
        let shapeId = Guid.NewGuid().ToString()
        
        let shapeStyle = 
            match shape.ToLowerInvariant() with
            | "rectangle" -> "rounded=0;whiteSpace=wrap;html=1;"
            | "ellipse" -> "ellipse;whiteSpace=wrap;html=1;"
            | "circle" -> "ellipse;whiteSpace=wrap;html=1;aspect=fixed;"
            | "triangle" -> "triangle;whiteSpace=wrap;html=1;"
            | "rhombus" -> "rhombus;whiteSpace=wrap;html=1;"
            | "hexagon" -> "shape=hexagon;perimeter=hexagonPerimeter2;whiteSpace=wrap;html=1;"
            | "cloud" -> "ellipse;shape=cloud;whiteSpace=wrap;html=1;"
            | "document" -> "shape=document;whiteSpace=wrap;html=1;boundedLbl=1;"
            | "cylinder" -> "shape=cylinder;whiteSpace=wrap;html=1;"
            | "diamond" -> "shape=diamond;whiteSpace=wrap;html=1;"
            | "process" -> "shape=process;whiteSpace=wrap;html=1;"
            | "actor" -> "shape=umlActor;verticalLabelPosition=bottom;verticalAlign=top;html=1;"
            | "note" -> "shape=note;whiteSpace=wrap;html=1;size=14;fillColor=#FFFF99;"
            | _ -> "rounded=0;whiteSpace=wrap;html=1;"
        
        let newShape = {
            Id = shapeId
            Value = value
            Style = shapeStyle
            IsVertex = true
            IsEdge = false
            Parent = "1" // Default layer
            Source = None
            Target = None
            Geometry = Some {
                Position = { X = x; Y = y }
                Size = { Width = width; Height = height }
                Relative = false
            }
        }
        
        let updatedPage = {
            page with
                Cells = page.Cells @ [newShape]
        }
        
        let updatedPages = 
            diagram.Pages
            |> List.mapi (fun i p -> if i = pageIndex then updatedPage else p)
        
        let updatedDiagram = {
            diagram with
                Modified = DateTime.Now
                Pages = updatedPages
        }
        
        (updatedDiagram, shapeId)

    /// Connects two shapes with an edge
    let connectShapes (diagram: Diagram) (pageIndex: int) (sourceId: string) (targetId: string) =
        if pageIndex < 0 || pageIndex >= diagram.Pages.Length then
            raise <| IndexOutOfRangeException("Page index out of range")
        
        let page = diagram.Pages.[pageIndex]
        
        // Check if source and target exist
        let sourceExists = page.Cells |> List.exists (fun cell -> cell.Id = sourceId)
        let targetExists = page.Cells |> List.exists (fun cell -> cell.Id = targetId)
        
        if not sourceExists then
            raise <| ArgumentException($"Source shape with ID {sourceId} not found")
        
        if not targetExists then
            raise <| ArgumentException($"Target shape with ID {targetId} not found")
        
        let edgeId = Guid.NewGuid().ToString()
        
        let edge = {
            Id = edgeId
            Value = ""
            Style = "endArrow=classic;html=1;rounded=0;"
            IsVertex = false
            IsEdge = true
            Parent = "1" // Default layer
            Source = Some sourceId
            Target = Some targetId
            Geometry = Some {
                Position = { X = 0.0; Y = 0.0 }
                Size = { Width = 0.0; Height = 0.0 }
                Relative = true
            }
        }
        
        let updatedPage = {
            page with
                Cells = page.Cells @ [edge]
        }
        
        let updatedPages = 
            diagram.Pages
            |> List.mapi (fun i p -> if i = pageIndex then updatedPage else p)
        
        let updatedDiagram = {
            diagram with
                Modified = DateTime.Now
                Pages = updatedPages
        }
        
        (updatedDiagram, edgeId)

    /// Deletes a shape from a diagram page
    let deleteShape (diagram: Diagram) (pageIndex: int) (shapeId: string) =
        if pageIndex < 0 || pageIndex >= diagram.Pages.Length then
            raise <| IndexOutOfRangeException("Page index out of range")
        
        let page = diagram.Pages.[pageIndex]
        
        // Check if shape exists
        match page.Cells |> List.tryFind (fun cell -> cell.Id = shapeId) with
        | None -> 
            raise <| ArgumentException($"Shape with ID {shapeId} not found")
        | Some _ ->
            // Remove the shape and any connected edges
            let updatedCells = page.Cells 
                              |> List.filter (fun cell -> 
                                 cell.Id <> shapeId && 
                                 cell.Source <> Some shapeId && 
                                 cell.Target <> Some shapeId)
            
            let updatedPage = {
                page with
                    Cells = updatedCells
            }
            
            let updatedPages = 
                diagram.Pages
                |> List.mapi (fun i p -> if i = pageIndex then updatedPage else p)
            
            let updatedDiagram = {
                diagram with
                    Modified = DateTime.Now
                    Pages = updatedPages
            }
            
            updatedDiagram

    /// Updates properties of an existing shape
    let updateShape (diagram: Diagram) (pageIndex: int) (shapeId: string) (value: string) (x: float option) (y: float option) (width: float option) (height: float option) (style: string option) =
        if pageIndex < 0 || pageIndex >= diagram.Pages.Length then
            raise <| IndexOutOfRangeException("Page index out of range")
        
        let page = diagram.Pages.[pageIndex]
        
        // Find the shape to update
        match page.Cells |> List.tryFindIndex (fun cell -> cell.Id = shapeId) with
        | None -> 
            raise <| ArgumentException($"Shape with ID {shapeId} not found")
        | Some cellIndex ->
            let cell = page.Cells.[cellIndex]
            
            // Update the geometry if needed
            let updatedGeometry = 
                match cell.Geometry with
                | None -> None
                | Some geo ->
                    let updatedPosition = {
                        X = defaultArg x geo.Position.X
                        Y = defaultArg y geo.Position.Y
                    }
                    
                    let updatedSize = {
                        Width = defaultArg width geo.Size.Width
                        Height = defaultArg height geo.Size.Height
                    }
                    
                    Some {
                        Position = updatedPosition
                        Size = updatedSize
                        Relative = geo.Relative
                    }
            
            // Update the style if provided
            let updatedStyle = 
                match style with
                | None -> cell.Style
                | Some s -> 
                    if cell.Style.Contains(";") && s.Contains(";") then
                        // Merge styles
                        let existingStyles = cell.Style.Split([|';'|], StringSplitOptions.RemoveEmptyEntries)
                                          |> Array.map (fun part -> part.Trim())
                                          |> Set.ofArray
                        
                        let newStyles = s.Split([|';'|], StringSplitOptions.RemoveEmptyEntries)
                                      |> Array.map (fun part -> part.Trim())
                                      |> Set.ofArray
                        
                        let combinedStyles = Set.union existingStyles newStyles
                        String.Join(";", combinedStyles) + ";"
                    else
                        s
            
            // Create updated cell
            let updatedCell = {
                cell with
                    Value = if value <> null then value else cell.Value
                    Style = updatedStyle
                    Geometry = updatedGeometry
            }
            
            // Update the cell in the page
            let updatedCells = 
                page.Cells
                |> List.mapi (fun i c -> if i = cellIndex then updatedCell else c)
            
            let updatedPage = {
                page with
                    Cells = updatedCells
            }
            
            let updatedPages = 
                diagram.Pages
                |> List.mapi (fun i p -> if i = pageIndex then updatedPage else p)
            
            let updatedDiagram = {
                diagram with
                    Modified = DateTime.Now
                    Pages = updatedPages
            }
            
            updatedDiagram

    /// Arranges shapes in a diagram according to a specific layout
    let arrangeLayout (diagram: Diagram) (pageIndex: int) (layout: string) =
        if pageIndex < 0 || pageIndex >= diagram.Pages.Length then
            raise <| IndexOutOfRangeException("Page index out of range")
        
        let page = diagram.Pages.[pageIndex]
        
        // Get all vertex cells with geometry
        let vertices = 
            page.Cells 
            |> List.filter (fun c -> c.IsVertex && c.Geometry.IsSome && c.Id <> "0" && c.Id <> "1")
        
        if vertices.IsEmpty then
            // No shapes to arrange
            diagram
        else
            // Get all edges
            let edges = page.Cells |> List.filter (fun c -> c.IsEdge)
            
            // Perform layout based on the requested type
            let updatedVertices =
                match layout.ToLowerInvariant() with
                | "horizontal" ->
                    // Arrange horizontally with equal spacing
                    let count = vertices.Length
                    let spacing = 150.0
                    let totalWidth = spacing * float(count - 1)
                    let startX = 50.0
                    let y = 100.0
                    
                    vertices
                    |> List.mapi (fun i vertex ->
                        let updatedGeo = 
                            match vertex.Geometry with
                            | None -> None
                            | Some geo ->
                                let newX = startX + (float i * spacing)
                                Some { geo with Position = { geo.Position with X = newX; Y = y } }
                        { vertex with Geometry = updatedGeo }
                    )
                
                | "vertical" ->
                    // Arrange vertically with equal spacing
                    let count = vertices.Length
                    let spacing = 100.0
                    let totalHeight = spacing * float(count - 1)
                    let x = 100.0
                    let startY = 50.0
                    
                    vertices
                    |> List.mapi (fun i vertex ->
                        let updatedGeo = 
                            match vertex.Geometry with
                            | None -> None
                            | Some geo ->
                                let newY = startY + (float i * spacing)
                                Some { geo with Position = { geo.Position with X = x; Y = newY } }
                        { vertex with Geometry = updatedGeo }
                    )
                
                | "circle" ->
                    // Arrange in a circle
                    let count = vertices.Length
                    let radius = 200.0
                    let centerX = 250.0
                    let centerY = 250.0
                    
                    vertices
                    |> List.mapi (fun i vertex ->
                        let updatedGeo = 
                            match vertex.Geometry with
                            | None -> None
                            | Some geo ->
                                let angle = 2.0 * Math.PI * float i / float count
                                let newX = centerX + radius * Math.Cos(angle)
                                let newY = centerY + radius * Math.Sin(angle)
                                Some { geo with Position = { geo.Position with X = newX; Y = newY } }
                        { vertex with Geometry = updatedGeo }
                    )
                
                | "grid" ->
                    // Arrange in a grid
                    let count = vertices.Length
                    let cols = int(Math.Ceiling(Math.Sqrt(float count)))
                    let spacing = 150.0
                    
                    vertices
                    |> List.mapi (fun i vertex ->
                        let updatedGeo = 
                            match vertex.Geometry with
                            | None -> None
                            | Some geo ->
                                let row = i / cols
                                let col = i % cols
                                let newX = 50.0 + (float col * spacing)
                                let newY = 50.0 + (float row * spacing)
                                Some { geo with Position = { geo.Position with X = newX; Y = newY } }
                        { vertex with Geometry = updatedGeo }
                    )
                
                | _ -> 
                    // Default to horizontal layout
                    let count = vertices.Length
                    let spacing = 150.0
                    let totalWidth = spacing * float(count - 1)
                    let startX = 50.0
                    let y = 100.0
                    
                    vertices
                    |> List.mapi (fun i vertex ->
                        let updatedGeo = 
                            match vertex.Geometry with
                            | None -> None
                            | Some geo ->
                                let newX = startX + (float i * spacing)
                                Some { geo with Position = { geo.Position with X = newX; Y = y } }
                        { vertex with Geometry = updatedGeo }
                    )
            
            // Combine the updated vertices with other cells
            let nonVertices = page.Cells |> List.filter (fun c -> not (c.IsVertex && c.Geometry.IsSome && c.Id <> "0" && c.Id <> "1"))
            let updatedCells = nonVertices @ updatedVertices
            
            let updatedPage = {
                page with
                    Cells = updatedCells
            }
            
            let updatedPages = 
                diagram.Pages
                |> List.mapi (fun i p -> if i = pageIndex then updatedPage else p)
            
            let updatedDiagram = {
                diagram with
                    Modified = DateTime.Now
                    Pages = updatedPages
            }
            
            updatedDiagram

    /// Move a shape to a new position
    let moveShape (diagram: Diagram) (shape_id: string) (x: float) (y: float) =
        // Create a modified copy of the diagram
        { diagram with 
            Pages = 
                diagram.Pages |> List.map (fun page -> 
                    { page with 
                        Cells = 
                            page.Cells |> List.map (fun cell -> 
                                if cell.Id = shape_id && cell.Geometry.IsSome then
                                    let geometry = cell.Geometry.Value
                                    { cell with 
                                        Geometry = Some { geometry with 
                                                            Position = { X = x; Y = y } } }
                                else
                                    cell) }) }

    /// Updates the style of a shape
    let updateShapeStyle (diagram: Diagram) (shape_id: string) (styleProperties: Map<string, string>) =
        // Create a modified copy of the diagram
        { diagram with 
            Pages = 
                diagram.Pages |> List.map (fun page -> 
                    { page with 
                        Cells = 
                            page.Cells |> List.map (fun cell -> 
                                if cell.Id = shape_id then
                                    let updatedStyle = 
                                        styleProperties
                                        |> Map.fold (fun style key value -> 
                                            style + key + "=" + value + ";") ""
                                    { cell with Style = updatedStyle }
                                else
                                    cell) }) }

    /// Automatically arrange shapes in a diagram using a basic layout algorithm
    let arrangeDiagram (diagram: Diagram) (pageId: string option) =
        // Create a modified copy of the diagram
        { diagram with 
            Pages = 
                diagram.Pages |> List.map (fun page -> 
                    // Only arrange the specified page or all pages if pageId is None
                    if pageId.IsNone || pageId.Value = page.Id then
                        // Get all vertices
                        let vertices = 
                            page.Cells 
                            |> List.filter (fun cell -> cell.IsVertex && cell.Geometry.IsSome)
                            
                        // Get all edges
                        let edges = 
                            page.Cells 
                            |> List.filter (fun cell -> cell.IsEdge)
                        
                        // Simple grid-based layout
                        let gridSize = 150.0
                        let startX = 50.0
                        let startY = 50.0
                        let maxPerRow = 5
                        
                        let arrangedVertices = 
                            vertices
                            |> List.mapi (fun i cell ->
                                let row = i / maxPerRow
                                let col = i % maxPerRow
                                let newX = startX + (float col * gridSize)
                                let newY = startY + (float row * gridSize)
                                let geometry = cell.Geometry.Value
                                
                                { cell with 
                                    Geometry = Some { geometry with Position = { X = newX; Y = newY } } }
                            )
                        
                        // Create a map of cell IDs to their new positions for updating edges
                        let positionMap = 
                            arrangedVertices 
                            |> List.map (fun cell -> 
                                let geometry = cell.Geometry.Value
                                (cell.Id, (geometry.Position.X + geometry.Size.Width/2.0, 
                                           geometry.Position.Y + geometry.Size.Height/2.0)))
                            |> Map.ofList
                        
                        // Update edges to connect to the new vertex positions
                        let arrangedEdges = 
                            edges 
                            |> List.map (fun edge ->
                                // If this edge connects two vertices that we've moved, update its geometry
                                match (edge.Source, edge.Target) with
                                | (Some sourceId, Some targetId) when 
                                    Map.containsKey sourceId positionMap && 
                                    Map.containsKey targetId positionMap ->
                                    
                                    let sourcePos = Map.find sourceId positionMap
                                    let targetPos = Map.find targetId positionMap
                                    
                                    // Create a simple point-to-point edge geometry or keep existing
                                    if edge.Geometry.IsSome then
                                        { edge with 
                                            Geometry = Some { edge.Geometry.Value with
                                                                Position = { X = 0.0; Y = 0.0 } } }
                                    else
                                        edge
                                | _ -> edge
                            )
                        
                        // Combine the arranged vertices and edges with any other cells
                        let otherCells = 
                            page.Cells 
                            |> List.filter (fun cell -> not (cell.IsVertex) && not (cell.IsEdge))
                        
                        { page with Cells = otherCells @ arrangedVertices @ arrangedEdges }
                    else
                        page) }

    /// Find elements by text content (case-insensitive partial match)
    let findElementsByText (diagram: Diagram) (pageIndex: int) (searchText: string) : (string * string) list =
        if pageIndex < 0 || pageIndex >= diagram.Pages.Length then
            raise <| IndexOutOfRangeException("Page index out of range")
        
        let page = diagram.Pages.[pageIndex]
        let searchTextLower = searchText.ToLowerInvariant()
        
        page.Cells
        |> List.filter (fun cell -> 
            cell.Id <> "0" && cell.Id <> "1" && 
            cell.Value.ToLowerInvariant().Contains(searchTextLower))
        |> List.map (fun cell -> (cell.Id, cell.Value))
    
    /// Get detailed information about a specific element
    let getElementInfo (diagram: Diagram) (pageIndex: int) (elementId: string) : ElementInfo option =
        if pageIndex < 0 || pageIndex >= diagram.Pages.Length then
            raise <| IndexOutOfRangeException("Page index out of range")
        
        let page = diagram.Pages.[pageIndex]
        
        match page.Cells |> List.tryFind (fun cell -> cell.Id = elementId) with
        | None -> None
        | Some element ->
            // Find connections where this element is source or target
            let connections = 
                page.Cells
                |> List.filter (fun cell -> 
                    cell.IsEdge && 
                    (cell.Source = Some elementId || cell.Target = Some elementId))
                |> List.choose (fun edge ->
                    match edge.Source, edge.Target with
                    | Some source, Some target ->
                        if source = elementId then 
                            Some (edge.Id, target) // Outgoing connection
                        else 
                            Some (edge.Id, source) // Incoming connection
                    | _ -> None)
            
            let elementType = 
                if element.IsVertex then "vertex"
                elif element.IsEdge then "edge"
                else "unknown"
            
            let position, size =
                match element.Geometry with
                | Some geo -> Some geo.Position, Some geo.Size
                | None -> None, None
            
            Some {
                Id = element.Id
                Type = elementType
                Value = element.Value
                Position = position
                Size = size
                Style = element.Style
                Parent = element.Parent
                Connections = connections
            }
    
    /// List all neighboring elements connected to the specified element
    let listNeighbors (diagram: Diagram) (pageIndex: int) (elementId: string) : (string * string * string) list =
        if pageIndex < 0 || pageIndex >= diagram.Pages.Length then
            raise <| IndexOutOfRangeException("Page index out of range")
        
        let page = diagram.Pages.[pageIndex]
        
        // Verify the element exists
        match page.Cells |> List.tryFind (fun cell -> cell.Id = elementId) with
        | None -> [] // Element not found
        | Some _ ->
            // Find edges connected to this element
            page.Cells
            |> List.filter (fun cell -> 
                cell.IsEdge && 
                (cell.Source = Some elementId || cell.Target = Some elementId))
            |> List.choose (fun edge ->
                match edge.Source, edge.Target with
                | Some source, Some target ->
                    // Get the neighbor element id (the other end of the connection)
                    let neighborId = if source = elementId then target else source
                    // Find the neighbor element to get its label
                    let neighborLabel = 
                        page.Cells 
                        |> List.tryFind (fun c -> c.Id = neighborId) 
                        |> Option.map (fun c -> c.Value)
                        |> Option.defaultValue ""
                    
                    let direction = 
                        if source = elementId then "outgoing" // Element -> Neighbor
                        else "incoming" // Neighbor -> Element
                    
                    Some (neighborId, neighborLabel, direction)
                | _ -> None)
    
    /// Calculate the bounding box of all elements in the diagram
    let getDiagramBounds (diagram: Diagram) (pageIndex: int) : BoundingBox option =
        if pageIndex < 0 || pageIndex >= diagram.Pages.Length then
            raise <| IndexOutOfRangeException("Page index out of range")
        
        let page = diagram.Pages.[pageIndex]
        
        // Get all elements with geometry
        let elementsWithGeometry = 
            page.Cells
            |> List.filter (fun cell -> 
                cell.Geometry.IsSome && cell.Id <> "0" && cell.Id <> "1")
            |> List.map (fun cell -> cell.Geometry.Value)
        
        if elementsWithGeometry.IsEmpty then
            None // No elements with geometry
        else
            // Calculate min/max coordinates
            let minX = elementsWithGeometry |> List.map (fun geo -> geo.Position.X) |> List.min
            let minY = elementsWithGeometry |> List.map (fun geo -> geo.Position.Y) |> List.min
            let maxX = elementsWithGeometry |> List.map (fun geo -> geo.Position.X + geo.Size.Width) |> List.max
            let maxY = elementsWithGeometry |> List.map (fun geo -> geo.Position.Y + geo.Size.Height) |> List.max
            
            Some {
                MinX = minX
                MinY = minY
                MaxX = maxX
                MaxY = maxY
                Width = maxX - minX
                Height = maxY - minY
            }

/// Functions for serializing DrawIO diagrams to XML
module XmlSerializer =
    open Types
    
    /// Create an mxGeometry element for a cell
    let createGeometryElement (geometry: Geometry option) =
        match geometry with
        | None -> null
        | Some geo ->
            let element = XElement(XName.Get("mxGeometry"))
            
            if geo.Position.X <> 0.0 then
                element.SetAttributeValue(XName.Get("x"), geo.Position.X)
                
            if geo.Position.Y <> 0.0 then
                element.SetAttributeValue(XName.Get("y"), geo.Position.Y)
                
            if geo.Size.Width <> 0.0 then
                element.SetAttributeValue(XName.Get("width"), geo.Size.Width)
                
            if geo.Size.Height <> 0.0 then
                element.SetAttributeValue(XName.Get("height"), geo.Size.Height)
                
            if geo.Relative then
                element.SetAttributeValue(XName.Get("relative"), "1")
                
            element.SetAttributeValue(XName.Get("as"), "geometry")
            element
    
    /// Create an mxCell element for a shape or connector
    let createCellElement (cell: Element) =
        let element = XElement(XName.Get("mxCell"))
        
        element.SetAttributeValue(XName.Get("id"), cell.Id)
        
        if not (String.IsNullOrEmpty(cell.Value)) then
            element.SetAttributeValue(XName.Get("value"), cell.Value)
            
        if not (String.IsNullOrEmpty(cell.Style)) then
            element.SetAttributeValue(XName.Get("style"), cell.Style)
            
        if cell.IsVertex then
            element.SetAttributeValue(XName.Get("vertex"), "1")
            
        if cell.IsEdge then
            element.SetAttributeValue(XName.Get("edge"), "1")
            
        if not (String.IsNullOrEmpty(cell.Parent)) then
            element.SetAttributeValue(XName.Get("parent"), cell.Parent)
            
        match cell.Source with
        | Some source -> element.SetAttributeValue(XName.Get("source"), source)
        | None -> ()
            
        match cell.Target with
        | Some target -> element.SetAttributeValue(XName.Get("target"), target)
        | None -> ()
            
        let geometryElement = createGeometryElement cell.Geometry
        if geometryElement <> null then
            element.Add(geometryElement)
            
        element
    
    /// Create the mxGraphModel element for a page
    let createGraphModelElement (page: Page) =
        let graphModel = XElement(XName.Get("mxGraphModel"))
        
        graphModel.SetAttributeValue(XName.Get("dx"), "800")
        graphModel.SetAttributeValue(XName.Get("dy"), "600")
        graphModel.SetAttributeValue(XName.Get("grid"), "1")
        graphModel.SetAttributeValue(XName.Get("gridSize"), "10")
        graphModel.SetAttributeValue(XName.Get("guides"), "1")
        graphModel.SetAttributeValue(XName.Get("tooltips"), "1")
        graphModel.SetAttributeValue(XName.Get("connect"), "1")
        graphModel.SetAttributeValue(XName.Get("arrows"), "1")
        graphModel.SetAttributeValue(XName.Get("fold"), "1")
        graphModel.SetAttributeValue(XName.Get("page"), "1")
        graphModel.SetAttributeValue(XName.Get("pageScale"), "1")
        graphModel.SetAttributeValue(XName.Get("pageWidth"), "850")
        graphModel.SetAttributeValue(XName.Get("pageHeight"), "1100")
        graphModel.SetAttributeValue(XName.Get("math"), "0")
        graphModel.SetAttributeValue(XName.Get("shadow"), "0")
        
        let root = XElement(XName.Get("root"))
        
        for cell in page.Cells do
            root.Add(createCellElement cell)
            
        graphModel.Add(root)
        graphModel
    
    /// Serialize a diagram to XML
    let serializeDiagram (diagram: Diagram) =
        let doc = XDocument()
        let mxfile = XElement(XName.Get("mxfile"))
        
        mxfile.SetAttributeValue(XName.Get("host"), "app.diagrams.net")
        mxfile.SetAttributeValue(XName.Get("modified"), diagram.Modified.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"))
        mxfile.SetAttributeValue(XName.Get("agent"), "DrawIO.MCP")
        mxfile.SetAttributeValue(XName.Get("version"), "15.0.5")
        mxfile.SetAttributeValue(XName.Get("type"), "device")
        
        for page in diagram.Pages do
            let diagramElem = XElement(XName.Get("diagram"))
            diagramElem.SetAttributeValue(XName.Get("id"), page.Id)
            diagramElem.SetAttributeValue(XName.Get("name"), page.Name)
            
            let graphModel = createGraphModelElement page
            diagramElem.Add(graphModel)
            
            mxfile.Add(diagramElem)
            
        doc.Add(mxfile)
        doc.ToString()

/// Functions for file operations
module FileOperations =
    open Types
    
    /// Load a diagram from a file
    let loadDiagram (filePath: string) =
        if not (File.Exists(filePath)) then
            raise <| FileNotFoundException($"Diagram file not found: {filePath}")
            
        let xmlContent = File.ReadAllText(filePath)
        XmlParser.parseDiagram xmlContent
    
    /// Save a diagram to a file
    let saveDiagram (diagram: Diagram) (filePath: string) =
        let xmlContent = XmlSerializer.serializeDiagram diagram
        File.WriteAllText(filePath, xmlContent)
        
    /// Create a new diagram and save it to a file
    let createNewDiagram (filePath: string) =
        let diagram = DiagramManipulation.createEmptyDiagram()
        saveDiagram diagram filePath
        diagram

    /// Create a new page in a diagram
    let createDiagramPage (diagram: Diagram) (name: string) =
        // Generate a unique ID for the new page
        let pageId = Guid.NewGuid().ToString()
        
        // Create a new empty page
        let newPage = {
            Id = pageId
            Name = name
            Cells = [
                // Add the default root and layer cells
                { 
                    Id = "0"
                    Value = ""
                    Style = ""
                    IsVertex = false
                    IsEdge = false
                    Parent = ""
                    Source = None
                    Target = None
                    Geometry = None
                };
                { 
                    Id = "1"
                    Value = ""
                    Style = ""
                    IsVertex = false
                    IsEdge = false
                    Parent = "0"
                    Source = None
                    Target = None
                    Geometry = None
                }
            ]
        }
        
        // Add the new page to the diagram
        { diagram with Pages = diagram.Pages @ [newPage] }
    
    /// Gets a diagram page by its index
    let getDiagramPage (diagram: Diagram) (page_index: int) =
        if page_index >= 0 && page_index < diagram.Pages.Length then
            Some diagram.Pages.[page_index]
        else
            None
    
    /// Update a diagram page's properties
    let updateDiagramPage (diagram: Diagram) (pageId: string) (name: string option) =
        { diagram with
            Pages = 
                diagram.Pages |> List.map (fun page ->
                    if page.Id = pageId then
                        { page with
                            Name = defaultArg name page.Name
                        }
                    else
                        page
                )
        }
    
    /// Delete a page from a diagram
    let deleteDiagramPage (diagram: Diagram) (pageId: string) =
        // Don't allow deleting the last page
        if diagram.Pages.Length <= 1 then
            diagram
        else
            { diagram with
                Pages = diagram.Pages |> List.filter (fun page -> page.Id <> pageId)
            }
    
    /// Move a cell from one page to another
    let moveCellBetweenPages (diagram: Diagram) (cellId: string) (sourcePageId: string) (targetPageId: string) =
        // Find the source and target pages
        let sourcePage = diagram.Pages |> List.tryFind (fun page -> page.Id = sourcePageId)
        let targetPage = diagram.Pages |> List.tryFind (fun page -> page.Id = targetPageId)
        
        match (sourcePage, targetPage) with
        | (Some srcPage, Some tgtPage) ->
            // Find the cell to move
            let cellToMove = srcPage.Cells |> List.tryFind (fun cell -> cell.Id = cellId)
            
            match cellToMove with
            | Some cell ->
                // Create a modified copy of the diagram
                { diagram with
                    Pages = 
                        diagram.Pages |> List.map (fun page ->
                            if page.Id = sourcePageId then
                                // Remove the cell from the source page
                                { page with Cells = page.Cells |> List.filter (fun c -> c.Id <> cellId) }
                            elif page.Id = targetPageId then
                                // Add the cell to the target page
                                { page with Cells = page.Cells @ [cell] }
                            else
                                page
                        )
                }
            | None -> diagram // Cell not found
        | _ -> diagram // Source or target page not found