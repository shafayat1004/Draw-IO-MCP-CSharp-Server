module DrawIO.MCP.Core.TestSuites.ShapeLibrary

open System
open System.IO
open Xunit
open DrawIO.MCP.Core
open DrawIO.MCP.Core.Types
open DrawIO.MCP.Core.ShapeLibrary
open DrawIO.MCP.Core.DiagramManipulation

[<Fact>]
let ``CreateEmptyLibrary should create library with correct name`` () =
    // Act
    let library = createEmptyLibrary "Test Library"
    
    // Assert
    Assert.Equal("Test Library", library.Name)
    Assert.Empty(library.Shapes)

[<Fact>]
let ``CreateCustomShape should create shape with correct properties`` () =
    // Act
    let shape = createCustomShape "Test Shape" "rounded=0;fillColor=#ff0000;" 120.0 60.0 None
    
    // Assert
    Assert.Equal("Test Shape", shape.Name)
    Assert.Equal("rounded=0;fillColor=#ff0000;", shape.Style)
    Assert.Equal(120.0, shape.Width)
    Assert.Equal(60.0, shape.Height)
    Assert.True(Option.isNone shape.XmlDefinition)
    Assert.NotNull(shape.Id)
    Assert.NotEmpty(shape.Id)

[<Fact>]
let ``AddShapeToLibrary should add shape to library`` () =
    // Arrange
    let library = createEmptyLibrary "Test Library"
    let shape1 = createCustomShape "Shape 1" "rounded=0;" 120.0 60.0 None
    let shape2 = createCustomShape "Shape 2" "ellipse;" 100.0 100.0 None
    
    // Act
    let libraryWithShape1 = addShapeToLibrary library shape1
    let libraryWithShape2 = addShapeToLibrary libraryWithShape1 shape2
    
    // Assert
    Assert.Single(libraryWithShape1.Shapes) |> ignore
    Assert.Equal(2, libraryWithShape2.Shapes.Length)
    
    // The latest shape should be at the beginning of the list (prepended)
    Assert.Equal(shape2.Id, libraryWithShape2.Shapes.[0].Id)
    Assert.Equal(shape1.Id, libraryWithShape2.Shapes.[1].Id)

[<Fact>]
let ``SaveLibraryToFile and LoadLibraryFromFile should preserve library data`` () =
    // Arrange
    let tempFilePath = Path.Combine(Path.GetTempPath(), $"test_library_{Guid.NewGuid().ToString()}.xml")
    let library = createEmptyLibrary "Test Library"
    let shape1 = createCustomShape "Shape 1" "rounded=0;fillColor=#ff0000;" 120.0 60.0 None
    let shape2 = createCustomShape "Shape 2" "ellipse;fillColor=#0000ff;" 100.0 100.0 (Some "<xml>Custom XML</xml>")
    let libraryWithShapes = library |> addShapeToLibrary <| shape1 |> addShapeToLibrary <| shape2
    
    try
        // Act
        saveLibraryToFile libraryWithShapes tempFilePath
        let loadedLibrary = loadLibraryFromFile tempFilePath
        
        // Assert
        Assert.Equal(Path.GetFileNameWithoutExtension(tempFilePath), loadedLibrary.Name) // Library name is from filename
        Assert.Equal(2, loadedLibrary.Shapes.Length)
        
        // Check shape properties were preserved (but IDs might be different)
        let loadedShape1 = loadedLibrary.Shapes |> List.find (fun s -> s.Name = "Shape 1")
        let loadedShape2 = loadedLibrary.Shapes |> List.find (fun s -> s.Name = "Shape 2")
        
        Assert.Equal("Shape 1", loadedShape1.Name)
        Assert.Contains("rounded=0", loadedShape1.Style)
        Assert.Contains("fillColor=#ff0000", loadedShape1.Style)
        Assert.Equal(120.0, loadedShape1.Width)
        Assert.Equal(60.0, loadedShape1.Height)
        
        Assert.Equal("Shape 2", loadedShape2.Name)
        Assert.Contains("ellipse", loadedShape2.Style)
        Assert.Contains("fillColor=#0000ff", loadedShape2.Style)
        Assert.Equal(100.0, loadedShape2.Width)
        Assert.Equal(100.0, loadedShape2.Height)
        Assert.Equal(Some "<xml>Custom XML</xml>", loadedShape2.XmlDefinition)
    finally
        // Cleanup
        if File.Exists(tempFilePath) then
            File.Delete(tempFilePath)

[<Fact>]
let ``AddShapeFromLibrary should add library shape to diagram`` () =
    // Arrange
    let library = createEmptyLibrary "Test Library"
    let shape = createCustomShape "Custom Shape" "rounded=0;fillColor=#ff0000;" 120.0 60.0 None
    let libraryWithShape = addShapeToLibrary library shape
    
    let diagram = createEmptyDiagram()
    
    // Act
    let (updatedDiagram, newShapeId) = addShapeFromLibrary diagram 0 libraryWithShape shape.Id 200.0 150.0
    
    // Assert
    Assert.NotNull(updatedDiagram)
    let addedShape = updatedDiagram.Pages.[0].Cells |> List.find (fun c -> c.Id = newShapeId)
    
    Assert.Equal("Custom Shape", addedShape.Value)
    Assert.Equal(200.0, addedShape.Geometry.Value.Position.X)
    Assert.Equal(150.0, addedShape.Geometry.Value.Position.Y)
    Assert.Equal(120.0, addedShape.Geometry.Value.Size.Width)
    Assert.Equal(60.0, addedShape.Geometry.Value.Size.Height)
    Assert.Contains("fillColor=#ff0000", addedShape.Style)

[<Fact>]
let ``SetDiagramBackground should set background color`` () =
    // This test requires validating XML changes directly since the background
    // color is stored in the XML attribute, not in the F# model
    
    // Arrange
    let diagram = createEmptyDiagram()
    let color = "#f5f5f5"
    
    // Act
    let updatedDiagram = DiagramManipulation.setDiagramBackground diagram None (Some color)
    
    // Convert to XML to verify the background attribute
    let xml = XmlSerializer.serializeDiagram updatedDiagram
    
    // Assert
    Assert.Contains($"background=\"{color}\"", xml)

[<Fact>]
let ``SetDiagramBackground should set background image`` () =
    // Arrange
    let diagram = createEmptyDiagram()
    let imagePath = "https://example.com/background.png"
    
    // Act
    let updatedDiagram = DiagramManipulation.setDiagramBackground diagram (Some imagePath) None
    
    // Convert to XML to verify the backgroundImage attribute
    let xml = XmlSerializer.serializeDiagram updatedDiagram
    
    // Assert
    Assert.Contains($"backgroundImage=\"{imagePath}\"", xml) 