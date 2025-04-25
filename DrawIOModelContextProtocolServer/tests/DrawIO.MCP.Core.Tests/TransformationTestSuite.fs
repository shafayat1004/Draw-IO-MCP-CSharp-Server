module DrawIO.MCP.Core.TestSuites.Transformation

open System
open Xunit
open DrawIO.MCP.Core
open DrawIO.MCP.Core.Types
open DrawIO.MCP.Core.DiagramManipulation

[<Fact>]
let ``RotateShape should add rotation style to shape`` () =
    // Arrange
    let diagram = createEmptyDiagram()
    let (diagramWithShape, shapeId) = addShape diagram 0 "Test Shape" 100.0 100.0 120.0 60.0 "rectangle"
    
    // Act
    let updatedDiagram = rotateShape diagramWithShape shapeId 45.0
    
    // Assert
    let rotatedShape = updatedDiagram.Pages.[0].Cells |> List.find (fun c -> c.Id = shapeId)
    Assert.Contains("rotation=45", rotatedShape.Style)

[<Fact>]
let ``RotateShape should update existing rotation style`` () =
    // Arrange
    let diagram = createEmptyDiagram()
    let (diagramWithShape, shapeId) = addShape diagram 0 "Test Shape" 100.0 100.0 120.0 60.0 "rectangle"
    let diagramWithRotation = rotateShape diagramWithShape shapeId 45.0
    
    // Act - Change rotation from 45 to 90 degrees
    let updatedDiagram = rotateShape diagramWithRotation shapeId 90.0
    
    // Assert
    let rotatedShape = updatedDiagram.Pages.[0].Cells |> List.find (fun c -> c.Id = shapeId)
    Assert.Contains("rotation=90", rotatedShape.Style)
    Assert.DoesNotContain("rotation=45", rotatedShape.Style)

[<Fact>]
let ``FlipShape horizontally should add flipH style`` () =
    // Arrange
    let diagram = createEmptyDiagram()
    let (diagramWithShape, shapeId) = addShape diagram 0 "Test Shape" 100.0 100.0 120.0 60.0 "rectangle"
    
    // Act
    let updatedDiagram = flipShape diagramWithShape shapeId FlipDirection.Horizontal
    
    // Assert
    let flippedShape = updatedDiagram.Pages.[0].Cells |> List.find (fun c -> c.Id = shapeId)
    Assert.Contains("flipH=1", flippedShape.Style)

[<Fact>]
let ``FlipShape vertically should add flipV style`` () =
    // Arrange
    let diagram = createEmptyDiagram()
    let (diagramWithShape, shapeId) = addShape diagram 0 "Test Shape" 100.0 100.0 120.0 60.0 "rectangle"
    
    // Act
    let updatedDiagram = flipShape diagramWithShape shapeId FlipDirection.Vertical
    
    // Assert
    let flippedShape = updatedDiagram.Pages.[0].Cells |> List.find (fun c -> c.Id = shapeId)
    Assert.Contains("flipV=1", flippedShape.Style)

[<Fact>]
let ``FlipShape should toggle flip state when used twice`` () =
    // Arrange
    let diagram = createEmptyDiagram()
    let (diagramWithShape, shapeId) = addShape diagram 0 "Test Shape" 100.0 100.0 120.0 60.0 "rectangle"
    
    // Act - Flip horizontally twice
    let flippedDiagram = flipShape diagramWithShape shapeId FlipDirection.Horizontal
    let unflippedDiagram = flipShape flippedDiagram shapeId FlipDirection.Horizontal
    
    // Assert
    let flippedShape = flippedDiagram.Pages.[0].Cells |> List.find (fun c -> c.Id = shapeId)
    let unflippedShape = unflippedDiagram.Pages.[0].Cells |> List.find (fun c -> c.Id = shapeId)
    
    Assert.Contains("flipH=1", flippedShape.Style)
    Assert.Contains("flipH=0", unflippedShape.Style) // Should be set to 0 when flipped back

[<Fact>]
let ``ConnectShapesAtPoints should create connection with waypoints`` () =
    // Arrange
    let diagram = createEmptyDiagram()
    let (diagramWithShape1, shape1Id) = addShape diagram 0 "Shape 1" 100.0 100.0 120.0 60.0 "rectangle"
    let (diagramWithShapes, shape2Id) = addShape diagramWithShape1 0 "Shape 2" 300.0 100.0 120.0 60.0 "rectangle"
    
    // Act - Connect with specific source and target points
    let (connectedDiagram, connectorId) = 
        connectShapesAtPoints 
            diagramWithShapes 
            0 
            shape1Id 
            shape2Id 
            (Some 150.0) 
            (Some 130.0) 
            (Some 320.0) 
            (Some 130.0)
    
    // Assert
    let connector = connectedDiagram.Pages.[0].Cells |> List.find (fun c -> c.Id = connectorId)
    
    // Check source and target
    Assert.Equal(shape1Id, connector.Source.Value)
    Assert.Equal(shape2Id, connector.Target.Value)
    
    // Check waypoints
    Assert.NotNull(connector.Geometry)
    Assert.NotEmpty(connector.Geometry.Value.Waypoints)
    
    // Check first waypoint (source point)
    let sourcePoint = connector.Geometry.Value.Waypoints.[0]
    Assert.Equal(150.0, sourcePoint.X)
    Assert.Equal(130.0, sourcePoint.Y)
    
    // Check second waypoint (target point)
    let targetPoint = connector.Geometry.Value.Waypoints.[1]
    Assert.Equal(320.0, targetPoint.X)
    Assert.Equal(130.0, targetPoint.Y) 