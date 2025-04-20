using System;
using System.Linq;
using DrawIO.MCP.Core;
using Xunit;

namespace DrawIO.MCP.Core.Tests
{
    public class PageManagementTests
    {
        [Fact]
        public void CreateDiagramPage_ShouldAddNewPage()
        {
            // Arrange
            var diagram = DiagramManipulation.createEmptyDiagram();
            int initialPageCount = diagram.Pages.Length;
            
            // Act
            var updatedDiagram = FileOperations.createDiagramPage(diagram, "Second Page");
            
            // Assert
            Assert.Equal(initialPageCount + 1, updatedDiagram.Pages.Length);
            Assert.Equal("Second Page", updatedDiagram.Pages[1].Name);
            Assert.NotNull(updatedDiagram.Pages[1].Id);
            
            // Verify the new page has the default cells
            Assert.Equal(2, updatedDiagram.Pages[1].Cells.Length);
            Assert.Equal("0", updatedDiagram.Pages[1].Cells[0].Id);
            Assert.Equal("1", updatedDiagram.Pages[1].Cells[1].Id);
        }
        
        [Fact]
        public void GetDiagramPage_ShouldFindPageById()
        {
            // Arrange
            var diagram = DiagramManipulation.createEmptyDiagram();
            var diagramWithSecondPage = FileOperations.createDiagramPage(diagram, "Second Page");
            string secondPageId = diagramWithSecondPage.Pages[1].Id;
            
            // Act
            var foundPage = FileOperations.getDiagramPage(diagramWithSecondPage, secondPageId);
            
            // Assert
            Assert.NotNull(foundPage);
            Assert.True(foundPage.IsSome());
            Assert.Equal("Second Page", foundPage.Value.Name);
        }
        
        [Fact]
        public void GetDiagramPage_ShouldFindPageByIndex()
        {
            // Arrange
            var diagram = DiagramManipulation.createEmptyDiagram();
            var diagramWithSecondPage = FileOperations.createDiagramPage(diagram, "Second Page");
            
            // Act
            var foundPage = FileOperations.getDiagramPage(diagramWithSecondPage, 1); // Index as int
            
            // Assert
            Assert.NotNull(foundPage);
            Assert.True(foundPage.IsSome());
            Assert.Equal("Second Page", foundPage.Value.Name);
        }
        
        [Fact]
        public void GetDiagramPage_ShouldReturnNoneForNonExistentPage()
        {
            // Arrange
            var diagram = DiagramManipulation.createEmptyDiagram();
            
            // Act
            var foundPage = FileOperations.getDiagramPage(diagram, "non-existent-id");
            
            // Assert
            Assert.NotNull(foundPage);
            Assert.False(foundPage.IsSome());
        }
        
        [Fact]
        public void UpdateDiagramPage_ShouldModifyPageName()
        {
            // Arrange
            var diagram = DiagramManipulation.createEmptyDiagram();
            string pageId = diagram.Pages[0].Id;
            
            // Act
            var updatedDiagram = FileOperations.updateDiagramPage(diagram, pageId, Microsoft.FSharp.Core.FSharpOption<string>.Some("Updated Page Name"));
            
            // Assert
            Assert.Equal("Updated Page Name", updatedDiagram.Pages[0].Name);
        }
        
        [Fact]
        public void DeleteDiagramPage_ShouldRemovePage()
        {
            // Arrange
            var diagram = DiagramManipulation.createEmptyDiagram();
            var diagramWithSecondPage = FileOperations.createDiagramPage(diagram, "Second Page");
            var diagramWithThirdPage = FileOperations.createDiagramPage(diagramWithSecondPage, "Third Page");
            int initialPageCount = diagramWithThirdPage.Pages.Length;
            string secondPageId = diagramWithThirdPage.Pages[1].Id;
            
            // Act
            var updatedDiagram = FileOperations.deleteDiagramPage(diagramWithThirdPage, secondPageId);
            
            // Assert
            Assert.Equal(initialPageCount - 1, updatedDiagram.Pages.Length);
            Assert.Equal("Third Page", updatedDiagram.Pages[1].Name); // Third page should now be the second
            Assert.DoesNotContain(updatedDiagram.Pages, p => p.Id == secondPageId);
        }
        
        [Fact]
        public void DeleteDiagramPage_ShouldNotDeleteLastPage()
        {
            // Arrange
            var diagram = DiagramManipulation.createEmptyDiagram();
            string pageId = diagram.Pages[0].Id;
            
            // Act
            var updatedDiagram = FileOperations.deleteDiagramPage(diagram, pageId);
            
            // Assert
            Assert.Equal(1, updatedDiagram.Pages.Length); // Should still have the default page
            Assert.Equal(pageId, updatedDiagram.Pages[0].Id); // Page should not be deleted
        }
        
        [Fact]
        public void MoveCellBetweenPages_ShouldMoveShapeToNewPage()
        {
            // Arrange
            var diagram = DiagramManipulation.createEmptyDiagram();
            var diagramWithSecondPage = FileOperations.createDiagramPage(diagram, "Second Page");
            
            // Add a shape to the first page
            var (diagramWithShape, shapeId) = DiagramManipulation.addShape(diagramWithSecondPage, 0, "Test Shape", 100, 200, 120, 60, "rectangle");
            
            // Get page IDs
            string sourcePageId = diagramWithShape.Pages[0].Id;
            string targetPageId = diagramWithShape.Pages[1].Id;
            
            // Verify the shape exists in the source page
            Assert.Contains(diagramWithShape.Pages[0].Cells, c => c.Id == shapeId);
            
            // Act
            var updatedDiagram = DiagramManipulation.moveCellBetweenPages(diagramWithShape, shapeId, sourcePageId, targetPageId);
            
            // Assert
            Assert.DoesNotContain(updatedDiagram.Pages[0].Cells, c => c.Id == shapeId);
            Assert.Contains(updatedDiagram.Pages[1].Cells, c => c.Id == shapeId);
        }
        
        [Fact]
        public void MoveCellBetweenPages_ShouldMoveEdgeAndPreserveConnections()
        {
            // Arrange
            var diagram = DiagramManipulation.createEmptyDiagram();
            var diagramWithSecondPage = FileOperations.createDiagramPage(diagram, "Second Page");
            
            // Add shapes to the first page
            var (diagramWithShape1, shape1Id) = DiagramManipulation.addShape(diagramWithSecondPage, 0, "Shape 1", 100, 100, 120, 60, "rectangle");
            var (diagramWithShape2, shape2Id) = DiagramManipulation.addShape(diagramWithShape1, 0, "Shape 2", 300, 100, 120, 60, "rectangle");
            
            // Connect the shapes
            var (diagramWithEdge, edgeId) = DiagramManipulation.connectShapes(diagramWithShape2, 0, shape1Id, shape2Id);
            
            // Get page IDs
            string sourcePageId = diagramWithEdge.Pages[0].Id;
            string targetPageId = diagramWithEdge.Pages[1].Id;
            
            // First move both shapes
            var diagramWithMovedShape1 = DiagramManipulation.moveCellBetweenPages(diagramWithEdge, shape1Id, sourcePageId, targetPageId);
            var diagramWithMovedShape2 = DiagramManipulation.moveCellBetweenPages(diagramWithMovedShape1, shape2Id, sourcePageId, targetPageId);
            
            // Act - Move the edge
            var updatedDiagram = DiagramManipulation.moveCellBetweenPages(diagramWithMovedShape2, edgeId, sourcePageId, targetPageId);
            
            // Assert
            // Edge should no longer be in the source page
            Assert.DoesNotContain(updatedDiagram.Pages[0].Cells, c => c.Id == edgeId);
            
            // Edge should be in the target page
            var movedEdge = updatedDiagram.Pages[1].Cells.First(c => c.Id == edgeId);
            Assert.NotNull(movedEdge);
            
            // Edge connections should be preserved
            Assert.Equal(shape1Id, movedEdge.Source.Value);
            Assert.Equal(shape2Id, movedEdge.Target.Value);
        }
        
        [Fact]
        public void DiagramOperations_ShouldMaintainPageConsistency()
        {
            // Arrange
            var diagram = DiagramManipulation.createEmptyDiagram();
            
            // Add multiple pages
            var diagramWithPages = FileOperations.createDiagramPage(diagram, "Page 2");
            diagramWithPages = FileOperations.createDiagramPage(diagramWithPages, "Page 3");
            
            // Add shapes to different pages
            var (diagramWithShapes, _) = DiagramManipulation.addShape(diagramWithPages, 0, "Shape on Page 1", 100, 100, 120, 60, "rectangle");
            var (updatedDiagram, _) = DiagramManipulation.addShape(diagramWithShapes, 1, "Shape on Page 2", 200, 200, 120, 60, "rectangle");
            var (finalDiagram, _) = DiagramManipulation.addShape(updatedDiagram, 2, "Shape on Page 3", 300, 300, 120, 60, "rectangle");
            
            // Act
            // Serialize and parse to simulate round-trip
            string xml = XmlSerializer.serializeDiagram(finalDiagram);
            var parsedDiagram = XmlParser.parseDiagram(xml);
            
            // Assert
            Assert.Equal(3, parsedDiagram.Pages.Length);
            Assert.Equal("Page-1", parsedDiagram.Pages[0].Name);
            Assert.Equal("Page 2", parsedDiagram.Pages[1].Name);
            Assert.Equal("Page 3", parsedDiagram.Pages[2].Name);
            
            // Verify each page has the right number of cells (base cells + the added shape)
            Assert.Equal(3, parsedDiagram.Pages[0].Cells.Length); // 2 base cells + 1 shape
            Assert.Equal(3, parsedDiagram.Pages[1].Cells.Length); // 2 base cells + 1 shape
            Assert.Equal(3, parsedDiagram.Pages[2].Cells.Length); // 2 base cells + 1 shape
            
            // Verify the shapes have the right text on each page
            Assert.Contains(parsedDiagram.Pages[0].Cells, c => c.Value == "Shape on Page 1");
            Assert.Contains(parsedDiagram.Pages[1].Cells, c => c.Value == "Shape on Page 2");
            Assert.Contains(parsedDiagram.Pages[2].Cells, c => c.Value == "Shape on Page 3");
        }
    }
} 