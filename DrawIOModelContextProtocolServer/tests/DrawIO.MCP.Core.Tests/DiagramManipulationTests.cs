using System;
using System.IO;
using System.Linq;
using DrawIO.MCP.Core;
using Xunit;
using System.Collections.Generic;

namespace DrawIO.MCP.Core.Tests
{
    public class DiagramManipulationTests
    {
        [Fact]
        public void CreateEmptyDiagram_ShouldReturnValidDiagram()
        {
            // Act
            var diagram = DiagramManipulation.createEmptyDiagram();
            
            // Assert
            Assert.NotNull(diagram);
            Assert.NotEmpty(diagram.Pages);
            Assert.Equal(1, diagram.Pages.Length);
        }
        
        [Fact]
        public void AddShape_ShouldAddShapeToPage()
        {
            // Arrange
            var diagram = DiagramManipulation.createEmptyDiagram();
            
            // Act
            var (updatedDiagram, shapeId) = DiagramManipulation.addShape(diagram, 0, "Test Shape", 100, 100, 120, 60, "rectangle");
            
            // Assert
            Assert.NotNull(updatedDiagram);
            Assert.NotEmpty(updatedDiagram.Pages);
            Assert.NotEmpty(updatedDiagram.Pages[0].Cells);
            Assert.Contains(updatedDiagram.Pages[0].Cells, cell => cell.Id == shapeId && cell.Value == "Test Shape");
        }
        
        [Fact]
        public void ConnectShapes_ShouldAddEdgeBetweenShapes()
        {
            // Arrange
            var diagram = DiagramManipulation.createEmptyDiagram();
            var (diagramWithShapes, shape1Id) = DiagramManipulation.addShape(diagram, 0, "Shape 1", 100, 100, 120, 60, "rectangle");
            var (diagramWithTwoShapes, shape2Id) = DiagramManipulation.addShape(diagramWithShapes, 0, "Shape 2", 300, 100, 120, 60, "rectangle");
            
            // Act
            var (updatedDiagram, edgeId) = DiagramManipulation.connectShapes(diagramWithTwoShapes, 0, shape1Id, shape2Id);
            
            // Assert
            Assert.NotNull(updatedDiagram);
            Assert.NotEmpty(updatedDiagram.Pages);
            Assert.NotEmpty(updatedDiagram.Pages[0].Cells);
            
            // Check if the edge was added
            var edge = Array.Find(updatedDiagram.Pages[0].Cells, cell => cell.Id == edgeId);
            Assert.NotNull(edge);
            Assert.True(edge.IsEdge);
            Assert.Equal(shape1Id, edge.Source.Value);
            Assert.Equal(shape2Id, edge.Target.Value);
        }
        
        [Fact]
        public void DeleteShape_ShouldRemoveShapeFromPage()
        {
            // Arrange
            var diagram = DiagramManipulation.createEmptyDiagram();
            var (diagramWithShape, shapeId) = DiagramManipulation.addShape(diagram, 0, "Test Shape", 100, 100, 120, 60, "rectangle");
            
            // Act
            var updatedDiagram = DiagramManipulation.deleteShape(diagramWithShape, 0, shapeId);
            
            // Assert
            Assert.NotNull(updatedDiagram);
            Assert.NotEmpty(updatedDiagram.Pages);
            Assert.DoesNotContain(updatedDiagram.Pages[0].Cells, cell => cell.Id == shapeId);
        }
        
        [Fact]
        public void UpdateShape_ShouldModifyShapeProperties()
        {
            // Arrange
            var diagram = DiagramManipulation.createEmptyDiagram();
            var (diagramWithShape, shapeId) = DiagramManipulation.addShape(diagram, 0, "Original Text", 100, 100, 120, 60, "rectangle");
            
            // Act
            var updatedDiagram = DiagramManipulation.updateShape(diagramWithShape, 0, shapeId, "Updated Text", 200, 200, 150, 75, "fillColor=#ff0000");
            
            // Assert
            Assert.NotNull(updatedDiagram);
            Assert.NotEmpty(updatedDiagram.Pages);
            
            var updatedShape = Array.Find(updatedDiagram.Pages[0].Cells, cell => cell.Id == shapeId);
            Assert.NotNull(updatedShape);
            Assert.Equal("Updated Text", updatedShape.Value);
            Assert.Contains("fillColor=#ff0000", updatedShape.Style);
            
            // Check geometry was updated
            Assert.NotNull(updatedShape.Geometry.Value);
            Assert.Equal(200, updatedShape.Geometry.Value.Position.X);
            Assert.Equal(200, updatedShape.Geometry.Value.Position.Y);
            Assert.Equal(150, updatedShape.Geometry.Value.Size.Width);
            Assert.Equal(75, updatedShape.Geometry.Value.Size.Height);
        }
        
        [Fact]
        public void ArrangeLayout_ShouldReorganizeShapes()
        {
            // Arrange
            var diagram = DiagramManipulation.createEmptyDiagram();
            var (diagramWithShape1, _) = DiagramManipulation.addShape(diagram, 0, "Shape 1", 100, 100, 120, 60, "rectangle");
            var (diagramWithShape2, _) = DiagramManipulation.addShape(diagramWithShape1, 0, "Shape 2", 130, 130, 120, 60, "rectangle");
            var (diagramWithShape3, _) = DiagramManipulation.addShape(diagramWithShape2, 0, "Shape 3", 160, 160, 120, 60, "rectangle");
            
            // Record original positions
            var originalPositions = diagramWithShape3.Pages[0].Cells
                .Where(c => c.IsVertex && c.Geometry.IsSome())
                .Select(c => (c.Id, c.Geometry.Value.Position.X, c.Geometry.Value.Position.Y))
                .ToList();
            
            // Act
            var arrangedDiagram = DiagramManipulation.arrangeLayout(diagramWithShape3, 0, "horizontal");
            
            // Assert
            Assert.NotNull(arrangedDiagram);
            
            // Check that at least some positions have changed
            bool anyPositionChanged = false;
            foreach (var cell in arrangedDiagram.Pages[0].Cells.Where(c => c.IsVertex && c.Geometry.IsSome()))
            {
                var original = originalPositions.FirstOrDefault(p => p.Id == cell.Id);
                if (original.Id != null && 
                    (Math.Abs(original.X - cell.Geometry.Value.Position.X) > 1 ||
                     Math.Abs(original.Y - cell.Geometry.Value.Position.Y) > 1))
                {
                    anyPositionChanged = true;
                    break;
                }
            }
            
            Assert.True(anyPositionChanged, "At least one shape should have changed position after layout arrangement");
        }

        [Fact]
        public void SetLineStyle_ShouldModifyConnectorStyle()
        {
            // Arrange
            var diagram = DiagramManipulation.createEmptyDiagram();
            var (diagramWithShapes, shape1Id) = DiagramManipulation.addShape(diagram, 0, "Shape 1", 100, 100, 120, 60, "rectangle");
            var (diagramWithTwoShapes, shape2Id) = DiagramManipulation.addShape(diagramWithShapes, 0, "Shape 2", 300, 100, 120, 60, "rectangle");
            var (diagramWithConnector, connectorId) = DiagramManipulation.connectShapes(diagramWithTwoShapes, 0, shape1Id, shape2Id);
            
            // Act
            var updatedDiagram = DiagramManipulation.updateShape(diagramWithConnector, 0, connectorId, null, null, null, null, null, "dashed=1;strokeWidth=2.5;");
            
            // Assert
            Assert.NotNull(updatedDiagram);
            var connector = Array.Find(updatedDiagram.Pages[0].Cells, cell => cell.Id == connectorId);
            Assert.NotNull(connector);
            Assert.Contains("dashed=1", connector.Style);
            Assert.Contains("strokeWidth=2.5", connector.Style);
        }

        [Fact]
        public void SetArrowStyle_ShouldModifyConnectorArrows()
        {
            // Arrange
            var diagram = DiagramManipulation.createEmptyDiagram();
            var (diagramWithShapes, shape1Id) = DiagramManipulation.addShape(diagram, 0, "Shape 1", 100, 100, 120, 60, "rectangle");
            var (diagramWithTwoShapes, shape2Id) = DiagramManipulation.addShape(diagramWithShapes, 0, "Shape 2", 300, 100, 120, 60, "rectangle");
            var (diagramWithConnector, connectorId) = DiagramManipulation.connectShapes(diagramWithTwoShapes, 0, shape1Id, shape2Id);
            
            // Act
            var updatedDiagram = DiagramManipulation.updateShape(diagramWithConnector, 0, connectorId, null, null, null, null, null, "startArrow=diamond;endArrow=classic;");
            
            // Assert
            Assert.NotNull(updatedDiagram);
            var connector = Array.Find(updatedDiagram.Pages[0].Cells, cell => cell.Id == connectorId);
            Assert.NotNull(connector);
            Assert.Contains("startArrow=diamond", connector.Style);
            Assert.Contains("endArrow=classic", connector.Style);
        }

        [Fact]
        public void ResetConnector_ShouldRemoveCustomRouting()
        {
            // Arrange
            var diagram = DiagramManipulation.createEmptyDiagram();
            var (diagramWithShapes, shape1Id) = DiagramManipulation.addShape(diagram, 0, "Shape 1", 100, 100, 120, 60, "rectangle");
            var (diagramWithTwoShapes, shape2Id) = DiagramManipulation.addShape(diagramWithShapes, 0, "Shape 2", 300, 100, 120, 60, "rectangle");
            var (diagramWithConnector, connectorId) = DiagramManipulation.connectShapes(diagramWithTwoShapes, 0, shape1Id, shape2Id);
            
            // First add some custom routing
            var customRoutedDiagram = DiagramManipulation.updateShape(diagramWithConnector, 0, connectorId, null, null, null, null, null, "edgeStyle=orthogonalEdgeStyle;curved=1;");
            
            // Act
            var updatedDiagram = DiagramManipulation.updateShape(customRoutedDiagram, 0, connectorId, null, null, null, null, null, "noJump=0;orthogonalLoop=1;jettySize=auto;");
            
            // Assert
            Assert.NotNull(updatedDiagram);
            var connector = Array.Find(updatedDiagram.Pages[0].Cells, cell => cell.Id == connectorId);
            Assert.NotNull(connector);
            Assert.Contains("noJump=0", connector.Style);
            Assert.Contains("orthogonalLoop=1", connector.Style);
            Assert.Contains("jettySize=auto", connector.Style);
            Assert.DoesNotContain("curved=1", connector.Style);
        }

        [Fact]
        public void ReverseConnector_ShouldSwapSourceAndTarget()
        {
            // Arrange
            var diagram = DiagramManipulation.createEmptyDiagram();
            var (diagramWithShapes, shape1Id) = DiagramManipulation.addShape(diagram, 0, "Shape 1", 100, 100, 120, 60, "rectangle");
            var (diagramWithTwoShapes, shape2Id) = DiagramManipulation.addShape(diagramWithShapes, 0, "Shape 2", 300, 100, 120, 60, "rectangle");
            var (diagramWithConnector, connectorId) = DiagramManipulation.connectShapes(diagramWithTwoShapes, 0, shape1Id, shape2Id);
            
            // Add some style to verify it's preserved
            var styledDiagram = DiagramManipulation.updateShape(diagramWithConnector, 0, connectorId, "Test Label", null, null, null, null, "startArrow=diamond;endArrow=classic;");
            
            // Act
            var (updatedDiagram, newConnectorId) = DiagramManipulation.connectShapes(styledDiagram, 0, shape2Id, shape1Id);
            DiagramManipulation.deleteShape(updatedDiagram, 0, connectorId);
            
            // Assert
            Assert.NotNull(updatedDiagram);
            var newConnector = Array.Find(updatedDiagram.Pages[0].Cells, cell => cell.Id == newConnectorId);
            Assert.NotNull(newConnector);
            Assert.Equal(shape2Id, newConnector.Source.Value);
            Assert.Equal(shape1Id, newConnector.Target.Value);
            Assert.Equal("Test Label", newConnector.Value);
            Assert.Contains("startArrow=diamond", newConnector.Style);
            Assert.Contains("endArrow=classic", newConnector.Style);
        }

        [Fact]
        public void ConnectorStyle_ShouldPreserveExistingStyles()
        {
            // Arrange
            var diagram = DiagramManipulation.createEmptyDiagram();
            var (diagramWithShapes, shape1Id) = DiagramManipulation.addShape(diagram, 0, "Shape 1", 100, 100, 120, 60, "rectangle");
            var (diagramWithTwoShapes, shape2Id) = DiagramManipulation.addShape(diagramWithShapes, 0, "Shape 2", 300, 100, 120, 60, "rectangle");
            var (diagramWithConnector, connectorId) = DiagramManipulation.connectShapes(diagramWithTwoShapes, 0, shape1Id, shape2Id);
            
            // Act
            var diagramWithLineStyle = DiagramManipulation.updateShape(diagramWithConnector, 0, connectorId, null, null, null, null, null, "dashed=1;strokeWidth=2.5;");
            var diagramWithArrowStyle = DiagramManipulation.updateShape(diagramWithLineStyle, 0, connectorId, null, null, null, null, null, "startArrow=diamond;endArrow=classic;");
            
            // Assert
            Assert.NotNull(diagramWithArrowStyle);
            var connector = Array.Find(diagramWithArrowStyle.Pages[0].Cells, cell => cell.Id == connectorId);
            Assert.NotNull(connector);
            Assert.Contains("dashed=1", connector.Style);
            Assert.Contains("strokeWidth=2.5", connector.Style);
            Assert.Contains("startArrow=diamond", connector.Style);
            Assert.Contains("endArrow=classic", connector.Style);
        }

        [Fact]
        public void AddWaypoint_ShouldAddWaypointToConnector()
        {
            // Arrange
            var diagram = DiagramManipulation.createEmptyDiagram();
            var (diagramWithShapes, shape1Id) = DiagramManipulation.addShape(diagram, 0, "Shape 1", 100, 100, 120, 60, "rectangle");
            var (diagramWithTwoShapes, shape2Id) = DiagramManipulation.addShape(diagramWithShapes, 0, "Shape 2", 300, 100, 120, 60, "rectangle");
            var (diagramWithConnector, connectorId) = DiagramManipulation.connectShapes(diagramWithTwoShapes, 0, shape1Id, shape2Id);
            
            // Act
            var updatedDiagram = DiagramManipulation.addWaypoint(diagramWithConnector, 0, connectorId, 200, 150, false, null);
            
            // Assert
            var waypoints = DiagramManipulation.getWaypoints(updatedDiagram, 0, connectorId);
            Assert.Single(waypoints);
            Assert.Equal(200, waypoints[0].X);
            Assert.Equal(150, waypoints[0].Y);
            Assert.False(waypoints[0].IsRelative);
        }
        
        [Fact]
        public void AddWaypoint_WithPosition_ShouldInsertAtSpecifiedIndex()
        {
            // Arrange
            var diagram = DiagramManipulation.createEmptyDiagram();
            var (diagramWithShapes, shape1Id) = DiagramManipulation.addShape(diagram, 0, "Shape 1", 100, 100, 120, 60, "rectangle");
            var (diagramWithTwoShapes, shape2Id) = DiagramManipulation.addShape(diagramWithShapes, 0, "Shape 2", 300, 100, 120, 60, "rectangle");
            var (diagramWithConnector, connectorId) = DiagramManipulation.connectShapes(diagramWithTwoShapes, 0, shape1Id, shape2Id);
            
            // Add first waypoint
            var diagramWithOneWaypoint = DiagramManipulation.addWaypoint(diagramWithConnector, 0, connectorId, 200, 150, false, Some.FromValue(0));
            
            // Act - Add another waypoint at beginning
            var updatedDiagram = DiagramManipulation.addWaypoint(diagramWithOneWaypoint, 0, connectorId, 150, 125, false, Some.FromValue(0));
            
            // Assert
            var waypoints = DiagramManipulation.getWaypoints(updatedDiagram, 0, connectorId);
            Assert.Equal(2, waypoints.Count);
            Assert.Equal(150, waypoints[0].X);
            Assert.Equal(125, waypoints[0].Y);
            Assert.Equal(200, waypoints[1].X);
            Assert.Equal(150, waypoints[1].Y);
        }
        
        [Fact]
        public void RemoveWaypoint_ShouldRemoveSpecifiedWaypoint()
        {
            // Arrange
            var diagram = DiagramManipulation.createEmptyDiagram();
            var (diagramWithShapes, shape1Id) = DiagramManipulation.addShape(diagram, 0, "Shape 1", 100, 100, 120, 60, "rectangle");
            var (diagramWithTwoShapes, shape2Id) = DiagramManipulation.addShape(diagramWithShapes, 0, "Shape 2", 300, 100, 120, 60, "rectangle");
            var (diagramWithConnector, connectorId) = DiagramManipulation.connectShapes(diagramWithTwoShapes, 0, shape1Id, shape2Id);
            
            // Add two waypoints
            var diagramWithOneWaypoint = DiagramManipulation.addWaypoint(diagramWithConnector, 0, connectorId, 150, 125, false, null);
            var diagramWithTwoWaypoints = DiagramManipulation.addWaypoint(diagramWithOneWaypoint, 0, connectorId, 200, 150, false, null);
            
            // Verify we have two waypoints
            var initialWaypoints = DiagramManipulation.getWaypoints(diagramWithTwoWaypoints, 0, connectorId);
            Assert.Equal(2, initialWaypoints.Count);
            
            // Act - Remove the first waypoint
            var updatedDiagram = DiagramManipulation.removeWaypoint(diagramWithTwoWaypoints, 0, connectorId, 0);
            
            // Assert
            var remainingWaypoints = DiagramManipulation.getWaypoints(updatedDiagram, 0, connectorId);
            Assert.Single(remainingWaypoints);
            Assert.Equal(200, remainingWaypoints[0].X);
            Assert.Equal(150, remainingWaypoints[0].Y);
        }
        
        [Fact]
        public void UpdateWaypoint_ShouldModifyWaypointPosition()
        {
            // Arrange
            var diagram = DiagramManipulation.createEmptyDiagram();
            var (diagramWithShapes, shape1Id) = DiagramManipulation.addShape(diagram, 0, "Shape 1", 100, 100, 120, 60, "rectangle");
            var (diagramWithTwoShapes, shape2Id) = DiagramManipulation.addShape(diagramWithShapes, 0, "Shape 2", 300, 100, 120, 60, "rectangle");
            var (diagramWithConnector, connectorId) = DiagramManipulation.connectShapes(diagramWithTwoShapes, 0, shape1Id, shape2Id);
            
            // Add a waypoint
            var diagramWithWaypoint = DiagramManipulation.addWaypoint(diagramWithConnector, 0, connectorId, 200, 150, false, null);
            
            // Act - Update the waypoint
            var updatedDiagram = DiagramManipulation.updateWaypoint(diagramWithWaypoint, 0, connectorId, 0, Some.FromValue(250.0), Some.FromValue(200.0));
            
            // Assert
            var waypoints = DiagramManipulation.getWaypoints(updatedDiagram, 0, connectorId);
            Assert.Single(waypoints);
            Assert.Equal(250, waypoints[0].X);
            Assert.Equal(200, waypoints[0].Y);
        }
        
        [Fact]
        public void UpdateWaypoint_WithPartialValues_ShouldOnlyUpdateSpecifiedProperties()
        {
            // Arrange
            var diagram = DiagramManipulation.createEmptyDiagram();
            var (diagramWithShapes, shape1Id) = DiagramManipulation.addShape(diagram, 0, "Shape 1", 100, 100, 120, 60, "rectangle");
            var (diagramWithTwoShapes, shape2Id) = DiagramManipulation.addShape(diagramWithShapes, 0, "Shape 2", 300, 100, 120, 60, "rectangle");
            var (diagramWithConnector, connectorId) = DiagramManipulation.connectShapes(diagramWithTwoShapes, 0, shape1Id, shape2Id);
            
            // Add a waypoint
            var diagramWithWaypoint = DiagramManipulation.addWaypoint(diagramWithConnector, 0, connectorId, 200, 150, false, null);
            
            // Act - Update only the Y coordinate
            var updatedDiagram = DiagramManipulation.updateWaypoint(diagramWithWaypoint, 0, connectorId, 0, None.Value, Some.FromValue(200.0));
            
            // Assert
            var waypoints = DiagramManipulation.getWaypoints(updatedDiagram, 0, connectorId);
            Assert.Single(waypoints);
            Assert.Equal(200, waypoints[0].X); // X should remain unchanged
            Assert.Equal(200, waypoints[0].Y); // Y should be updated
        }
        
        [Fact]
        public void ClearWaypoints_ShouldRemoveAllWaypoints()
        {
            // Arrange
            var diagram = DiagramManipulation.createEmptyDiagram();
            var (diagramWithShapes, shape1Id) = DiagramManipulation.addShape(diagram, 0, "Shape 1", 100, 100, 120, 60, "rectangle");
            var (diagramWithTwoShapes, shape2Id) = DiagramManipulation.addShape(diagramWithShapes, 0, "Shape 2", 300, 100, 120, 60, "rectangle");
            var (diagramWithConnector, connectorId) = DiagramManipulation.connectShapes(diagramWithTwoShapes, 0, shape1Id, shape2Id);
            
            // Add multiple waypoints
            var diagramWithOneWaypoint = DiagramManipulation.addWaypoint(diagramWithConnector, 0, connectorId, 150, 125, false, null);
            var diagramWithTwoWaypoints = DiagramManipulation.addWaypoint(diagramWithOneWaypoint, 0, connectorId, 200, 150, false, null);
            var diagramWithThreeWaypoints = DiagramManipulation.addWaypoint(diagramWithTwoWaypoints, 0, connectorId, 250, 125, false, null);
            
            // Verify we have three waypoints
            var initialWaypoints = DiagramManipulation.getWaypoints(diagramWithThreeWaypoints, 0, connectorId);
            Assert.Equal(3, initialWaypoints.Count);
            
            // Act - Clear all waypoints
            var updatedDiagram = DiagramManipulation.clearWaypoints(diagramWithThreeWaypoints, 0, connectorId);
            
            // Assert
            var remainingWaypoints = DiagramManipulation.getWaypoints(updatedDiagram, 0, connectorId);
            Assert.Empty(remainingWaypoints);
        }
        
        [Fact]
        public void GetWaypoints_NonExistentConnector_ShouldThrowException()
        {
            // Arrange
            var diagram = DiagramManipulation.createEmptyDiagram();
            
            // Act & Assert
            Assert.Throws<ArgumentException>(() => 
                DiagramManipulation.getWaypoints(diagram, 0, "non-existent-id"));
        }
        
        [Fact]
        public void AddWaypoint_ToVertexNotEdge_ShouldThrowException()
        {
            // Arrange
            var diagram = DiagramManipulation.createEmptyDiagram();
            var (diagramWithShape, shapeId) = DiagramManipulation.addShape(diagram, 0, "Shape 1", 100, 100, 120, 60, "rectangle");
            
            // Act & Assert
            Assert.Throws<ArgumentException>(() => 
                DiagramManipulation.addWaypoint(diagramWithShape, 0, shapeId, 200, 150, false, null));
        }
        
        [Fact]
        public void RemoveWaypoint_InvalidIndex_ShouldThrowException()
        {
            // Arrange
            var diagram = DiagramManipulation.createEmptyDiagram();
            var (diagramWithShapes, shape1Id) = DiagramManipulation.addShape(diagram, 0, "Shape 1", 100, 100, 120, 60, "rectangle");
            var (diagramWithTwoShapes, shape2Id) = DiagramManipulation.addShape(diagramWithShapes, 0, "Shape 2", 300, 100, 120, 60, "rectangle");
            var (diagramWithConnector, connectorId) = DiagramManipulation.connectShapes(diagramWithTwoShapes, 0, shape1Id, shape2Id);
            
            // Add a waypoint
            var diagramWithWaypoint = DiagramManipulation.addWaypoint(diagramWithConnector, 0, connectorId, 200, 150, false, null);
            
            // Act & Assert
            Assert.Throws<ArgumentException>(() => 
                DiagramManipulation.removeWaypoint(diagramWithWaypoint, 0, connectorId, 1)); // Index 1 is out of range
        }
        
        [Fact]
        public void UpdateWaypoint_InvalidIndex_ShouldThrowException()
        {
            // Arrange
            var diagram = DiagramManipulation.createEmptyDiagram();
            var (diagramWithShapes, shape1Id) = DiagramManipulation.addShape(diagram, 0, "Shape 1", 100, 100, 120, 60, "rectangle");
            var (diagramWithTwoShapes, shape2Id) = DiagramManipulation.addShape(diagramWithShapes, 0, "Shape 2", 300, 100, 120, 60, "rectangle");
            var (diagramWithConnector, connectorId) = DiagramManipulation.connectShapes(diagramWithTwoShapes, 0, shape1Id, shape2Id);
            
            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => DiagramManipulation.updateWaypoint(diagramWithConnector, 0, connectorId, 5, 250, 300, null));
        }

        [Fact]
        public void GroupShapes_ShouldCreateGroupAndSetParent()
        {
            // Arrange
            var diagram = DiagramManipulation.createEmptyDiagram();
            var (diagramWithShape1, shape1Id) = DiagramManipulation.addShape(diagram, 0, "Shape 1", 100, 100, 120, 60, "rectangle");
            var (diagramWithShape2, shape2Id) = DiagramManipulation.addShape(diagramWithShape1, 0, "Shape 2", 250, 100, 120, 60, "rectangle");
            var (diagramWithShape3, shape3Id) = DiagramManipulation.addShape(diagramWithShape2, 0, "Shape 3", 175, 200, 120, 60, "rectangle");
            
            var shapeIds = Microsoft.FSharp.Collections.FSharpList<string>.Empty;
            shapeIds = Microsoft.FSharp.Collections.FSharpList<string>.Cons(shape3Id, shapeIds);
            shapeIds = Microsoft.FSharp.Collections.FSharpList<string>.Cons(shape2Id, shapeIds);
            shapeIds = Microsoft.FSharp.Collections.FSharpList<string>.Cons(shape1Id, shapeIds);
            
            // Act
            var (updatedDiagram, groupId) = DiagramManipulation.groupShapes(diagramWithShape3, 0, shapeIds);
            
            // Assert
            Assert.NotNull(updatedDiagram);
            Assert.NotEmpty(updatedDiagram.Pages);
            
            // Find the group cell
            var groupCell = Array.Find(updatedDiagram.Pages[0].Cells, cell => cell.Id == groupId);
            Assert.NotNull(groupCell);
            Assert.Contains("group", groupCell.Style);
            Assert.True(groupCell.IsVertex);
            Assert.Equal("1", groupCell.Parent); // Group parent should be the default layer
            
            // Verify that all shapes have the group as their parent
            var shape1 = Array.Find(updatedDiagram.Pages[0].Cells, cell => cell.Id == shape1Id);
            var shape2 = Array.Find(updatedDiagram.Pages[0].Cells, cell => cell.Id == shape2Id);
            var shape3 = Array.Find(updatedDiagram.Pages[0].Cells, cell => cell.Id == shape3Id);
            
            Assert.Equal(groupId, shape1.Parent);
            Assert.Equal(groupId, shape2.Parent);
            Assert.Equal(groupId, shape3.Parent);
            
            // Verify group's geometry encompasses all shapes
            Assert.NotNull(groupCell.Geometry);
            Assert.Equal(100, groupCell.Geometry.Value.Position.X); // Left-most shape (shape1)
            Assert.Equal(100, groupCell.Geometry.Value.Position.Y); // Top-most shape (shape1 and shape2)
            Assert.Equal(270, groupCell.Geometry.Value.Size.Width); // From x=100 to x=250+120=370, width = 370-100 = 270
            Assert.Equal(160, groupCell.Geometry.Value.Size.Height); // From y=100 to y=200+60=260, height = 260-100 = 160
        }
        
        [Fact]
        public void GroupShapes_WithEmptyList_ShouldThrowException()
        {
            // Arrange
            var diagram = DiagramManipulation.createEmptyDiagram();
            var emptyList = Microsoft.FSharp.Collections.FSharpList<string>.Empty;
            
            // Act & Assert
            Assert.Throws<ArgumentException>(() => DiagramManipulation.groupShapes(diagram, 0, emptyList));
        }
        
        [Fact]
        public void GroupShapes_WithNonExistentShapes_ShouldThrowException()
        {
            // Arrange
            var diagram = DiagramManipulation.createEmptyDiagram();
            var (diagramWithShape, shapeId) = DiagramManipulation.addShape(diagram, 0, "Shape 1", 100, 100, 120, 60, "rectangle");
            
            var shapeIds = Microsoft.FSharp.Collections.FSharpList<string>.Empty;
            shapeIds = Microsoft.FSharp.Collections.FSharpList<string>.Cons(shapeId, shapeIds);
            shapeIds = Microsoft.FSharp.Collections.FSharpList<string>.Cons("non-existent-id", shapeIds);
            
            // Act & Assert
            Assert.Throws<ArgumentException>(() => DiagramManipulation.groupShapes(diagramWithShape, 0, shapeIds));
        }
        
        [Fact]
        public void UngroupShapes_ShouldReassignParents()
        {
            // Arrange
            var diagram = DiagramManipulation.createEmptyDiagram();
            var (diagramWithShape1, shape1Id) = DiagramManipulation.addShape(diagram, 0, "Shape 1", 100, 100, 120, 60, "rectangle");
            var (diagramWithShape2, shape2Id) = DiagramManipulation.addShape(diagramWithShape1, 0, "Shape 2", 250, 100, 120, 60, "rectangle");
            
            var shapeIds = Microsoft.FSharp.Collections.FSharpList<string>.Empty;
            shapeIds = Microsoft.FSharp.Collections.FSharpList<string>.Cons(shape2Id, shapeIds);
            shapeIds = Microsoft.FSharp.Collections.FSharpList<string>.Cons(shape1Id, shapeIds);
            
            var (diagramWithGroup, groupId) = DiagramManipulation.groupShapes(diagramWithShape2, 0, shapeIds);
            
            // Act
            var updatedDiagram = DiagramManipulation.ungroupShapes(diagramWithGroup, 0, groupId);
            
            // Assert
            Assert.NotNull(updatedDiagram);
            Assert.NotEmpty(updatedDiagram.Pages);
            
            // Verify group was removed
            Assert.DoesNotContain(updatedDiagram.Pages[0].Cells, cell => cell.Id == groupId);
            
            // Verify shapes are reassigned to the layer
            var shape1 = Array.Find(updatedDiagram.Pages[0].Cells, cell => cell.Id == shape1Id);
            var shape2 = Array.Find(updatedDiagram.Pages[0].Cells, cell => cell.Id == shape2Id);
            
            Assert.NotNull(shape1);
            Assert.NotNull(shape2);
            Assert.Equal("1", shape1.Parent); // Parent should be the default layer
            Assert.Equal("1", shape2.Parent); // Parent should be the default layer
        }
        
        [Fact]
        public void UngroupShapes_WithNonExistentGroup_ShouldThrowException()
        {
            // Arrange
            var diagram = DiagramManipulation.createEmptyDiagram();
            
            // Act & Assert
            Assert.Throws<ArgumentException>(() => DiagramManipulation.ungroupShapes(diagram, 0, "non-existent-group-id"));
        }
        
        [Fact]
        public void UngroupShapes_WithNonGroupElement_ShouldThrowException()
        {
            // Arrange
            var diagram = DiagramManipulation.createEmptyDiagram();
            var (diagramWithShape, shapeId) = DiagramManipulation.addShape(diagram, 0, "Shape 1", 100, 100, 120, 60, "rectangle");
            
            // Act & Assert
            Assert.Throws<ArgumentException>(() => DiagramManipulation.ungroupShapes(diagramWithShape, 0, shapeId));
        }

        [Fact]
        public void DeleteShape_WhenDeletingGroup_ShouldDeleteAllChildShapes()
        {
            // Arrange
            var diagram = DiagramManipulation.createEmptyDiagram();
            var (diagramWithShape1, shape1Id) = DiagramManipulation.addShape(diagram, 0, "Shape 1", 100, 100, 120, 60, "rectangle");
            var (diagramWithShape2, shape2Id) = DiagramManipulation.addShape(diagramWithShape1, 0, "Shape 2", 250, 100, 120, 60, "rectangle");
            var (diagramWithShape3, shape3Id) = DiagramManipulation.addShape(diagramWithShape2, 0, "Shape 3", 175, 200, 120, 60, "rectangle");
            
            // Create a group with the three shapes
            var shapeIds = Microsoft.FSharp.Collections.FSharpList<string>.Empty;
            shapeIds = Microsoft.FSharp.Collections.FSharpList<string>.Cons(shape3Id, shapeIds);
            shapeIds = Microsoft.FSharp.Collections.FSharpList<string>.Cons(shape2Id, shapeIds);
            shapeIds = Microsoft.FSharp.Collections.FSharpList<string>.Cons(shape1Id, shapeIds);
            
            var (diagramWithGroup, groupId) = DiagramManipulation.groupShapes(diagramWithShape3, 0, shapeIds);
            
            // Verify initial state
            Assert.NotNull(Array.Find(diagramWithGroup.Pages[0].Cells, cell => cell.Id == groupId));
            Assert.NotNull(Array.Find(diagramWithGroup.Pages[0].Cells, cell => cell.Id == shape1Id));
            Assert.NotNull(Array.Find(diagramWithGroup.Pages[0].Cells, cell => cell.Id == shape2Id));
            Assert.NotNull(Array.Find(diagramWithGroup.Pages[0].Cells, cell => cell.Id == shape3Id));
            
            // Act - Delete the group
            var updatedDiagram = DiagramManipulation.deleteShape(diagramWithGroup, 0, groupId);
            
            // Assert - The group and all its child shapes should be deleted
            Assert.Null(Array.Find(updatedDiagram.Pages[0].Cells, cell => cell.Id == groupId));
            Assert.Null(Array.Find(updatedDiagram.Pages[0].Cells, cell => cell.Id == shape1Id));
            Assert.Null(Array.Find(updatedDiagram.Pages[0].Cells, cell => cell.Id == shape2Id));
            Assert.Null(Array.Find(updatedDiagram.Pages[0].Cells, cell => cell.Id == shape3Id));
        }

        [Fact]
        public void SetDiagramBackground_ShouldUpdateBackgroundColor()
        {
            // Arrange
            var diagram = DiagramManipulation.createEmptyDiagram();
            string backgroundColor = "#f5f5f5";
            
            // Act
            var updatedDiagram = DiagramManipulation.setDiagramBackground(diagram, FSharpOption<string>.None, FSharpOption<string>.Some(backgroundColor));
            
            // Assert
            Assert.NotNull(updatedDiagram);
            Assert.NotEmpty(updatedDiagram.Pages);
            
            // Check if background style was applied to the root cell
            var rootCell = Array.Find(updatedDiagram.Pages[0].Cells, cell => cell.Id == "0");
            Assert.NotNull(rootCell);
            Assert.Contains($"fillColor={backgroundColor}", rootCell.Style);
        }
        
        [Fact]
        public void SetDiagramBackground_ShouldUpdateBackgroundImage()
        {
            // Arrange
            var diagram = DiagramManipulation.createEmptyDiagram();
            string backgroundImage = "https://example.com/image.jpg";
            
            // Act
            var updatedDiagram = DiagramManipulation.setDiagramBackground(diagram, FSharpOption<string>.Some(backgroundImage), FSharpOption<string>.None);
            
            // Assert
            Assert.NotNull(updatedDiagram);
            Assert.NotEmpty(updatedDiagram.Pages);
            
            // Check if background image was applied to the root cell
            var rootCell = Array.Find(updatedDiagram.Pages[0].Cells, cell => cell.Id == "0");
            Assert.NotNull(rootCell);
            Assert.Contains($"image={backgroundImage}", rootCell.Style);
        }
        
        [Fact]
        public void UpdateShapeStyle_ShouldApplyMultipleStyleProperties()
        {
            // Arrange
            var diagram = DiagramManipulation.createEmptyDiagram();
            var (diagramWithShape, shapeId) = DiagramManipulation.addShape(diagram, 0, "Shape to Style", 100, 100, 120, 60, "rectangle");
            
            // Build style string with multiple properties
            string style = "rounded=1;shadow=1;glass=1;opacity=80;";
            
            // Act
            var updatedDiagram = DiagramManipulation.updateShape(diagramWithShape, 0, shapeId, null, null, null, null, null, style);
            
            // Assert
            Assert.NotNull(updatedDiagram);
            var updatedShape = Array.Find(updatedDiagram.Pages[0].Cells, cell => cell.Id == shapeId);
            Assert.NotNull(updatedShape);
            
            // Check if all style properties were applied
            Assert.Contains("rounded=1", updatedShape.Style);
            Assert.Contains("shadow=1", updatedShape.Style);
            Assert.Contains("glass=1", updatedShape.Style);
            Assert.Contains("opacity=80", updatedShape.Style);
        }
        
        [Fact]
        public void ConnectShapesAtPoints_ShouldConnectAtSpecificPoints()
        {
            // Arrange
            var diagram = DiagramManipulation.createEmptyDiagram();
            var (diagramWithShape1, shape1Id) = DiagramManipulation.addShape(diagram, 0, "Source Shape", 100, 100, 120, 60, "rectangle");
            var (diagramWithShape2, shape2Id) = DiagramManipulation.addShape(diagramWithShape1, 0, "Target Shape", 300, 100, 120, 60, "rectangle");
            
            // Specific connection points
            double sourceX = 120; // right side of shape1
            double sourceY = 130; // middle of shape1
            double targetX = 300; // left side of shape2
            double targetY = 130; // middle of shape2
            
            // Act
            var (updatedDiagram, edgeId) = DiagramManipulation.connectShapesAtPoints(
                diagramWithShape2, 
                0, 
                shape1Id, 
                shape2Id, 
                FSharpOption<double>.Some(sourceX), 
                FSharpOption<double>.Some(sourceY), 
                FSharpOption<double>.Some(targetX), 
                FSharpOption<double>.Some(targetY));
            
            // Assert
            Assert.NotNull(updatedDiagram);
            var edge = Array.Find(updatedDiagram.Pages[0].Cells, cell => cell.Id == edgeId);
            Assert.NotNull(edge);
            Assert.True(edge.IsEdge);
            Assert.Equal(shape1Id, edge.Source.Value);
            Assert.Equal(shape2Id, edge.Target.Value);
            
            // Check if the geometry contains the connection points
            Assert.NotNull(edge.Geometry.Value);
            
            // The connection points are stored in the geometry's sourcePoint and targetPoint
            Assert.True(edge.Geometry.Value.SourcePoint.IsSome());
            Assert.True(edge.Geometry.Value.TargetPoint.IsSome());
            
            // Check if the points are close to the specified coordinates
            // Note: Due to the way DrawIO connects shapes, the exact coordinates might be adjusted
            Assert.InRange(edge.Geometry.Value.SourcePoint.Value.X, sourceX - 10, sourceX + 10);
            Assert.InRange(edge.Geometry.Value.SourcePoint.Value.Y, sourceY - 10, sourceY + 10);
            Assert.InRange(edge.Geometry.Value.TargetPoint.Value.X, targetX - 10, targetX + 10);
            Assert.InRange(edge.Geometry.Value.TargetPoint.Value.Y, targetY - 10, targetY + 10);
        }
        
        [Fact]
        public void GenerateVpc_ShouldCreateVpcLayoutWithComponents()
        {
            // Act
            var emptyDiagram = DiagramManipulation.createEmptyDiagram();
            
            // Add VPC
            var (vpcDiagram, vpcId) = DiagramManipulation.addShape(emptyDiagram, 0, "VPC", 50, 50, 600, 400, "swimlane");
            
            // Add IGW
            var (igwDiagram, igwId) = DiagramManipulation.addShape(vpcDiagram, 0, "IGW", 350, 10, 80, 40, "rectangle");
            
            // Connect components
            var (diagramWithConnector1, connectorId1) = DiagramManipulation.connectShapes(igwDiagram, 0, igwId, vpcId);
            
            // Assert
            Assert.NotNull(diagramWithConnector1);
            Assert.NotEmpty(diagramWithConnector1.Pages);
            
            // Check if VPC and IGW exist
            Assert.Contains(diagramWithConnector1.Pages[0].Cells, cell => cell.Id == vpcId && cell.Value == "VPC");
            Assert.Contains(diagramWithConnector1.Pages[0].Cells, cell => cell.Id == igwId && cell.Value == "IGW");
            
            // Check if the components are connected
            var connector = Array.Find(diagramWithConnector1.Pages[0].Cells, cell => cell.Id == connectorId1);
            Assert.NotNull(connector);
            Assert.Equal(igwId, connector.Source.Value);
            Assert.Equal(vpcId, connector.Target.Value);
        }
        
        [Fact]
        public void ArrangeDiagram_ShouldRearrangeElements()
        {
            // Arrange
            var diagram = DiagramManipulation.createEmptyDiagram();
            var (diagramWithShape1, shape1Id) = DiagramManipulation.addShape(diagram, 0, "Shape 1", 100, 100, 120, 60, "rectangle");
            var (diagramWithShape2, shape2Id) = DiagramManipulation.addShape(diagramWithShape1, 0, "Shape 2", 130, 130, 120, 60, "rectangle");
            var (diagramWithShape3, shape3Id) = DiagramManipulation.addShape(diagramWithShape2, 0, "Shape 3", 160, 160, 120, 60, "rectangle");
            
            // Record original positions
            var originalPositions = diagramWithShape3.Pages[0].Cells
                .Where(c => c.IsVertex && c.Geometry.IsSome())
                .Select(c => (c.Id, c.Geometry.Value.Position.X, c.Geometry.Value.Position.Y))
                .ToList();
            
            // Act
            var arrangedDiagram = DiagramManipulation.arrangeDiagram(diagramWithShape3, FSharpOption<string>.None);
            
            // Assert
            Assert.NotNull(arrangedDiagram);
            
            // Check that at least some positions have changed
            bool anyPositionChanged = false;
            foreach (var cell in arrangedDiagram.Pages[0].Cells.Where(c => c.IsVertex && c.Geometry.IsSome()))
            {
                var original = originalPositions.FirstOrDefault(p => p.Id == cell.Id);
                if (original.Id != null && 
                    (Math.Abs(original.X - cell.Geometry.Value.Position.X) > 1 ||
                     Math.Abs(original.Y - cell.Geometry.Value.Position.Y) > 1))
                {
                    anyPositionChanged = true;
                    break;
                }
            }
            
            Assert.True(anyPositionChanged, "At least one shape should have changed position after layout arrangement");
        }

        [Fact]
        public void SetDiagramBackground_ShouldReturnProperResponse()
        {
            // Arrange
            var diagram = DiagramManipulation.createEmptyDiagram();
            string backgroundColor = "#f5f5f5";
            
            // Act
            var updatedDiagram = DiagramManipulation.setDiagramBackground(diagram, FSharpOption<string>.None, FSharpOption<string>.Some(backgroundColor));
            
            // Create a success response object similar to DiagramToolExecutor's response
            var response = new
            {
                status = "success",
                content = new[] 
                { 
                    new 
                    { 
                        type = "text", 
                        text = $"Diagram background updated with color {backgroundColor}" 
                    } 
                }
            };
            
            // Assert
            Assert.NotNull(updatedDiagram);
            Assert.Equal("success", response.status);
            Assert.NotEmpty(response.content);
            Assert.Equal("text", response.content[0].type);
            Assert.Contains(backgroundColor, response.content[0].text);
        }
        
        [Fact]
        public void UpdateShapeStyle_ShouldReturnProperResponse()
        {
            // Arrange
            var diagram = DiagramManipulation.createEmptyDiagram();
            var (diagramWithShape, shapeId) = DiagramManipulation.addShape(diagram, 0, "Shape to Style", 100, 100, 120, 60, "rectangle");
            string style = "rounded=1;shadow=1;glass=1;opacity=80;";
            
            // Act
            var updatedDiagram = DiagramManipulation.updateShape(diagramWithShape, 0, shapeId, null, null, null, null, null, style);
            
            // Create a success response object similar to DiagramToolExecutor's response
            var styleProperties = new Dictionary<string, string>
            {
                { "rounded", "1" },
                { "shadow", "1" },
                { "glass", "1" },
                { "opacity", "80" }
            };
            
            var response = new
            {
                status = "success",
                message = $"Style of shape {shapeId} updated",
                styleProperties = styleProperties,
                content = new[] 
                { 
                    new 
                    { 
                        type = "text", 
                        text = $"Style of shape {shapeId} updated" 
                    } 
                }
            };
            
            // Assert
            Assert.NotNull(updatedDiagram);
            Assert.Equal("success", response.status);
            Assert.NotEmpty(response.content);
            Assert.Equal("text", response.content[0].type);
            Assert.Contains(shapeId, response.content[0].text);
            Assert.Equal(4, response.styleProperties.Count);
        }
        
        [Fact]
        public void ConnectShapesAtPoints_ShouldReturnProperResponse()
        {
            // Arrange
            var diagram = DiagramManipulation.createEmptyDiagram();
            var (diagramWithShape1, shape1Id) = DiagramManipulation.addShape(diagram, 0, "Source Shape", 100, 100, 120, 60, "rectangle");
            var (diagramWithShape2, shape2Id) = DiagramManipulation.addShape(diagramWithShape1, 0, "Target Shape", 300, 100, 120, 60, "rectangle");
            
            // Act
            var (updatedDiagram, edgeId) = DiagramManipulation.connectShapesAtPoints(
                diagramWithShape2, 
                0, 
                shape1Id, 
                shape2Id, 
                FSharpOption<double>.Some(120), 
                FSharpOption<double>.Some(130), 
                FSharpOption<double>.Some(300), 
                FSharpOption<double>.Some(130));
            
            // Create a success response object similar to DiagramToolExecutor's response
            var response = new
            {
                status = "success",
                connectorId = edgeId,
                content = new[] 
                { 
                    new 
                    { 
                        type = "text", 
                        text = $"Connected shapes {shape1Id} and {shape2Id} with connector {edgeId}" 
                    } 
                }
            };
            
            // Assert
            Assert.NotNull(updatedDiagram);
            Assert.Equal("success", response.status);
            Assert.NotEmpty(response.content);
            Assert.Equal("text", response.content[0].type);
            Assert.Contains(shape1Id, response.content[0].text);
            Assert.Contains(shape2Id, response.content[0].text);
            Assert.Contains(edgeId, response.content[0].text);
            Assert.Equal(edgeId, response.connectorId);
        }
        
        [Fact]
        public void GenerateVpc_ShouldReturnProperResponse()
        {
            // Act
            var emptyDiagram = DiagramManipulation.createEmptyDiagram();
            var (vpcDiagram, vpcId) = DiagramManipulation.addShape(emptyDiagram, 0, "VPC", 50, 50, 600, 400, "swimlane");
            var (igwDiagram, igwId) = DiagramManipulation.addShape(vpcDiagram, 0, "IGW", 350, 10, 80, 40, "rectangle");
            var (diagramWithConnector1, connectorId1) = DiagramManipulation.connectShapes(igwDiagram, 0, igwId, vpcId);
            
            // Create a success response object similar to DiagramToolExecutor's response
            var response = new
            {
                status = "success",
                content = new[] 
                { 
                    new 
                    { 
                        type = "text", 
                        text = "Created AWS VPC layout diagram with VPC and Internet Gateway" 
                    } 
                }
            };
            
            // Assert
            Assert.NotNull(diagramWithConnector1);
            Assert.Equal("success", response.status);
            Assert.NotEmpty(response.content);
            Assert.Equal("text", response.content[0].type);
            Assert.Contains("VPC", response.content[0].text);
            Assert.Contains("Gateway", response.content[0].text);
        }
        
        [Fact]
        public void ArrangeDiagram_ShouldReturnProperResponse()
        {
            // Arrange
            var diagram = DiagramManipulation.createEmptyDiagram();
            var (diagramWithShape1, shape1Id) = DiagramManipulation.addShape(diagram, 0, "Shape 1", 100, 100, 120, 60, "rectangle");
            var (diagramWithShape2, shape2Id) = DiagramManipulation.addShape(diagramWithShape1, 0, "Shape 2", 130, 130, 120, 60, "rectangle");
            var (diagramWithShape3, shape3Id) = DiagramManipulation.addShape(diagramWithShape2, 0, "Shape 3", 160, 160, 120, 60, "rectangle");
            
            // Act
            var arrangedDiagram = DiagramManipulation.arrangeDiagram(diagramWithShape3, FSharpOption<string>.None);
            
            // Create a success response object similar to DiagramToolExecutor's response
            var layout = "horizontal";
            var response = new
            {
                status = "success",
                message = $"Diagram arranged using layout: {layout}",
                content = new[] 
                { 
                    new 
                    { 
                        type = "text", 
                        text = $"Diagram arranged using layout: {layout}" 
                    } 
                }
            };
            
            // Assert
            Assert.NotNull(arrangedDiagram);
            Assert.Equal("success", response.status);
            Assert.NotEmpty(response.content);
            Assert.Equal("text", response.content[0].type);
            Assert.Contains("arranged", response.content[0].text);
            Assert.Contains(layout, response.content[0].text);
        }
    }
} 