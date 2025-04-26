module DrawIO.MCP.Core.XmlParser

open System
open System.IO
open System.Xml.Linq
open System.Text
open System.IO.Compression
open Types
/// Functions for parsing and manipulating DrawIO XML

    
/// Decode a base64-compressed drawio diagram string into an XML Element
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

/// Parse a waypoint from an mxPoint element
let parseWaypoint (pointElement: XElement) =
    if pointElement = null then None
    else
        try
            let x = 
                match pointElement.Attribute(XName.Get("x")) with 
                | null -> 0.0 
                | attr -> Double.Parse(attr.Value)
            
            let y = 
                match pointElement.Attribute(XName.Get("y")) with 
                | null -> 0.0 
                | attr -> Double.Parse(attr.Value)
            
            let isRelative = 
                match pointElement.Attribute(XName.Get("relative")) with 
                | null -> false 
                | attr -> attr.Value = "1" || attr.Value = "true"
            
            Some {
                X = x
                Y = y
                IsRelative = isRelative
            }
        with ex ->
            None

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
            
            // Parse waypoints from mxPoint elements
            let waypoints =
                // First check for Array element with as="points" attribute
                let arrayElement = 
                    geometryElement.Elements(XName.Get("Array"))
                    |> Seq.tryFind (fun elem -> 
                        match elem.Attribute(XName.Get("as")) with
                        | null -> false
                        | attr -> attr.Value = "points")
                
                match arrayElement with
                | Some array -> 
                    // Parse waypoints from inside the Array element
                    array.Elements(XName.Get("mxPoint"))
                    |> Seq.choose parseWaypoint
                    |> Seq.toList
                | None ->
                    // Fallback to direct mxPoint elements for backward compatibility
                    if Seq.isEmpty (geometryElement.Elements(XName.Get("mxPoint"))) then
                        []
                    else
                        geometryElement.Elements(XName.Get("mxPoint"))
                        |> Seq.choose parseWaypoint
                        |> Seq.toList
            
            Some {
                Position = { X = x; Y = y }
                Size = { Width = width; Height = height }
                Relative = relative
                Waypoints = waypoints
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
            raise <| Exception("Invalid drawio file format")
        
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
