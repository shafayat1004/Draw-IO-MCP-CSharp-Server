1.  **Central Dispatch:** `DrawIOModelContextProtocolServer/src/DrawIO.MCP.STDIO/McpRequestDispatcher.cs` handles incoming MCP requests. It has a dictionary (`_methodHandlers`) mapping method names (like `tools/execute`) to handler functions. The `ExecuteToolAsync` method in this class is the entry point for tool calls.
2.  **Tool Execution:** `ExecuteToolAsync` in `McpRequestDispatcher` calls `DiagramToolExecutor.ExecuteToolAsync` (`DrawIOModelContextProtocolServer/src/DrawIO.MCP.STDIO/DiagramToolExecutor.cs`).
3.  **DiagramToolExecutor:** This static class contains a large `switch` statement that routes the call to specific `async Task<object>` methods based on the `toolName`. Each of these methods implements the logic for a single tool.
4.  **State Handling (Load/Save):** Inside each tool's method in `DiagramToolExecutor`, the pattern seems to be:
    *   Get the diagram name from parameters.
    *   Construct the file path (`Path.Combine(diagramsDirectory, diagramName)`).
    *   Load the diagram using `DrawIO.MCP.Core.FileOperations.loadDiagram(filePath)` or the helper `LoadDiagram(filePath)`. This calls into the F# Core library.
    *   Call the relevant manipulation function from the F# Core library (e.g., `DrawIO.MCP.Core.DiagramManipulation.addShape`, `DrawIO.MCP.Core.FileOperations.deleteDiagramPage`). These functions typically take the current diagram object and parameters, and return the *updated* diagram object.
    *   Save the *updated* diagram object back to the file using `DrawIO.MCP.Core.FileOperations.saveDiagram(updatedDiagram, diagramPath)` or the helper `SaveDiagram(updatedDiagram, filePath)`.
5.  **Core Library:** The actual diagram manipulation logic resides in `DrawIO.MCP.Core` (F# library), specifically in modules like `FileOperations`, `DiagramManipulation`, `ShapeLibrary`, etc. The state seems to be represented by the `DrawIO.MCP.Core.Types.Diagram` F# type, which likely wraps or represents the XML structure (`mxGraphModel` mentioned in `XmlParser.fs`).
6.  **No Obvious Session State:** I don't see an immediate session management system in the STDIO server code that would easily hold the undo/redo stacks per diagram. State is primarily managed by loading/saving the file for each operation. The SSE server (`DrawIO.MCP.SSE/Program.cs`) uses a `DrawIoService` which *might* hold state, but the main focus here seems to be the STDIO server based on the previous context.

**Implications for Undo/Redo Plan:**

*   **Central Integration Point:** The best place to hook in the undo/redo logic seems to be within `DiagramToolExecutor.ExecuteToolAsync` *before* calling the specific tool method and *after* it returns successfully (before saving).
*   **State Capture:** We need to capture the state *after* `LoadDiagram` but *before* calling the Core F# manipulation function. The state is likely the `DrawIO.MCP.Core.Types.Diagram` object returned by `LoadDiagram`.
*   **Stack Management:** Since there's no obvious session state mechanism in the STDIO part, we'll need to introduce one. A simple approach could be a static `Dictionary<string, Tuple<Stack<Diagram>, Stack<Diagram>>>` where the key is the diagram filename (`diagramName`) and the value is a tuple containing the undo and redo stacks for that diagram. We'll need to handle potential concurrency issues if multiple operations on the *same* diagram could happen simultaneously (though less likely in a typical MCP scenario). Using `ConcurrentDictionary` might be safer.
*   **State Representation in Stacks:** Storing the `DrawIO.MCP.Core.Types.Diagram` F# object directly in the stacks might be more efficient than serializing/deserializing XML strings repeatedly, but we need to ensure these objects are properly cloned/copied so that modifications to the *current* diagram don't affect the states stored in the stacks. If deep cloning is complex, storing the XML string might be simpler despite the overhead. Given the load/save cycle happens anyway, storing the XML string (captured *before* modification) seems feasible and aligns with the file-based persistence.
*   **`undo`/`redo` Tools:** These new tools would be added to the `switch` in `DiagramToolExecutor`. They would access the static dictionary, manipulate the stacks, and call `SaveDiagram` with the restored state.

**Refined Plan Element:**

*   **Data Structure:** Use a `System.Collections.Concurrent.ConcurrentDictionary<string, Tuple<Stack<string>, Stack<string>>>` (static member within `DiagramToolExecutor` or a dedicated state management class) to store undo/redo stacks (as XML strings) keyed by diagram filename.
*   **Integration Logic in `DiagramToolExecutor.ExecuteToolAsync`:**
    1.  Identify if the `toolName` is a state-modifying tool (needs a predefined list or attribute).
    2.  If modifying:
        *   Extract `diagramName`.
        *   Load the diagram XML *before* calling the specific tool method (`LoadDiagram` followed by getting its XML representation, or just `File.ReadAllTextAsync`).
        *   Get or create the `undo`/`redo` stacks for this `diagramName` from the concurrent dictionary.
        *   Call the specific tool's async method (e.g., `AddShapeAsync`).
        *   **If successful:** Push the *pre-modification* XML state onto the `undoStack` (manage history depth), clear the `redoStack`. *Then* call `SaveDiagram` with the modified diagram object returned by the tool method.
        *   **If fails:** Do not modify stacks or save.
*   **`undo`/`redo` Tool Logic:**
    1.  Extract `diagramName`.
    2.  Access the stacks from the dictionary.
    3.  Perform stack operations (pop from one, push current state to the other).
    4.  Get the restored XML state.
    5.  Use a function (perhaps in `Core.FileOperations`) to parse this XML string back into a `Diagram` object.
    6.  Call `SaveDiagram` with the restored `Diagram` object.

This refined plan seems more concrete based on the codebase structure. Does this look good?
