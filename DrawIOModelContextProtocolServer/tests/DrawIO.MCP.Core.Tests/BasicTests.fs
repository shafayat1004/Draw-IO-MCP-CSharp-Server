module DrawIO.MCP.Core.Tests.BasicTests

open System
open Xunit
open DrawIO.MCP.Core

[<Fact>]
let ``Create empty diagram should succeed``() =
    // Create a temporary file path for testing
    let tempFilePath = System.IO.Path.GetTempFileName()
    
    // Create a new diagram
    let diagram = FileOperations.createNewDiagram tempFilePath
    
    // Verify the diagram has at least one page
    Assert.True(diagram.Pages.Length >= 1)
    
    // Clean up
    try
        System.IO.File.Delete(tempFilePath)
    with _ -> () 