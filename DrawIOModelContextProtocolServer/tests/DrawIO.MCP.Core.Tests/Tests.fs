module DrawIO.MCP.Core.Tests

open System
open System.IO
open System.Xml.Linq
open Xunit
open DrawIO.MCP.Core
open DrawIO.MCP.Core.Types
open DrawIO.MCP.Core.DiagramManipulation
open DrawIO.MCP.Core.XmlSerializer

[<Fact>]
let ``Create Empty Diagram Creates Valid Structure`` () =
    // Act
    let diagram = createEmptyDiagram()
    
    // Assert
    Assert.NotNull(diagram)
    Assert.Single(diagram.Pages) |> ignore
    Assert.Equal(2, diagram.Pages.[0].Cells.Length)
    Assert.Equal("0", diagram.Pages.[0].Cells.[0].Id)
    Assert.Equal("1", diagram.Pages.[0].Cells.[1].Id)
    Assert.Equal("0", diagram.Pages.[0].Cells.[1].Parent)
    // Use ignore to explicitly discard the result
    diagram.Pages.[0] |> ignore

[<Fact>]
let ``Add Shape To Diagram Returns Updated Diagram And Shape Id`` () =
    // Arrange
    let diagram = createEmptyDiagram()
    
    // Act
    let (updatedDiagram, newId) = addShape diagram 0 "Test Shape" 100.0 200.0 300.0 400.0 "rectangle"
    
    // Assert
    Assert.NotNull(updatedDiagram)
    Assert.Equal(3, updatedDiagram.Pages.[0].Cells.Length)
    Assert.Equal(2, diagram.Pages.[0].Cells.Length) // Original diagram unchanged
    
    let addedShape = updatedDiagram.Pages.[0].Cells |> List.find (fun c -> c.Id = newId)
    Assert.Equal("Test Shape", addedShape.Value)
    Assert.Equal(true, addedShape.IsVertex)
    Assert.Equal(false, addedShape.IsEdge)
    Assert.Equal("1", addedShape.Parent)
    
    let geometry = addedShape.Geometry.Value
    Assert.Equal(100.0, geometry.Position.X)
    Assert.Equal(200.0, geometry.Position.Y)
    Assert.Equal(300.0, geometry.Size.Width)
    Assert.Equal(400.0, geometry.Size.Height)

[<Fact>]
let ``Connect Shapes Creates Edge Between Shapes`` () =
    // Arrange
    let diagram = createEmptyDiagram()
    let (diagramWithShape1, shape1Id) = addShape diagram 0 "Shape 1" 100.0 100.0 100.0 100.0 "rectangle"
    let (diagramWithShape2, shape2Id) = addShape diagramWithShape1 0 "Shape 2" 300.0 100.0 100.0 100.0 "rectangle"
    
    // Act
    let (diagramWithEdge, edgeId) = connectShapes diagramWithShape2 0 shape1Id shape2Id
    
    // Assert
    Assert.NotNull(diagramWithEdge)
    Assert.Equal(5, diagramWithEdge.Pages.[0].Cells.Length)
    
    let addedEdge = diagramWithEdge.Pages.[0].Cells |> List.find (fun c -> c.Id = edgeId)
    Assert.Equal(true, addedEdge.IsEdge)
    Assert.Equal(false, addedEdge.IsVertex)
    Assert.Equal("1", addedEdge.Parent)
    Assert.Equal(Some shape1Id, addedEdge.Source)
    Assert.Equal(Some shape2Id, addedEdge.Target)
    
    let geometry = addedEdge.Geometry.Value
    Assert.Equal(true, geometry.Relative)

[<Fact>]
let ``Serialize Diagram Creates Valid XML`` () =
    // Arrange
    let diagram = createEmptyDiagram()
    let (diagramWithShape, _) = addShape diagram 0 "Test Shape" 100.0 200.0 300.0 400.0 "rectangle"
    
    // Act
    let xml = XmlSerializer.serializeDiagram diagramWithShape
    
    // Assert
    Assert.NotNull(xml)
    Assert.NotEmpty(xml)
    
    // Parse the XML to verify structure
    let doc = XDocument.Parse(xml)
    let root = doc.Root
    Assert.Equal("mxfile", root.Name.LocalName)
    ignore root
    
    let diagrams = root.Elements(XName.Get("diagram"))
    Assert.Single(diagrams) |> ignore
    
    let firstDiagram = Seq.head diagrams
    Assert.Equal("Page-1", firstDiagram.Attribute(XName.Get("name")).Value)
    
    let mxGraphModel = firstDiagram.Element(XName.Get("mxGraphModel"))
    Assert.NotNull(mxGraphModel)
    mxGraphModel |> ignore
    
    let rootElement = mxGraphModel.Element(XName.Get("root"))
    Assert.NotNull(rootElement)
    rootElement |> ignore
    
    let cells = rootElement.Elements(XName.Get("mxCell"))
    Assert.Equal(3, Seq.length cells)
    // Use ignore to explicitly discard the result
    cells |> Seq.head |> ignore

[<Fact>]
let ``Serialize And Parse Round Trip Preserves Diagram`` () =
    // Arrange
    let originalDiagram = createEmptyDiagram()
    let (diagramWithShape, shapeId) = addShape originalDiagram 0 "Test Shape" 100.0 200.0 300.0 400.0 "rectangle"
    
    // Act
    let xml = XmlSerializer.serializeDiagram diagramWithShape
    let parsedDiagram = XmlParser.parseDiagram xml
    
    // Assert
    Assert.Equal(diagramWithShape.Pages.Length, parsedDiagram.Pages.Length)
    Assert.Equal(diagramWithShape.Pages.[0].Name, parsedDiagram.Pages.[0].Name)
    Assert.Equal(diagramWithShape.Pages.[0].Cells.Length, parsedDiagram.Pages.[0].Cells.Length)
    
    // Find the added shape in both diagrams and compare
    let originalShape = diagramWithShape.Pages.[0].Cells |> List.find (fun c -> c.Id = shapeId)
    let parsedShape = parsedDiagram.Pages.[0].Cells |> List.find (fun c -> c.Id = shapeId)
    
    Assert.Equal(originalShape.Value, parsedShape.Value)
    Assert.Equal(originalShape.IsVertex, parsedShape.IsVertex)
    Assert.Equal(originalShape.Parent, parsedShape.Parent)
    
    let originalGeometry = originalShape.Geometry.Value
    let parsedGeometry = parsedShape.Geometry.Value
    
    Assert.Equal(originalGeometry.Position.X, parsedGeometry.Position.X)
    Assert.Equal(originalGeometry.Position.Y, parsedGeometry.Position.Y)
    Assert.Equal(originalGeometry.Size.Width, parsedGeometry.Size.Width)
    Assert.Equal(originalGeometry.Size.Height, parsedGeometry.Size.Height)