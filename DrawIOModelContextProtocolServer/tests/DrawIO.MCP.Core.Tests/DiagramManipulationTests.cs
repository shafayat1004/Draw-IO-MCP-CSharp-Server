using System;
using System.IO;
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
    }
} 