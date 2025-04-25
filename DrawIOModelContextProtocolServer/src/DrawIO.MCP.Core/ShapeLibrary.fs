namespace DrawIO.MCP.Core

open System
open System.IO
open System.Xml.Linq
open Types

/// Module for managing custom shape libraries
module ShapeLibrary =
    /// Create a new empty shape library
    let createEmptyLibrary (name: string) =
        {
            Name = name
            Shapes = []
        }
    
    /// Add a custom shape to a library
    let addShapeToLibrary (library: ShapeLibrary) (shape: CustomShape) =
        { library with Shapes = shape :: library.Shapes }
    
    /// Create a new custom shape
    let createCustomShape (name: string) (style: string) (width: float) (height: float) (xmlDefinition: string option) =
        {
            Id = Guid.NewGuid().ToString()
            Name = name
            Style = style
            Width = width
            Height = height
            XmlDefinition = xmlDefinition
        }
    
    /// Save a shape library to a file
    let saveLibraryToFile (library: ShapeLibrary) (filePath: string) =
        // Create a basic XML structure for the library
        let doc = new XDocument()
        let root = new XElement(XName.Get("mxlibrary"))
        
        // Add each shape to the library
        for shape in library.Shapes do
            let shapeElement = new XElement(XName.Get("shape"))
            
            shapeElement.SetAttributeValue(XName.Get("id"), shape.Id)
            shapeElement.SetAttributeValue(XName.Get("name"), shape.Name)
            shapeElement.SetAttributeValue(XName.Get("style"), shape.Style)
            shapeElement.SetAttributeValue(XName.Get("width"), shape.Width)
            shapeElement.SetAttributeValue(XName.Get("height"), shape.Height)
            
            // Add XML definition if provided
            match shape.XmlDefinition with
            | Some xml ->
                let xmlElement = new XElement(XName.Get("xml"))
                xmlElement.Value <- xml
                shapeElement.Add(xmlElement)
            | None -> ()
            
            root.Add(shapeElement)
        
        doc.Add(root)
        File.WriteAllText(filePath, doc.ToString())
    
    /// Load a shape library from a file
    let loadLibraryFromFile (filePath: string) =
        if not (File.Exists(filePath)) then
            raise (FileNotFoundException($"Shape library file not found: {filePath}"))
        
        // Parse the XML file
        let xml = File.ReadAllText(filePath)
        let doc = XDocument.Parse(xml)
        
        // Get the root element
        let root = doc.Element(XName.Get("mxlibrary"))
        if root = null then
            raise (InvalidDataException("Invalid shape library file format"))
        
        // Get the library name from filename
        let libraryName = Path.GetFileNameWithoutExtension(filePath)
        
        // Extract all shape elements
        let shapes =
            root.Elements(XName.Get("shape"))
            |> Seq.map (fun element ->
                let id = 
                    match element.Attribute(XName.Get("id")) with
                    | null -> Guid.NewGuid().ToString()
                    | attr -> attr.Value
                
                let name = 
                    match element.Attribute(XName.Get("name")) with
                    | null -> "Unnamed Shape"
                    | attr -> attr.Value
                
                let style = 
                    match element.Attribute(XName.Get("style")) with
                    | null -> ""
                    | attr -> attr.Value
                
                // Parse width and height
                let width =
                    match element.Attribute(XName.Get("width")) with
                    | null -> 80.0
                    | attr -> 
                        match Double.TryParse(attr.Value) with
                        | true, value -> value
                        | _ -> 80.0
                
                let height =
                    match element.Attribute(XName.Get("height")) with
                    | null -> 40.0
                    | attr -> 
                        match Double.TryParse(attr.Value) with
                        | true, value -> value
                        | _ -> 40.0
                
                // Get XML definition if present
                let xmlDefinition =
                    let xmlElement = element.Element(XName.Get("xml"))
                    if xmlElement = null then None else Some xmlElement.Value
                
                {
                    Id = id
                    Name = name
                    Style = style
                    Width = width
                    Height = height
                    XmlDefinition = xmlDefinition
                })
            |> Seq.toList
        
        // Create the library
        {
            Name = libraryName
            Shapes = shapes
        }
    
    /// Add a shape from a library to a diagram
    let addShapeFromLibrary (diagram: Diagram) (pageIndex: int) (library: ShapeLibrary) (shapeId: string) (x: float) (y: float) =
        // Find the shape in the library
        match library.Shapes |> List.tryFind (fun s -> s.Id = shapeId) with
        | Some shape ->
            // Create a new ID for the shape
            let newId = DiagramManipulation.generateId()
            
            if pageIndex < 0 || pageIndex >= diagram.Pages.Length then
                raise <| IndexOutOfRangeException("Page index out of range")
            
            let page = diagram.Pages.[pageIndex]
            
            // Create a new vertex with the shape's properties
            let newVertex = DiagramManipulation.createVertex 
                                newId 
                                shape.Name 
                                x 
                                y 
                                shape.Width 
                                shape.Height 
                                shape.Style 
                                "1" // Default layer
                                
            // Add the new vertex to the page
            let updatedPage = {
                page with
                    Cells = page.Cells @ [newVertex]
            }
            
            // Update the diagram with the new page
            let updatedPages = 
                diagram.Pages
                |> List.mapi (fun i p -> if i = pageIndex then updatedPage else p)
            
            // Return the updated diagram and the new shape ID
            let updatedDiagram = {
                diagram with
                    Modified = DateTime.Now
                    Pages = updatedPages
            }
            
            (updatedDiagram, newId)
        | None ->
            raise (ArgumentException($"Shape with ID {shapeId} not found in library {library.Name}")) 