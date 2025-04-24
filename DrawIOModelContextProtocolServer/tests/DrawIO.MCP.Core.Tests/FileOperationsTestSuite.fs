module DrawIO.MCP.Core.TestSuites.FileOperations

open System
open System.IO
open Xunit
open DrawIO.MCP.Core
open DrawIO.MCP.Core.Types
open DrawIO.MCP.Core.DiagramManipulation
open DrawIO.MCP.Core.FileOperations

[<Fact>]
let ``Create and Save Diagram should create valid file`` () =
    // Arrange
    let tempFilePath = Path.GetTempFileName()
    
    try
        // Act
        let diagram = createNewDiagram tempFilePath
        
        // Verify the file exists
        Assert.True(File.Exists(tempFilePath))
        
        // Modify the diagram and save it again
        let (diagramWithShape, _) = addShape diagram 0 "Test Shape" 100.0 100.0 120.0 60.0 "rectangle"
        saveDiagram diagramWithShape tempFilePath
        
        // Load the diagram from the file
        let loadedDiagram = loadDiagram tempFilePath
        
        // Assert
        Assert.Equal(diagramWithShape.Pages.Length, loadedDiagram.Pages.Length)
        Assert.Equal(diagramWithShape.Pages.[0].Cells.Length, loadedDiagram.Pages.[0].Cells.Length)
        
        // Find the shape we added
        let hasShape = loadedDiagram.Pages.[0].Cells |> List.exists (fun c -> c.Value = "Test Shape")
        Assert.True(hasShape)
    finally
        // Clean up
        if File.Exists(tempFilePath) then
            File.Delete(tempFilePath)

[<Fact>]
let ``Load Non-existent Diagram should throw exception`` () =
    Assert.Throws<FileNotFoundException>(fun () -> 
        loadDiagram "non-existent-diagram.drawio" |> ignore)

[<Fact>]
let ``Create and Delete Page operations should work correctly`` () =
    // Arrange
    let diagram = createEmptyDiagram()
    let originalPageCount = diagram.Pages.Length
    
    // Act - Add a new page
    let diagramWithNewPage = createDiagramPage diagram "New Test Page"
    
    // Assert
    Assert.Equal(originalPageCount + 1, diagramWithNewPage.Pages.Length)
    Assert.Equal("New Test Page", diagramWithNewPage.Pages.[1].Name)
    
    // Get a page by index
    let pageOption = getDiagramPage diagramWithNewPage 1
    Assert.True(pageOption.IsSome)
    Assert.Equal("New Test Page", pageOption.Value.Name)
    
    // Delete the page
    let pageToDeleteId = diagramWithNewPage.Pages.[1].Id
    let diagramAfterDelete = deleteDiagramPage diagramWithNewPage pageToDeleteId
    
    // Assert
    Assert.Equal(originalPageCount, diagramAfterDelete.Pages.Length)
    Assert.Equal(diagram.Pages.[0].Id, diagramAfterDelete.Pages.[0].Id)

[<Fact>]
let ``Update Page properties should modify page name`` () =
    // Arrange
    let diagram = createEmptyDiagram()
    let originalPage = diagram.Pages.[0]
    
    // Act
    let updatedDiagram = updateDiagramPage diagram originalPage.Id (Some "Updated Page Name")
    
    // Assert
    Assert.Equal("Updated Page Name", updatedDiagram.Pages.[0].Name)
    Assert.Equal(originalPage.Id, updatedDiagram.Pages.[0].Id)

[<Fact>]
let ``Move Cell Between Pages should transfer cell properly`` () =
    // Arrange
    let diagram = createEmptyDiagram()
    let diagramWithSecondPage = createDiagramPage diagram "Second Page"
    
    // Add a shape to the first page
    let (diagramWithShape, shapeId) = addShape diagramWithSecondPage 0 "Moving Shape" 100.0 100.0 120.0 60.0 "rectangle"
    
    // Verify the shape exists in the first page
    Assert.Contains(diagramWithShape.Pages.[0].Cells, fun c -> c.Id = shapeId)
    
    // Get source and target page IDs
    let sourcePageId = diagramWithShape.Pages.[0].Id
    let targetPageId = diagramWithShape.Pages.[1].Id
    
    // Act
    let updatedDiagram = moveCellBetweenPages diagramWithShape shapeId sourcePageId targetPageId
    
    // Assert
    // Shape should no longer be in the source page
    Assert.DoesNotContain(updatedDiagram.Pages.[0].Cells, fun c -> c.Id = shapeId)
    
    // Shape should now be in the target page
    Assert.Contains(updatedDiagram.Pages.[1].Cells, fun c -> c.Id = shapeId)
    
    // Shape properties should be preserved
    let movedShape = updatedDiagram.Pages.[1].Cells |> List.find (fun c -> c.Id = shapeId)
    Assert.Equal("Moving Shape", movedShape.Value)
    Assert.Equal(100.0, movedShape.Geometry.Value.Position.X)
    Assert.Equal(100.0, movedShape.Geometry.Value.Position.Y)

[<Fact>]
let ``Cannot delete last page of diagram`` () =
    // Arrange
    let diagram = createEmptyDiagram()
    let pageId = diagram.Pages.[0].Id
    
    // Act
    let updatedDiagram = deleteDiagramPage diagram pageId
    
    // Assert - Should still have the original page
    Assert.Equal(1, updatedDiagram.Pages.Length)
    Assert.Equal(pageId, updatedDiagram.Pages.[0].Id) 