module DrawIO.MCP.Core.XmlSerializer

open System
open System.Xml.Linq
open Types

/// Functions for serializing DrawIO diagrams to XML

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
        
        // Add waypoints if any exist
        if not (List.isEmpty geo.Waypoints) then
            // Create an Array element to hold the waypoints
            let arrayElement = XElement(XName.Get("Array"))
            arrayElement.SetAttributeValue(XName.Get("as"), "points")
            
            // Add each waypoint to the array
            for waypoint in geo.Waypoints do
                let pointElement = XElement(XName.Get("mxPoint"))
                pointElement.SetAttributeValue(XName.Get("x"), waypoint.X)
                pointElement.SetAttributeValue(XName.Get("y"), waypoint.Y)
                if waypoint.IsRelative then
                    pointElement.SetAttributeValue(XName.Get("relative"), "1")
                arrayElement.Add(pointElement)
            
            // Add the array to the geometry element
            element.Add(arrayElement)
            
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
    
    // Check if there are any background properties stored in the cell data
    // This is a temporary solution to preserve background settings
    match page.Cells |> List.tryHead with
    | Some cell when cell.Id = "0" && not (String.IsNullOrEmpty(cell.Value)) ->
        // Try to parse background properties from root cell
        if cell.Value.Contains("background=") then
            let start = cell.Value.IndexOf("background=\"") + "background=\"".Length
            let endIndex = cell.Value.IndexOf("\"", start)
            if start > 0 && endIndex > start then
                let backgroundColor = cell.Value.Substring(start, endIndex - start)
                graphModel.SetAttributeValue(XName.Get("background"), backgroundColor)
                
        if cell.Value.Contains("backgroundImage=") then
            let start = cell.Value.IndexOf("backgroundImage=\"") + "backgroundImage=\"".Length
            let endIndex = cell.Value.IndexOf("\"", start)
            if start > 0 && endIndex > start then
                let backgroundImage = cell.Value.Substring(start, endIndex - start)
                graphModel.SetAttributeValue(XName.Get("backgroundImage"), backgroundImage)
    | _ -> ()
    
    let root = XElement(XName.Get("root"))
    
    for cell in page.Cells do
        root.Add(createCellElement(cell))
        
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
        
        let graphModel = createGraphModelElement(page)
        diagramElem.Add(graphModel)
        
        mxfile.Add(diagramElem)
        
    doc.Add(mxfile)
    doc.ToString()