module DrawIO.MCP.Core.FileOperations

open System
open System.IO
open Types

/// Functions for file operations

/// Load a diagram from a file
let loadDiagram (filePath: string) =
    if not (File.Exists(filePath)) then
        raise <| FileNotFoundException($"Diagram file not found: {filePath}")
        
    let xmlContent = File.ReadAllText(filePath)
    XmlParser.parseDiagram(xmlContent)

/// Save a diagram to a file
let saveDiagram (diagram: Diagram) (filePath: string) =
    let xmlContent = XmlSerializer.serializeDiagram(diagram)
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
        Some diagram.Pages[page_index]
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
    | Some srcPage, Some _tgtPage ->
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