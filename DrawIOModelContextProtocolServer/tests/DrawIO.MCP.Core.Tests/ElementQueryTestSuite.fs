module DrawIO.MCP.Core.TestSuites.ElementQuery

open System
open Xunit
open DrawIO.MCP.Core
open DrawIO.MCP.Core.Types
open DrawIO.MCP.Core.DiagramManipulation

[<Fact>]
let ``FindElementsByText should find matching elements`` () =
    // Arrange
    let diagram = createEmptyDiagram()
    let (diagramWithShape1, _) = addShape diagram 0 "Hello World" 100.0 100.0 120.0 60.0 "rectangle"
    let (diagramWithShape2, _) = addShape diagramWithShape1 0 "Another Shape" 250.0 100.0 120.0 60.0 "rectangle"
    let (diagramWithShape3, _) = addShape diagramWithShape2 0 "More Hello" 400.0 100.0 120.0 60.0 "rectangle"
    
    // Act
    let results = findElementsByText diagramWithShape3 0 "hello"
    
    // Assert
    Assert.Equal(2, results.Length)
    
    // Verify both shapes with "hello" (case insensitive) are found
    let values = results |> List.map snd
    Assert.Contains("Hello World", values)
    Assert.Contains("More Hello", values)
    Assert.DoesNotContain("Another Shape", values)

[<Fact>]
let ``FindElementsByText with no matches should return empty list`` () =
    // Arrange
    let diagram = createEmptyDiagram()
    let (diagramWithShape, _) = addShape diagram 0 "No Match" 100.0 100.0 120.0 60.0 "rectangle"
    
    // Act
    let results = findElementsByText diagramWithShape 0 "xyz123"
    
    // Assert
    Assert.Empty(results)

[<Fact>]
let ``GetElementInfo should return detailed information`` () =
    // Arrange
    let diagram = createEmptyDiagram()
    let (diagramWithShape1, shape1Id) = addShape diagram 0 "Source" 100.0 100.0 120.0 60.0 "rectangle"
    let (diagramWithShape2, shape2Id) = addShape diagramWithShape1 0 "Target" 300.0 100.0 120.0 60.0 "rectangle"
    let (finalDiagram, edgeId) = connectShapes diagramWithShape2 0 shape1Id shape2Id
    
    // Act
    let info = getElementInfo finalDiagram 0 shape1Id
    
    // Assert
    Assert.True(info.IsSome)
    let elementInfo = info.Value
    
    Assert.Equal(shape1Id, elementInfo.Id)
    Assert.Equal("Source", elementInfo.Value)
    Assert.Equal("vertex", elementInfo.Type)
    Assert.True(elementInfo.Position.IsSome)
    Assert.True(elementInfo.Size.IsSome)
    Assert.Equal(100.0, elementInfo.Position.Value.X)
    Assert.Equal(100.0, elementInfo.Position.Value.Y)
    Assert.Equal(120.0, elementInfo.Size.Value.Width)
    Assert.Equal(60.0, elementInfo.Size.Value.Height)
    
    // Check connections
    Assert.Equal(1, elementInfo.Connections.Length)
    let (connectionId, targetId) = elementInfo.Connections.[0]
    Assert.Equal(edgeId, connectionId)
    Assert.Equal(shape2Id, targetId)

[<Fact>]
let ``GetElementInfo for edge should include source and target`` () =
    // Arrange
    let diagram = createEmptyDiagram()
    let (diagramWithShape1, shape1Id) = addShape diagram 0 "Source" 100.0 100.0 120.0 60.0 "rectangle"
    let (diagramWithShape2, shape2Id) = addShape diagramWithShape1 0 "Target" 300.0 100.0 120.0 60.0 "rectangle"
    let (finalDiagram, edgeId) = connectShapes diagramWithShape2 0 shape1Id shape2Id
    
    // Act
    let info = getElementInfo finalDiagram 0 edgeId
    
    // Assert
    Assert.True(info.IsSome)
    let elementInfo = info.Value
    
    Assert.Equal(edgeId, elementInfo.Id)
    Assert.Equal("edge", elementInfo.Type)
    Assert.True(elementInfo.IsEdge)
    Assert.Equal(Some shape1Id, elementInfo.Source)
    Assert.Equal(Some shape2Id, elementInfo.Target)

[<Fact>]
let ``GetElementInfo for non-existent element should return None`` () =
    // Arrange
    let diagram = createEmptyDiagram()
    
    // Act
    let info = getElementInfo diagram 0 "non-existent-id"
    
    // Assert
    Assert.False(info.IsSome)

[<Fact>]
let ``ListNeighbors should find connected elements`` () =
    // Arrange
    let diagram = createEmptyDiagram()
    let (diagramWithShape1, shape1Id) = addShape diagram 0 "Center" 200.0 200.0 120.0 60.0 "rectangle"
    let (diagramWithShape2, shape2Id) = addShape diagramWithShape1 0 "Top" 200.0 100.0 120.0 60.0 "rectangle"
    let (diagramWithShape3, shape3Id) = addShape diagramWithShape2 0 "Right" 350.0 200.0 120.0 60.0 "rectangle"
    let (diagramWithShape4, shape4Id) = addShape diagramWithShape3 0 "Bottom" 200.0 300.0 120.0 60.0 "rectangle"
    
    // Connect center to others
    let (diagram1, _) = connectShapes diagramWithShape4 0 shape1Id shape2Id // Center -> Top
    let (diagram2, _) = connectShapes diagram1 0 shape3Id shape1Id // Right -> Center
    let (finalDiagram, _) = connectShapes diagram2 0 shape1Id shape4Id // Center -> Bottom
    
    // Act
    let neighbors = listNeighbors finalDiagram 0 shape1Id
    
    // Assert
    Assert.Equal(3, neighbors.Length)
    
    // Check that we have all expected neighbors
    let neighborIds = neighbors |> List.map (fun (id, _, _) -> id)
    Assert.Contains(shape2Id, neighborIds)
    Assert.Contains(shape3Id, neighborIds)
    Assert.Contains(shape4Id, neighborIds)
    
    // Check directions
    let outgoingNeighbors = neighbors |> List.filter (fun (_, _, dir) -> dir = "outgoing") |> List.map (fun (id, _, _) -> id)
    let incomingNeighbors = neighbors |> List.filter (fun (_, _, dir) -> dir = "incoming") |> List.map (fun (id, _, _) -> id)
    
    Assert.Contains(shape2Id, outgoingNeighbors) // Center -> Top
    Assert.Contains(shape4Id, outgoingNeighbors) // Center -> Bottom
    Assert.Contains(shape3Id, incomingNeighbors) // Right -> Center

[<Fact>]
let ``GetDiagramBounds should calculate correct bounds`` () =
    // Arrange
    let diagram = createEmptyDiagram()
    let (diagramWithShape1, _) = addShape diagram 0 "Top Left" 100.0 100.0 50.0 50.0 "rectangle"
    let (diagramWithShape2, _) = addShape diagramWithShape1 0 "Bottom Right" 400.0 300.0 100.0 75.0 "rectangle"
    
    // Act
    let bounds = getDiagramBounds diagramWithShape2 0
    
    // Assert
    Assert.True(bounds.IsSome)
    let boundingBox = bounds.Value
    
    Assert.Equal(100.0, boundingBox.MinX)
    Assert.Equal(100.0, boundingBox.MinY)
    Assert.Equal(500.0, boundingBox.MaxX) // 400 + 100
    Assert.Equal(375.0, boundingBox.MaxY) // 300 + 75
    Assert.Equal(400.0, boundingBox.Width) // 500 - 100
    Assert.Equal(275.0, boundingBox.Height) // 375 - 100

[<Fact>]
let ``GetDiagramBounds for empty diagram should return None`` () =
    // Arrange
    let diagram = createEmptyDiagram()
    
    // Act
    let bounds = getDiagramBounds diagram 0
    
    // Assert
    Assert.False(bounds.IsSome)

[<Fact>]
let ``GetDiagramBounds with negative coordinates should handle correctly`` () =
    // Arrange
    let diagram = createEmptyDiagram()
    let (diagramWithShape1, _) = addShape diagram 0 "Negative" -100.0 -50.0 50.0 25.0 "rectangle"
    let (diagramWithShape2, _) = addShape diagramWithShape1 0 "Positive" 100.0 200.0 75.0 100.0 "rectangle"
    
    // Act
    let bounds = getDiagramBounds diagramWithShape2 0
    
    // Assert
    Assert.True(bounds.IsSome)
    let boundingBox = bounds.Value
    
    Assert.Equal(-100.0, boundingBox.MinX)
    Assert.Equal(-50.0, boundingBox.MinY)
    Assert.Equal(175.0, boundingBox.MaxX) // 100 + 75
    Assert.Equal(300.0, boundingBox.MaxY) // 200 + 100
    Assert.Equal(275.0, boundingBox.Width) // 175 - (-100)
    Assert.Equal(350.0, boundingBox.Height) // 300 - (-50) 