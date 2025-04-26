using System;
using System.IO;
using DrawIO.MCP.Core;
using Xunit;

namespace DrawIO.MCP.Core.Tests
{
    public class ErrorHandlingTests
    {
        [Fact]
        public void AddShape_InvalidPageIndex_ThrowsException()
        {
            // Arrange
            var diagram = DiagramManipulation.createEmptyDiagram();
            
            // Act & Assert
            Assert.Throws<IndexOutOfRangeException>(() => 
                DiagramManipulation.addShape(diagram, 999, "Test Shape", 100, 100, 120, 60, "rectangle"));
        }
        
        [Fact]
        public void ConnectShapes_NonExistentSourceShape_ThrowsException()
        {
            // Arrange
            var diagram = DiagramManipulation.createEmptyDiagram();
            var (diagramWithShape, targetId) = DiagramManipulation.addShape(diagram, 0, "Target", 300, 100, 120, 60, "rectangle");
            
            // Act & Assert
            Assert.Throws<ArgumentException>(() => 
                DiagramManipulation.connectShapes(diagramWithShape, 0, "non-existent-id", targetId));
        }
        
        [Fact]
        public void ConnectShapes_NonExistentTargetShape_ThrowsException()
        {
            // Arrange
            var diagram = DiagramManipulation.createEmptyDiagram();
            var (diagramWithShape, sourceId) = DiagramManipulation.addShape(diagram, 0, "Source", 100, 100, 120, 60, "rectangle");
            
            // Act & Assert
            Assert.Throws<ArgumentException>(() => 
                DiagramManipulation.connectShapes(diagramWithShape, 0, sourceId, "non-existent-id"));
        }
        
        [Fact]
        public void DeleteShape_NonExistentShape_ThrowsException()
        {
            // Arrange
            var diagram = DiagramManipulation.createEmptyDiagram();
            
            // Act & Assert
            Assert.Throws<ArgumentException>(() => 
                DiagramManipulation.deleteShape(diagram, 0, "non-existent-id"));
        }
        
        [Fact]
        public void UpdateShape_NonExistentShape_ThrowsException()
        {
            // Arrange
            var diagram = DiagramManipulation.createEmptyDiagram();
            
            // Act & Assert
            Assert.Throws<ArgumentException>(() => 
                DiagramManipulation.updateShape(diagram, 0, "non-existent-id", "Updated Text", 200, 200, 150, 75, "fillColor=#ff0000"));
        }
        
        [Fact]
        public void LoadDiagram_NonExistentFile_ThrowsException()
        {
            // Act & Assert
            Assert.Throws<FileNotFoundException>(() => 
                FileOperations.loadDiagram("non-existent-file.drawio"));
        }
        
        [Fact]
        public void ParseDiagram_InvalidXml_ThrowsException()
        {
            // Arrange
            string invalidXml = "<not-valid-drawio>This is not valid DrawIO XML</not-valid-drawio>";
            
            // Act & Assert
            Assert.Throws<Exception>(() => XmlParser.parseDiagram(invalidXml));
        }
        
        [Fact]
        public void MoveCellBetweenPages_NonExistentCell_ThrowsException()
        {
            // Arrange
            var diagram = DiagramManipulation.createEmptyDiagram();
            var diagramWithSecondPage = FileOperations.createDiagramPage(diagram, "Second Page");
            string sourcePageId = diagramWithSecondPage.Pages[0].Id;
            string targetPageId = diagramWithSecondPage.Pages[1].Id;
            
            // Act & Assert
            Assert.Throws<ArgumentException>(() => 
                DiagramManipulation.moveCellBetweenPages(diagramWithSecondPage, "non-existent-id", sourcePageId, targetPageId));
        }
        
        [Fact]
        public void MoveCellBetweenPages_NonExistentSourcePage_ThrowsException()
        {
            // Arrange
            var diagram = DiagramManipulation.createEmptyDiagram();
            var diagramWithSecondPage = FileOperations.createDiagramPage(diagram, "Second Page");
            var (diagramWithShape, shapeId) = DiagramManipulation.addShape(diagramWithSecondPage, 0, "Test Shape", 100, 100, 120, 60, "rectangle");
            string targetPageId = diagramWithShape.Pages[1].Id;
            
            // Act & Assert
            Assert.Throws<ArgumentException>(() => 
                DiagramManipulation.moveCellBetweenPages(diagramWithShape, shapeId, "non-existent-page-id", targetPageId));
        }
        
        [Fact]
        public void MoveCellBetweenPages_NonExistentTargetPage_ThrowsException()
        {
            // Arrange
            var diagram = DiagramManipulation.createEmptyDiagram();
            var diagramWithSecondPage = FileOperations.createDiagramPage(diagram, "Second Page");
            var (diagramWithShape, shapeId) = DiagramManipulation.addShape(diagramWithSecondPage, 0, "Test Shape", 100, 100, 120, 60, "rectangle");
            string sourcePageId = diagramWithShape.Pages[0].Id;
            
            // Act & Assert
            Assert.Throws<ArgumentException>(() => 
                DiagramManipulation.moveCellBetweenPages(diagramWithShape, shapeId, sourcePageId, "non-existent-page-id"));
        }
        
        [Fact]
        public void UpdateDiagramPage_NonExistentPage_NoError()
        {
            // Arrange
            var diagram = DiagramManipulation.createEmptyDiagram();
            
            // Act - This should not throw an exception, just return the diagram unchanged
            var updatedDiagram = FileOperations.updateDiagramPage(diagram, "non-existent-page-id", 
                Microsoft.FSharp.Core.FSharpOption<string>.Some("New Name"));
            
            // Assert
            Assert.Equal(diagram.Pages.Length, updatedDiagram.Pages.Length);
            Assert.Equal("Page-1", updatedDiagram.Pages[0].Name); // Name should not be changed
        }
        
        [Fact]
        public void DeleteDiagramPage_NonExistentPage_NoError()
        {
            // Arrange
            var diagram = DiagramManipulation.createEmptyDiagram();
            
            // Act - This should not throw an exception, just return the diagram unchanged
            var updatedDiagram = FileOperations.deleteDiagramPage(diagram, "non-existent-page-id");
            
            // Assert
            Assert.Equal(diagram.Pages.Length, updatedDiagram.Pages.Length);
        }
        
        [Fact]
        public void ArrangeLayout_InvalidPageIndex_ThrowsException()
        {
            // Arrange
            var diagram = DiagramManipulation.createEmptyDiagram();
            
            // Act & Assert
            Assert.Throws<IndexOutOfRangeException>(() => 
                DiagramManipulation.arrangeLayout(diagram, 999, "horizontal"));
        }
        
        [Fact]
        public void ArrangeLayout_EmptyDiagram_ReturnsUnchanged()
        {
            // Arrange
            var diagram = DiagramManipulation.createEmptyDiagram();
            
            // Act
            var arrangedDiagram = DiagramManipulation.arrangeLayout(diagram, 0, "horizontal");
            
            // Assert - Should not throw and should return a diagram with same structure
            Assert.Equal(diagram.Pages.Length, arrangedDiagram.Pages.Length);
            Assert.Equal(diagram.Pages[0].Cells.Length, arrangedDiagram.Pages[0].Cells.Length);
        }
        
        [Fact]
        public void DiagramManipulation_NegativeCoordinates_Accepted()
        {
            // Arrange
            var diagram = DiagramManipulation.createEmptyDiagram();
            
            // Act - Use negative coordinates
            var (updatedDiagram, shapeId) = DiagramManipulation.addShape(diagram, 0, "Negative Position", -100, -200, 120, 60, "rectangle");
            
            // Assert
            var addedShape = Array.Find(updatedDiagram.Pages[0].Cells, c => c.Id == shapeId);
            Assert.NotNull(addedShape);
            Assert.NotNull(addedShape.Geometry.Value);
            Assert.Equal(-100, addedShape.Geometry.Value.Position.X);
            Assert.Equal(-200, addedShape.Geometry.Value.Position.Y);
        }
        
        [Fact]
        public void DiagramManipulation_ZeroSizeShape_Accepted()
        {
            // Arrange
            var diagram = DiagramManipulation.createEmptyDiagram();
            
            // Act - Use zero width/height
            var (updatedDiagram, shapeId) = DiagramManipulation.addShape(diagram, 0, "Zero Size", 100, 100, 0, 0, "rectangle");
            
            // Assert
            var addedShape = Array.Find(updatedDiagram.Pages[0].Cells, c => c.Id == shapeId);
            Assert.NotNull(addedShape);
            Assert.NotNull(addedShape.Geometry.Value);
            Assert.Equal(0, addedShape.Geometry.Value.Size.Width);
            Assert.Equal(0, addedShape.Geometry.Value.Size.Height);
        }
        
        [Fact]
        public void SetDiagramBackground_WithNonExistentDiagram_ShouldThrowException()
        {
            // Arrange
            string nonExistentDiagram = Path.Combine(Path.GetTempPath(), "non_existent_diagram.drawio");
            
            // Act & Assert
            var exception = Assert.Throws<FileNotFoundException>(() => 
                DiagramManipulation.setDiagramBackground(null, Microsoft.FSharp.Core.FSharpOption<string>.Some("https://example.com/image.jpg"), Microsoft.FSharp.Core.FSharpOption<string>.None));
            
            Assert.Contains("diagram", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        
        [Fact]
        public void SetDiagramBackground_WithNoParameters_ShouldThrowException()
        {
            // Arrange
            var diagram = DiagramManipulation.createEmptyDiagram();
            
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => 
                DiagramManipulation.setDiagramBackground(diagram, Microsoft.FSharp.Core.FSharpOption<string>.None, Microsoft.FSharp.Core.FSharpOption<string>.None));
            
            Assert.Contains("background", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        
        [Fact]
        public void UpdateShapeStyle_WithNonExistentShape_ShouldThrowException()
        {
            // Arrange
            var diagram = DiagramManipulation.createEmptyDiagram();
            
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => 
                DiagramManipulation.updateShape(diagram, 0, "non-existent-shape", null, null, null, null, null, "rounded=1;shadow=1;"));
            
            Assert.Contains("not found", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        
        [Fact]
        public void UpdateShapeStyle_WithInvalidPageIndex_ShouldThrowException()
        {
            // Arrange
            var diagram = DiagramManipulation.createEmptyDiagram();
            var (diagramWithShape, shapeId) = DiagramManipulation.addShape(diagram, 0, "Test Shape", 100, 100, 120, 60, "rectangle");
            
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => 
                DiagramManipulation.updateShape(diagramWithShape, 999, shapeId, null, null, null, null, null, "rounded=1;shadow=1;"));
            
            Assert.Contains("page", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        
        [Fact]
        public void ConnectShapesAtPoints_WithNonExistentSourceShape_ShouldThrowException()
        {
            // Arrange
            var diagram = DiagramManipulation.createEmptyDiagram();
            var (diagramWithShape, targetId) = DiagramManipulation.addShape(diagram, 0, "Target Shape", 300, 100, 120, 60, "rectangle");
            
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => 
                DiagramManipulation.connectShapesAtPoints(
                    diagramWithShape, 
                    0, 
                    "non-existent-source", 
                    targetId, 
                    Microsoft.FSharp.Core.FSharpOption<double>.None, 
                    Microsoft.FSharp.Core.FSharpOption<double>.None, 
                    Microsoft.FSharp.Core.FSharpOption<double>.None, 
                    Microsoft.FSharp.Core.FSharpOption<double>.None));
            
            Assert.Contains("source", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        
        [Fact]
        public void ConnectShapesAtPoints_WithNonExistentTargetShape_ShouldThrowException()
        {
            // Arrange
            var diagram = DiagramManipulation.createEmptyDiagram();
            var (diagramWithShape, sourceId) = DiagramManipulation.addShape(diagram, 0, "Source Shape", 100, 100, 120, 60, "rectangle");
            
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => 
                DiagramManipulation.connectShapesAtPoints(
                    diagramWithShape, 
                    0, 
                    sourceId, 
                    "non-existent-target", 
                    Microsoft.FSharp.Core.FSharpOption<double>.None, 
                    Microsoft.FSharp.Core.FSharpOption<double>.None, 
                    Microsoft.FSharp.Core.FSharpOption<double>.None, 
                    Microsoft.FSharp.Core.FSharpOption<double>.None));
            
            Assert.Contains("target", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        
        [Fact]
        public void GenerateVpc_ShouldFailWithInvalidVpcName()
        {
            // Not directly applicable to the core functions as it's a higher-level operation 
            // that uses multiple core functions, but we can test if VPC creation fails with an empty name
            
            // Arrange
            var emptyDiagram = DiagramManipulation.createEmptyDiagram();
            
            // Act & Assert - Try to add a VPC with an empty name
            var exception = Assert.Throws<ArgumentException>(() => 
                DiagramManipulation.addShape(emptyDiagram, 0, "", 50, 50, 600, 400, "swimlane"));
            
            Assert.Contains("value", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        
        [Fact]
        public void ArrangeDiagram_WithInvalidPageId_ShouldThrowException()
        {
            // Arrange
            var diagram = DiagramManipulation.createEmptyDiagram();
            
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => 
                DiagramManipulation.arrangeDiagram(diagram, Microsoft.FSharp.Core.FSharpOption<string>.Some("non-existent-page-id")));
            
            Assert.Contains("page", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
    }
} 