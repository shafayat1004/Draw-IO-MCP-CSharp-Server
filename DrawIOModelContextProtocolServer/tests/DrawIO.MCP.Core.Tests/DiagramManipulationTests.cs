using System;
using System.IO;
using System.Linq;
using DrawIO.MCP.Core;
using Xunit;

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
            var diagramWithOneWaypoint = DiagramManipulation.addWaypoint(diagramWithConnector, 0, connectorId, 200, 150, false, null);
            
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
    }
} 