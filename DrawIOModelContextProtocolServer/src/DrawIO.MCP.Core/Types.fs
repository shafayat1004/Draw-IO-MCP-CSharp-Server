module DrawIO.MCP.Core.Types

open System

/// Types for representing DrawIO diagram elements

/// A position in the diagram
type Position = {
    X: float
    Y: float
}

/// Size of an element
type Size = {
    Width: float
    Height: float
}

/// A waypoint for a connector
type Waypoint = {
    X: float
    Y: float
    IsRelative: bool
}

/// Geometry of an element
type Geometry = {
    Position: Position
    Size: Size
    Relative: bool
    Waypoints: Waypoint list
}

/// An element in the diagram
type Element = {
    Id: string
    Value: string
    Style: string
    IsVertex: bool
    IsEdge: bool
    Parent: string
    Source: string option
    Target: string option
    Geometry: Geometry option
}

/// A diagram page
type Page = {
    Id: string
    Name: string
    Cells: Element list
}

/// A complete diagram
type Diagram = {
    Modified: DateTime
    Pages: Page list
}

/// Query result for element info
type ElementInfo = {
    Id: string
    Type: string
    Value: string
    Position: Position option
    Size: Size option
    Style: string
    Parent: string
    Connections: (string * string) list
    IsEdge: bool
    Source: string option
    Target: string option
    Waypoints: Waypoint list option
}

/// Bounding box of a diagram
type BoundingBox = {
    MinX: float
    MinY: float
    MaxX: float
    MaxY: float
    Width: float
    Height: float
}

/// Custom shape library definition
type CustomShape = {
    Id: string
    Name: string
    Style: string
    Width: float
    Height: float
    XmlDefinition: string option
}

/// A collection of custom shapes
type ShapeLibrary = {
    Name: string
    Shapes: CustomShape list
} 