module DrawIO.MCP.Core.TestSuites.StyleManipulation

open System
open Xunit
open DrawIO.MCP.Core
open DrawIO.MCP.Core.Types
open DrawIO.MCP.Core.DiagramManipulation
open Microsoft.FSharp.Core

[<Fact>]
let ``UpdateShapeStyle should apply style properties`` () =
    // Arrange
    let diagram = createEmptyDiagram()
    let (diagramWithShape, shapeId) = addShape diagram 0 "Test Shape" 100.0 100.0 120.0 60.0 "rectangle"
    
    // Create a style map
    let styleProperties = Map.ofList [
        "fillColor", "#ff0000"
        "strokeColor", "#0000ff"
        "strokeWidth", "2"
        "dashed", "1"
    ]
    
    // Act
    let updatedDiagram = updateShapeStyle diagramWithShape shapeId styleProperties
    
    // Assert
    // Find the updated shape
    let updatedShape = updatedDiagram.Pages.[0].Cells |> List.find (fun c -> c.Id = shapeId)
    
    // Check each style property was applied
    Assert.Contains("fillColor=#ff0000", updatedShape.Style)
    Assert.Contains("strokeColor=#0000ff", updatedShape.Style)
    Assert.Contains("strokeWidth=2", updatedShape.Style)
    Assert.Contains("dashed=1", updatedShape.Style)

[<Fact>]
let ``UpdateShape should merge new styles with existing styles`` () =
    // Arrange
    let diagram = createEmptyDiagram()
    let (diagramWithShape, shapeId) = addShape diagram 0 "Test Shape" 100.0 100.0 120.0 60.0 "rectangle"
    
    // First update with some styles
    let diagram1 = updateShape diagramWithShape 0 shapeId "Test Shape" None None None None (Some "fillColor=#ff0000;fontStyle=1;")
    
    // Act - Update with different styles
    let updatedDiagram = updateShape diagram1 0 shapeId "Test Shape" None None None None (Some "strokeColor=#0000ff;strokeWidth=2;")
    
    // Assert
    // Find the updated shape
    let updatedShape = updatedDiagram.Pages.[0].Cells |> List.find (fun c -> c.Id = shapeId)
    
    // Both sets of styles should be present
    Assert.Contains("fillColor=#ff0000", updatedShape.Style)
    Assert.Contains("fontStyle=1", updatedShape.Style)
    Assert.Contains("strokeColor=#0000ff", updatedShape.Style)
    Assert.Contains("strokeWidth=2", updatedShape.Style)

[<Fact>]
let ``UpdateShape should update geometry and style`` () =
    // Arrange
    let diagram = createEmptyDiagram()
    let (diagramWithShape, shapeId) = addShape diagram 0 "Original" 100.0 100.0 120.0 60.0 "rectangle"
    
    // Act
    let updatedDiagram = updateShape diagramWithShape 0 shapeId "Updated" (Some 200.0) (Some 200.0) (Some 150.0) (Some 75.0) (Some "fillColor=#ff0000;")
    
    // Assert
    // Find the updated shape
    let updatedShape = updatedDiagram.Pages.[0].Cells |> List.find (fun c -> c.Id = shapeId)
    
    // Check all properties updated
    Assert.Equal("Updated", updatedShape.Value)
    Assert.Contains("fillColor=#ff0000", updatedShape.Style)
    
    let geometry = updatedShape.Geometry.Value
    Assert.Equal(200.0, geometry.Position.X)
    Assert.Equal(200.0, geometry.Position.Y)
    Assert.Equal(150.0, geometry.Size.Width)
    Assert.Equal(75.0, geometry.Size.Height)

[<Fact>]
let ``ArrangeLayout with horizontal layout should align shapes horizontally`` () =
    // Arrange
    let diagram = createEmptyDiagram()
    let (diagramWithShape1, _) = addShape diagram 0 "Shape 1" 100.0 100.0 120.0 60.0 "rectangle"
    let (diagramWithShape2, _) = addShape diagramWithShape1 0 "Shape 2" 300.0 200.0 120.0 60.0 "rectangle"
    let (diagramWithShape3, _) = addShape diagramWithShape2 0 "Shape 3" 500.0 300.0 120.0 60.0 "rectangle"
    
    // Act
    let arrangedDiagram = arrangeLayout diagramWithShape3 0 "horizontal"
    
    // Assert
    let vertices = arrangedDiagram.Pages.[0].Cells 
                  |> List.filter (fun c -> c.IsVertex && c.Value <> "" && c.Geometry.IsSome)
    
    // Check all shapes have the same Y value
    let yValues = vertices |> List.map (fun v -> v.Geometry.Value.Position.Y) |> Set.ofList
    Assert.Single(yValues) // Should only be one unique Y value
    
    // Check X values are evenly spaced
    let xValues = vertices |> List.map (fun v -> v.Geometry.Value.Position.X) |> List.sort
    let spacings = List.pairwise xValues |> List.map (fun (a, b) -> b - a)
    
    // All spacings should be equal (within a small tolerance for floating point)
    let firstSpacing = spacings.[0]
    for spacing in spacings do
        let _ = Assert.True(Math.Abs(spacing - firstSpacing) < 0.001)
        ()

[<Fact>]
let ``ArrangeLayout with vertical layout should align shapes vertically`` () =
    // Arrange
    let diagram = createEmptyDiagram()
    let (diagramWithShape1, _) = addShape diagram 0 "Shape 1" 100.0 100.0 120.0 60.0 "rectangle"
    let (diagramWithShape2, _) = addShape diagramWithShape1 0 "Shape 2" 300.0 200.0 120.0 60.0 "rectangle"
    let (diagramWithShape3, _) = addShape diagramWithShape2 0 "Shape 3" 500.0 300.0 120.0 60.0 "rectangle"
    
    // Act
    let arrangedDiagram = arrangeLayout diagramWithShape3 0 "vertical"
    
    // Assert
    let vertices = arrangedDiagram.Pages.[0].Cells 
                  |> List.filter (fun c -> c.IsVertex && c.Value <> "" && c.Geometry.IsSome)
    
    // Check all shapes have the same X value
    let xValues = vertices |> List.map (fun v -> v.Geometry.Value.Position.X) |> Set.ofList
    Assert.Single(xValues) // Should only be one unique X value
    
    // Check Y values are evenly spaced
    let yValues = vertices |> List.map (fun v -> v.Geometry.Value.Position.Y) |> List.sort
    let spacings = List.pairwise yValues |> List.map (fun (a, b) -> b - a)
    
    // All spacings should be equal (within a small tolerance for floating point)
    let firstSpacing = spacings.[0]
    for spacing in spacings do
        let _ = Assert.True(Math.Abs(spacing - firstSpacing) < 0.001)
        ()

[<Fact>]
let ``MoveShape should update shape position`` () =
    // Arrange
    let diagram = createEmptyDiagram()
    let (diagramWithShape, shapeId) = addShape diagram 0 "Test Shape" 100.0 100.0 120.0 60.0 "rectangle"
    
    // Act
    let updatedDiagram = moveShape diagramWithShape shapeId 300.0 200.0
    
    // Assert
    let movedShape = updatedDiagram.Pages.[0].Cells |> List.find (fun c -> c.Id = shapeId)
    Assert.Equal(300.0, movedShape.Geometry.Value.Position.X)
    Assert.Equal(200.0, movedShape.Geometry.Value.Position.Y)
    
    // Size should remain unchanged
    Assert.Equal(120.0, movedShape.Geometry.Value.Size.Width)
    Assert.Equal(60.0, movedShape.Geometry.Value.Size.Height)

[<Fact>]
let ``Different shape types should have correct styles`` () =
    // Arrange
    let diagram = createEmptyDiagram()
    
    // Create shapes with different types
    let (diagramWithRect, rectId) = addShape diagram 0 "Rectangle" 100.0 100.0 120.0 60.0 "rectangle"
    let (diagramWithEllipse, ellipseId) = addShape diagramWithRect 0 "Ellipse" 300.0 100.0 120.0 60.0 "ellipse"
    let (diagramWithDiamond, diamondId) = addShape diagramWithEllipse 0 "Diamond" 500.0 100.0 120.0 60.0 "diamond"
    
    // Assert
    let rectShape = diagramWithDiamond.Pages.[0].Cells |> List.find (fun c -> c.Id = rectId)
    let ellipseShape = diagramWithDiamond.Pages.[0].Cells |> List.find (fun c -> c.Id = ellipseId)
    let diamondShape = diagramWithDiamond.Pages.[0].Cells |> List.find (fun c -> c.Id = diamondId)
    
    // Each shape type should have an appropriate style
    Assert.Contains("rounded=0", rectShape.Style)
    Assert.Contains("ellipse", ellipseShape.Style)
    Assert.Contains("shape=diamond", diamondShape.Style) 