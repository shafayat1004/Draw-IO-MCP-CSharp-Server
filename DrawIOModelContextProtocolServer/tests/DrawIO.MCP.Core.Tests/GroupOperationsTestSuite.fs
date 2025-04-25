module DrawIO.MCP.Core.TestSuites.GroupOperationsTests

open System
open Xunit
open DrawIO.MCP.Core
open DrawIO.MCP.Core.DiagramManipulation
open DrawIO.MCP.Core.Types

[<Fact>]
let ``Deleting a group should delete all of its child shapes`` () =
    // Arrange - Create a diagram with three shapes
    let diagram = createEmptyDiagram()
    let (diagramWithShape1, shape1Id) = addShape diagram 0 "Shape 1" 100.0 100.0 120.0 60.0 "rectangle"
    let (diagramWithShape2, shape2Id) = addShape diagramWithShape1 0 "Shape 2" 250.0 100.0 120.0 60.0 "rectangle"
    let (diagramWithShape3, shape3Id) = addShape diagramWithShape2 0 "Shape 3" 175.0 200.0 120.0 60.0 "rectangle"
    
    // Group the shapes
    let shapeIds = [shape1Id; shape2Id; shape3Id]
    let (diagramWithGroup, groupId) = groupShapes diagramWithShape3 0 shapeIds
    
    // Verify initial state
    let page = diagramWithGroup.Pages.[0]
    Assert.True(page.Cells |> List.exists (fun cell -> cell.Id = groupId))
    Assert.True(page.Cells |> List.exists (fun cell -> cell.Id = shape1Id))
    Assert.True(page.Cells |> List.exists (fun cell -> cell.Id = shape2Id))
    Assert.True(page.Cells |> List.exists (fun cell -> cell.Id = shape3Id))
    
    // Act - Delete the group
    let updatedDiagram = deleteShape diagramWithGroup 0 groupId
    
    // Assert - All shapes should be deleted
    let updatedPage = updatedDiagram.Pages.[0]
    Assert.False(updatedPage.Cells |> List.exists (fun cell -> cell.Id = groupId))
    Assert.False(updatedPage.Cells |> List.exists (fun cell -> cell.Id = shape1Id))
    Assert.False(updatedPage.Cells |> List.exists (fun cell -> cell.Id = shape2Id))
    Assert.False(updatedPage.Cells |> List.exists (fun cell -> cell.Id = shape3Id))

[<Fact>]
let ``Deleting a group with connected shapes should delete group and all connectors`` () =
    // Arrange - Create a diagram with shapes inside and outside the group
    let diagram = createEmptyDiagram()
    let (diagramWithShape1, shape1Id) = addShape diagram 0 "Shape 1" 100.0 100.0 120.0 60.0 "rectangle"
    let (diagramWithShape2, shape2Id) = addShape diagramWithShape1 0 "Shape 2" 250.0 100.0 120.0 60.0 "rectangle"
    let (diagramWithShape3, externalShapeId) = addShape diagramWithShape2 0 "External Shape" 400.0 100.0 120.0 60.0 "rectangle"
    
    // Connect a shape that will be in the group to the external shape
    let (diagramWithConnector, connectorId) = connectShapes diagramWithShape3 0 shape2Id externalShapeId
    
    // Group only the first two shapes
    let shapeIds = [shape1Id; shape2Id]
    let (diagramWithGroup, groupId) = groupShapes diagramWithConnector 0 shapeIds
    
    // Verify initial state
    let page = diagramWithGroup.Pages.[0]
    Assert.True(page.Cells |> List.exists (fun cell -> cell.Id = groupId))
    Assert.True(page.Cells |> List.exists (fun cell -> cell.Id = connectorId))
    Assert.True(page.Cells |> List.exists (fun cell -> cell.Id = externalShapeId))
    
    // Act - Delete the group
    let updatedDiagram = deleteShape diagramWithGroup 0 groupId
    
    // Assert - Group, its shapes, and connected connectors should be deleted, but external shape should remain
    let updatedPage = updatedDiagram.Pages.[0]
    Assert.False(updatedPage.Cells |> List.exists (fun cell -> cell.Id = groupId))
    Assert.False(updatedPage.Cells |> List.exists (fun cell -> cell.Id = shape1Id))
    Assert.False(updatedPage.Cells |> List.exists (fun cell -> cell.Id = shape2Id))
    Assert.False(updatedPage.Cells |> List.exists (fun cell -> cell.Id = connectorId))
    Assert.True(updatedPage.Cells |> List.exists (fun cell -> cell.Id = externalShapeId))

[<Fact>]
let ``Deleting a shape with group as parent should not delete the group or siblings`` () =
    // Arrange - Create a diagram with three shapes
    let diagram = createEmptyDiagram()
    let (diagramWithShape1, shape1Id) = addShape diagram 0 "Shape 1" 100.0 100.0 120.0 60.0 "rectangle"
    let (diagramWithShape2, shape2Id) = addShape diagramWithShape1 0 "Shape 2" 250.0 100.0 120.0 60.0 "rectangle"
    let (diagramWithShape3, shape3Id) = addShape diagramWithShape2 0 "Shape 3" 175.0 200.0 120.0 60.0 "rectangle"
    
    // Group the shapes
    let shapeIds = [shape1Id; shape2Id; shape3Id]
    let (diagramWithGroup, groupId) = groupShapes diagramWithShape3 0 shapeIds
    
    // Verify initial state
    let page = diagramWithGroup.Pages.[0]
    Assert.True(page.Cells |> List.exists (fun cell -> cell.Id = groupId))
    Assert.True(page.Cells |> List.exists (fun cell -> cell.Id = shape1Id))
    
    // Act - Delete one shape from the group
    let updatedDiagram = deleteShape diagramWithGroup 0 shape1Id
    
    // Assert - Only the deleted shape should be gone, not the group or other shapes
    let updatedPage = updatedDiagram.Pages.[0]
    Assert.True(updatedPage.Cells |> List.exists (fun cell -> cell.Id = groupId))
    Assert.False(updatedPage.Cells |> List.exists (fun cell -> cell.Id = shape1Id))
    Assert.True(updatedPage.Cells |> List.exists (fun cell -> cell.Id = shape2Id))
    Assert.True(updatedPage.Cells |> List.exists (fun cell -> cell.Id = shape3Id))

[<Fact>]
let ``Deleting a nested group should delete all children recursively`` () =
    // Arrange - Create a diagram with shapes and nested groups
    let diagram = createEmptyDiagram()
    let (diagramWithShape1, shape1Id) = addShape diagram 0 "Shape 1" 100.0 100.0 120.0 60.0 "rectangle"
    let (diagramWithShape2, shape2Id) = addShape diagramWithShape1 0 "Shape 2" 250.0 100.0 120.0 60.0 "rectangle"
    
    // Create inner group with first two shapes
    let innerShapeIds = [shape1Id; shape2Id]
    let (diagramWithInnerGroup, innerGroupId) = groupShapes diagramWithShape2 0 innerShapeIds
    
    // Add more shapes
    let (diagramWithShape3, shape3Id) = addShape diagramWithInnerGroup 0 "Shape 3" 175.0 200.0 120.0 60.0 "rectangle"
    let (diagramWithShape4, shape4Id) = addShape diagramWithShape3 0 "Shape 4" 300.0 200.0 120.0 60.0 "rectangle"
    
    // Create outer group containing the inner group and the additional shapes
    let outerShapeIds = [innerGroupId; shape3Id; shape4Id]
    let (diagramWithOuterGroup, outerGroupId) = groupShapes diagramWithShape4 0 outerShapeIds
    
    // Verify initial state
    let page = diagramWithOuterGroup.Pages.[0]
    Assert.True(page.Cells |> List.exists (fun cell -> cell.Id = outerGroupId))
    Assert.True(page.Cells |> List.exists (fun cell -> cell.Id = innerGroupId))
    Assert.True(page.Cells |> List.exists (fun cell -> cell.Id = shape1Id))
    
    // Act - Delete the outer group
    let updatedDiagram = deleteShape diagramWithOuterGroup 0 outerGroupId
    
    // Assert - All groups and shapes should be deleted
    let updatedPage = updatedDiagram.Pages.[0]
    Assert.False(updatedPage.Cells |> List.exists (fun cell -> cell.Id = outerGroupId))
    Assert.False(updatedPage.Cells |> List.exists (fun cell -> cell.Id = innerGroupId))
    Assert.False(updatedPage.Cells |> List.exists (fun cell -> cell.Id = shape1Id))
    Assert.False(updatedPage.Cells |> List.exists (fun cell -> cell.Id = shape2Id))
    Assert.False(updatedPage.Cells |> List.exists (fun cell -> cell.Id = shape3Id))
    Assert.False(updatedPage.Cells |> List.exists (fun cell -> cell.Id = shape4Id))

[<Fact>]
let ``Deleting a group that contains connectors between its shapes should delete everything`` () =
    // Arrange - Create a diagram with connected shapes
    let diagram = createEmptyDiagram()
    let (diagramWithShape1, shape1Id) = addShape diagram 0 "Shape 1" 100.0 100.0 120.0 60.0 "rectangle"
    let (diagramWithShape2, shape2Id) = addShape diagramWithShape1 0 "Shape 2" 300.0 100.0 120.0 60.0 "rectangle"
    
    // Connect the shapes
    let (diagramWithConnector, connectorId) = connectShapes diagramWithShape2 0 shape1Id shape2Id
    
    // Group everything including the connector
    let shapeIds = [shape1Id; shape2Id; connectorId]
    let (diagramWithGroup, groupId) = groupShapes diagramWithConnector 0 shapeIds
    
    // Verify initial state
    let page = diagramWithGroup.Pages.[0]
    Assert.True(page.Cells |> List.exists (fun cell -> cell.Id = groupId))
    Assert.True(page.Cells |> List.exists (fun cell -> cell.Id = connectorId))
    
    // Act - Delete the group
    let updatedDiagram = deleteShape diagramWithGroup 0 groupId
    
    // Assert - Everything should be deleted
    let updatedPage = updatedDiagram.Pages.[0]
    Assert.False(updatedPage.Cells |> List.exists (fun cell -> cell.Id = groupId))
    Assert.False(updatedPage.Cells |> List.exists (fun cell -> cell.Id = shape1Id))
    Assert.False(updatedPage.Cells |> List.exists (fun cell -> cell.Id = shape2Id))
    Assert.False(updatedPage.Cells |> List.exists (fun cell -> cell.Id = connectorId)) 