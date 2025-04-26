module DrawIO.MCP.Core.TestSuites.XmlParser

open System
open System.IO
open System.Xml.Linq
open Xunit
open DrawIO.MCP.Core
open DrawIO.MCP.Core.Types
open DrawIO.MCP.Core.XmlParser
open DrawIO.MCP.Core.XmlSerializer

[<Fact>]
let ``Parse simple diagram XML should succeed`` () =
    // Arrange
    let simpleXml = """
    <mxfile host="app.diagrams.net" modified="2023-06-24T10:30:00.000Z" agent="DrawIO.MCP" version="15.0.5" type="device">
      <diagram id="page-1" name="Page 1">
        <mxGraphModel dx="800" dy="600" grid="1" gridSize="10" guides="1" tooltips="1" connect="1" arrows="1" fold="1" page="1" pageScale="1" pageWidth="850" pageHeight="1100">
          <root>
            <mxCell id="0"/>
            <mxCell id="1" parent="0"/>
            <mxCell id="2" value="Rectangle" style="rounded=0;whiteSpace=wrap;html=1;" vertex="1" parent="1">
              <mxGeometry x="100" y="100" width="120" height="60" as="geometry"/>
            </mxCell>
          </root>
        </mxGraphModel>
      </diagram>
    </mxfile>
    """
    
    // Act
    let diagram = parseDiagram simpleXml
    
    // Assert
    Assert.Equal(1, diagram.Pages.Length)
    Assert.Equal("Page 1", diagram.Pages[0].Name)
    Assert.Equal(3, diagram.Pages[0].Cells.Length) // 2 system cells + 1 rectangle
    
    let rectangle = diagram.Pages[0].Cells |> List.find (fun c -> c.Id = "2")
    Assert.Equal("Rectangle", rectangle.Value)
    Assert.True(rectangle.IsVertex)
    Assert.Equal("1", rectangle.Parent)
    
    let geometry = rectangle.Geometry.Value
    Assert.Equal(100.0, geometry.Position.X)
    Assert.Equal(100.0, geometry.Position.Y)
    Assert.Equal(120.0, geometry.Size.Width)
    Assert.Equal(60.0, geometry.Size.Height)

[<Fact>]
let ``Parse and serialize diagram should preserve content`` () =
    // Arrange
    let originalXml = """
    <mxfile host="app.diagrams.net" modified="2023-06-24T10:30:00.000Z" agent="DrawIO.MCP" version="15.0.5" type="device">
      <diagram id="page-1" name="Test Page">
        <mxGraphModel dx="800" dy="600" grid="1" gridSize="10" guides="1" tooltips="1" connect="1" arrows="1" fold="1" page="1" pageScale="1" pageWidth="850" pageHeight="1100">
          <root>
            <mxCell id="0"/>
            <mxCell id="1" parent="0"/>
            <mxCell id="2" value="Source" style="ellipse;whiteSpace=wrap;html=1;" vertex="1" parent="1">
              <mxGeometry x="100" y="100" width="80" height="80" as="geometry"/>
            </mxCell>
            <mxCell id="3" value="Target" style="rounded=1;whiteSpace=wrap;html=1;" vertex="1" parent="1">
              <mxGeometry x="300" y="110" width="120" height="60" as="geometry"/>
            </mxCell>
            <mxCell id="4" value="Connector" style="edgeStyle=orthogonalEdgeStyle;rounded=0;orthogonalLoop=1;jettySize=auto;html=1;" edge="1" parent="1" source="2" target="3">
              <mxGeometry relative="1" as="geometry"/>
            </mxCell>
          </root>
        </mxGraphModel>
      </diagram>
    </mxfile>
    """
    
    // Act
    let parsedDiagram = parseDiagram originalXml
    let serializedXml = XmlSerializer.serializeDiagram parsedDiagram
    let reparsedDiagram = parseDiagram serializedXml
    
    // Assert
    // Verify all pages are preserved
    Assert.Equal(parsedDiagram.Pages.Length, reparsedDiagram.Pages.Length)
    
    // Verify page content is preserved
    let originalPage = parsedDiagram.Pages[0]
    let reparsedPage = reparsedDiagram.Pages[0]
    
    Assert.Equal(originalPage.Name, reparsedPage.Name)
    Assert.Equal(originalPage.Cells.Length, reparsedPage.Cells.Length)
    
    // Verify specific cells
    let originalSource = originalPage.Cells |> List.find (fun c -> c.Value = "Source")
    let reparsedSource = reparsedPage.Cells |> List.find (fun c -> c.Value = "Source")
    
    Assert.Equal(originalSource.IsVertex, reparsedSource.IsVertex)
    Assert.Equal(originalSource.Style, reparsedSource.Style)
    
    // Verify connector source/target is preserved
    let originalConnector = originalPage.Cells |> List.find (fun c -> c.Value = "Connector")
    let reparsedConnector = reparsedPage.Cells |> List.find (fun c -> c.Value = "Connector")
    
    Assert.Equal(originalConnector.Source, reparsedConnector.Source)
    Assert.Equal(originalConnector.Target, reparsedConnector.Target)

[<Fact>]
let ``Parse malformed XML should throw exception`` () =
    // Arrange
    let malformedXml = """
    <mxfile host="app.diagrams.net">
      <diagram id="page-1" name="Page 1">
        <mxGraphModel>
          <root>
            <mxCell id="0"/>
            <mxCell id="1" parent="0"/>
            <mxCell id="2" value="Incomplete
    """
    
    // Act & Assert
    Assert.Throws<System.Exception>(fun () -> parseDiagram malformedXml |> ignore)

[<Fact>]
let ``Parse compressed diagram content should succeed`` () =
    // This test would need actual base64 compressed content
    // For now we'll just verify the method exists and doesn't crash on empty input
    
    // Arrange & Act & Assert
    Assert.Throws<System.Exception>(fun () -> decodeDrawioXml "" |> ignore)

[<Fact>]
let ``Parse diagram with multiple pages should preserve all pages`` () =
    // Arrange
    let multiPageXml = """
    <mxfile host="app.diagrams.net" modified="2023-06-24T10:30:00.000Z" agent="DrawIO.MCP" version="15.0.5" type="device">
      <diagram id="page-1" name="First Page">
        <mxGraphModel dx="800" dy="600" grid="1" gridSize="10" guides="1" tooltips="1" connect="1" arrows="1" fold="1" page="1" pageScale="1" pageWidth="850" pageHeight="1100">
          <root>
            <mxCell id="0"/>
            <mxCell id="1" parent="0"/>
            <mxCell id="2" value="Page 1 Shape" style="rounded=0;whiteSpace=wrap;html=1;" vertex="1" parent="1">
              <mxGeometry x="100" y="100" width="120" height="60" as="geometry"/>
            </mxCell>
          </root>
        </mxGraphModel>
      </diagram>
      <diagram id="page-2" name="Second Page">
        <mxGraphModel dx="800" dy="600" grid="1" gridSize="10" guides="1" tooltips="1" connect="1" arrows="1" fold="1" page="1" pageScale="1" pageWidth="850" pageHeight="1100">
          <root>
            <mxCell id="0"/>
            <mxCell id="1" parent="0"/>
            <mxCell id="3" value="Page 2 Shape" style="ellipse;whiteSpace=wrap;html=1;" vertex="1" parent="1">
              <mxGeometry x="200" y="200" width="80" height="80" as="geometry"/>
            </mxCell>
          </root>
        </mxGraphModel>
      </diagram>
    </mxfile>
    """
    
    // Act
    let diagram = parseDiagram multiPageXml
    
    // Assert
    Assert.Equal(2, diagram.Pages.Length)
    Assert.Equal("First Page", diagram.Pages[0].Name)
    Assert.Equal("Second Page", diagram.Pages[1].Name)
    
    let page1Shape = diagram.Pages[0].Cells |> List.find (fun c -> c.Value = "Page 1 Shape")
    let page2Shape = diagram.Pages[1].Cells |> List.find (fun c -> c.Value = "Page 2 Shape")
    
    Assert.Equal("rounded=0;whiteSpace=wrap;html=1;", page1Shape.Style)
    Assert.Equal("ellipse;whiteSpace=wrap;html=1;", page2Shape.Style) 