# Feature Expectation

- Atomic tools for diagram building/editing (beyond what’s already listed)
- Undo/redo architecture design in the MCP Server
- Feedback loop capabilities, including image export for visual confirmation
- Integration of drawio CLI tools (validation and export)
- Best practices from diagramming UX for intelligent tool sequencing

# LLM-Driven drawio Diagram Editing – Tools and Architecture Design

**Overview:** We propose a comprehensive set of atomic tools and an architecture for an MCP server that enables fine-grained control of drawio (diagrams.net) diagrams by a conversational AI (LLM) with human oversight. The design emphasizes pure, functional tool implementations (for predictability and easy undo/redo), efficient handling of the drawio XML format, and real-time visual feedback after each operation. Below we present the toolset in three priority tiers, F#-style pseudocode for each tool, an undo/redo scheme, the image preview feedback loop, and integration details for the drawio CLI (for validation and export).

## Priority Tiers of Diagram Tools

We organize diagram-editing actions into three tiers by priority. Each **tool** represents a small, focused operation (an *atomic action*) that the LLM can invoke. These go far beyond basic shape creation, covering alignment, styling, layering, etc., to give the AI fine control. All tools operate on an internal **DiagramModel** (representing the drawio diagram’s XML structure ([Manually edit the XML source of your drawio diagram](https://www.drawio.com/doc/faq/diagram-source-edit#:~:text=The%20shapes%2C%20connectors%2C%20styles%2C%20and,diagram%2C%20and%20edit%20it%20directly))) and return a new updated model (no side effects). This makes them *LLM-friendly* – the AI can reason about one small change at a time, and humans can follow each step.

### Tier 1: Essential Creation & Editing Tools

These core tools cover creating basic elements, modifying them, and querying the diagram. They are the minimum needed to build and edit a diagram:

- **Add Shape (Vertex)** – Insert a new shape/node into the diagram. The tool takes a shape type (e.g. rectangle, ellipse, UML class box, etc.), position (x,y), size (width,height), and initial style (colors, etc.). It creates a new `<mxCell>` for the shape with a unique ID, `vertex="1"`, and adds it to the diagram’s XML root ([mxgraph - Format of drawio XML file? - Stack Overflow](https://stackoverflow.com/questions/59416025/format-of-draw-io-xml-file#:~:text=%3CmxGraphModel%3E%20%3Croot%3E%20%3CmxCell%20id%3D,rounded%3D0%3BwhiteSpace%3Dwrap%3Bhtml%3D1%3Bsha)). By default, the new shape’s parent is the current layer (e.g. layer “1” for the base layer). It can also set a text label if provided.  
  *Pseudocode:*  
  ```fsharp
  /// Add a new shape vertex to the diagram
  let addShape (diagram: DiagramModel, shapeType: string, pos: Point, size: Size, 
               style: Style, text: string option) : DiagramModel =
      // 1. Generate a new unique ID for the shape
      let id = diagram.NextId  
      // 2. Build style string (include shape type and provided style attributes)
      let baseShapeStyle = 
          match shapeType with
          | "rectangle" -> ""  // default shape is rectangle if not specified
          | other -> $"shape={other};"
      let styleStr = baseShapeStyle + (Style.toString style)
      // 3. Create an mxCell element for the shape
      let cell = {
          Id = id; 
          Value = text.GetValueOrDefault("");  // label text or empty
          Style = styleStr;
          Vertex = true; Edge = false;
          Parent = diagram.CurrentLayerId;
          Geometry = { X = pos.x; Y = pos.y; Width = size.width; Height = size.height; Relative = false }
      }
      // 4. Insert the new cell into diagram’s cell map and increment NextId
      { diagram with Cells = diagram.Cells.Add(id, Shape cell); NextId = id + 1 }
  ```  
  **Explanation:** This creates a new shape cell with given geometry and style. The `Style` may include keys like `fillColor`, `strokeColor`, etc., and possibly a `shape` key if a non-rectangle shape is desired (drawio uses style keys to determine specific shapes or icons). The new shape is placed at `pos` with given `size` and added to the diagram model.

- **Add Connector (Edge)** – Create a new connector (edge) linking two shapes. This adds an `<mxCell edge="1">` entry with `source` and `target` attributes referring to the IDs of the two shapes ([Edge labeling · Issue #29 · hbmartin/graphviz2drawio · GitHub](https://github.com/hbmartin/graphviz2drawio/issues/29#:~:text=parent%3D,1)). The tool can accept a connector style (e.g. straight, elbow, curved) and arrowhead settings. It computes a default path (drawio will route it orthogonally or straight by default).  
  *Pseudocode:*  
  ```fsharp
  /// Add a new connector edge between two vertices
  let addConnector (diagram: DiagramModel, sourceId: string, targetId: string, 
                    style: Style, text: string option) : DiagramModel =
      let id = diagram.NextId
      // Base edge style (e.g., default routing style and arrow)
      let baseEdgeStyle = "edgeStyle=orthogonalEdgeStyle;rounded=0;" 
      // (could also allow "straight" vs "orthogonal" etc. via style param)
      let arrowStyle = if style.HasArrow then "endArrow=block;endFill=1;" else ""
      let styleStr = baseEdgeStyle + arrowStyle + (Style.toString style)
      let edgeCell = {
          Id = id;
          Value = text.GetValueOrDefault("");
          Style = styleStr;
          Vertex = false; Edge = true;
          Source = sourceId; Target = targetId;
          Parent = diagram.CurrentLayerId;
          Geometry = { Relative = true;  // edges typically have relative geometry with optional waypoints
                       SourcePoint = None; TargetPoint = None; Waypoints = [] }
      }
      { diagram with Cells = diagram.Cells.Add(id, Edge edgeCell); NextId = id + 1 }
  ```  
  **Explanation:** This function links two existing shapes by their IDs. By default, it sets an orthogonal edge style (right-angle connectors) with an arrow at the end. The geometry is marked `relative="1"` (meaning the connector’s path is relative to source/target positions ([Edge labeling · Issue #29 · hbmartin/graphviz2drawio · GitHub](https://github.com/hbmartin/graphviz2drawio/issues/29#:~:text=parent%3D,1))) and no explicit waypoints, so it will auto-route. The LLM could specify different edge styles by adjusting the style parameter (e.g. straight lines vs. curved by including `curved=1` in style).

- **Delete Element** – Remove a shape or connector. This finds the `<mxCell>` by ID and removes it from the model. If the element is a group or has children (shapes grouped or connectors attached), we may also remove or reparent those accordingly. Deleting an edge simply removes that edge cell.  
  *Pseudocode:*  
  ```fsharp
  /// Delete a shape or connector (and any attached edges if cascade delete for shapes)
  let deleteElement (diagram: DiagramModel, elementId: string) : DiagramModel =
      // If it's a vertex with connected edges, remove those edges too (optional cascade behavior)
      let connectedIds = 
          match diagram.Cells.TryFind(elementId) with
          | Some (Shape _) -> 
                // find all edges where source or target is this shape
                diagram.Cells 
                |> Seq.choose (fun (id, cell) -> 
                       match cell with 
                       | Edge e when e.Source = elementId || e.Target = elementId -> Some id 
                       | _ -> None )
                |> Seq.toList
          | _ -> []
      // Remove the element and any connected edges from the cell map
      let newCells = (elementId :: connectedIds) |> List.fold (fun cells id -> cells.Remove(id)) diagram.Cells
      { diagram with Cells = newCells }
  ```  
  **Explanation:** We remove the target cell from the model. If it’s a shape, the code also gathers any connectors attached to it and removes them (so the diagram doesn’t retain dangling edges). This keeps the XML consistent (each removed `<mxCell>` will be gone). The resulting diagram model no longer contains that element.

- **Move Shape** – Change a shape’s position. The tool takes a shape ID and new coordinates (or a delta x,y). It updates the shape’s `<mxGeometry>` values for x and y ([Arrange shapes and connectors](https://www.drawio.com/doc/faq/arrange-tab#:~:text=Reposition%20a%20shape)). (Connectors usually move automatically with shapes since their geometry is relative.) This tool ensures efficient repositioning for large diagrams by just updating one entry in memory rather than scanning full XML.  
  *Pseudocode:*  
  ```fsharp
  /// Move a shape (vertex) to a new position
  let moveShape (diagram: DiagramModel, shapeId: string, newPos: Point) : DiagramModel =
      match diagram.Cells.TryFind(shapeId) with
      | Some (Shape shapeData) ->
          // Update the geometry coordinates
          let newGeom = { shapeData.Geometry with X = newPos.x; Y = newPos.y }
          let newShape = { shapeData with Geometry = newGeom }
          { diagram with Cells = diagram.Cells.Add(shapeId, Shape newShape) }
      | _ -> diagram  // if not found or not a shape, return unchanged
  ```  
  **Explanation:** The function locates the shape by ID and updates its X,Y position. Only the target shape’s data is changed. Because we treat the model immutably, we return a new DiagramModel with the updated cell entry. This corresponds to the user action of dragging a shape or typing new coordinates ([Arrange shapes and connectors](https://www.drawio.com/doc/faq/arrange-tab#:~:text=Reposition%20a%20shape)) (the top-left corner relative to the page’s origin ([Arrange shapes and connectors](https://www.drawio.com/doc/faq/arrange-tab#:~:text=Note%3A%20The%20exact%20position%20is,more%20than%20one%20printed%20page))).

- **Resize Shape** – Adjust a shape’s width and height. Similar to move, this finds the shape’s geometry and updates the width/height attributes ([Arrange shapes and connectors](https://www.drawio.com/doc/faq/arrange-tab#:~:text=Resize%20a%20shape)). We can also support an “autosize” mode that sets the shape’s size to fit its text (similar to drawio’s *Autosize* feature ([Arrange shapes and connectors](https://www.drawio.com/doc/faq/arrange-tab#:~:text=the%20shape%E2%80%99s%20proportions%20%20when,a%20new%20height%20or%20width))).  
  *Pseudocode:*  
  ```fsharp
  /// Resize a shape to specified dimensions (or autosize to its text if autosize = true)
  let resizeShape (diagram: DiagramModel, shapeId: string, newSize: Size, autosize: bool) : DiagramModel =
      match diagram.Cells.TryFind(shapeId) with
      | Some (Shape shapeData) ->
          let newGeom = 
              if autosize then 
                  // e.g., measure text and padding (not fully implemented in pseudocode)
                  let bbox = Text.measure shapeData.Value 
                  { shapeData.Geometry with Width = bbox.width; Height = bbox.height }
              else 
                  { shapeData.Geometry with Width = newSize.width; Height = newSize.height }
          let newShape = { shapeData with Geometry = newGeom }
          { diagram with Cells = diagram.Cells.Add(shapeId, Shape newShape) }
      | _ -> diagram
  ```  
  **Explanation:** If `autosize` is true, the function would calculate the bounding box needed for the shape’s label (perhaps using an internal text-measuring utility) and set the geometry accordingly ([Arrange shapes and connectors](https://www.drawio.com/doc/faq/arrange-tab#:~:text=the%20shape%E2%80%99s%20proportions%20%20when,a%20new%20height%20or%20width)). Otherwise, it directly sets the specified width/height. This corresponds to interactively dragging the shape’s corner or using the format panel to input dimensions ([Arrange shapes and connectors](https://www.drawio.com/doc/faq/arrange-tab#:~:text=Select%20a%20shape%2C%20then%20enter,the%20Width%20and%20Height%20fields)). 

- **Edit Label/Text** – Change the text label of a shape or connector. The tool finds the element and updates its `Value` (which drawio uses as the displayed text). It might also adjust the size if autosizing is on. For connectors, the `Value` is the edge label.  
  *Pseudocode:*  
  ```fsharp
  /// Set the text label of a shape or connector
  let setLabel (diagram: DiagramModel, elementId: string, newText: string) : DiagramModel =
      match diagram.Cells.TryFind(elementId) with
      | Some (Shape shapeData) ->
          let newShape = { shapeData with Value = newText }
          // Optionally autosize shape to new text
          let autoSizedShape = 
              if shapeData.Style.Contains("autosize=1") then 
                  // Recompute geometry to fit text
                  { newShape with Geometry = Text.autoSizeGeometry newText shapeData.Style newShape.Geometry }
              else newShape
          { diagram with Cells = diagram.Cells.Add(elementId, Shape autoSizedShape) }
      | Some (Edge edgeData) ->
          let newEdge = { edgeData with Value = newText }
          { diagram with Cells = diagram.Cells.Add(elementId, Edge newEdge) }
      | None -> diagram
  ```  
  **Explanation:** This handles both vertices and edges. A shape’s text might be stored in `mxCell@value` (if HTML labels are used, drawio sets `value` to a HTML snippet). Here we simply replace it. If the shape has an autosize style flag, we recalc its size. This allows the LLM to rename components (“rename the box to *Server*”, etc.) easily.

- **Basic Style Changes** – Tools to adjust common style attributes on shapes or connectors. We expose these as separate atomic tools so the LLM can say “make this box green” or “change font size to 14” directly. Under the hood, each tool will modify the style string of the target element. (The drawio style is a `key=value;key=value;...` string on the `<mxCell>` ([mxgraph - Format of drawio XML file? - Stack Overflow](https://stackoverflow.com/questions/59416025/format-of-draw-io-xml-file#:~:text=%3CmxCell%20id%3D%223%22%20value%3D%22Notes%22%20style%3D%22rounded%3D0%3BwhiteSpace%3Dwrap%3Bhtml%3D1%3Bsha%20dow%3D1%3BstrokeColor%3D,CCCCCC%3Balign%3Dleft%3BverticalAlign%3Dtop%3BspacingLeft%3D4%3Bmovable%3D0%3Bresiza%20ble%3D0%3Bconnectable%3D0%3BallowArrows%3D0%3Brotatable%3D0%3B%22%20vertex%3D%221%22%20parent%3D%222)).) For example:  
  - **Set Fill Color** – Change a shape’s fill color. (Updates the `fillColor` key in style ([mxgraph - Format of drawio XML file? - Stack Overflow](https://stackoverflow.com/questions/59416025/format-of-draw-io-xml-file#:~:text=%3CmxCell%20id%3D%223%22%20value%3D%22Notes%22%20style%3D%22rounded%3D0%3BwhiteSpace%3Dwrap%3Bhtml%3D1%3Bsha%20dow%3D1%3BstrokeColor%3D,CCCCCC%3Balign%3Dleft%3BverticalAlign%3Dtop%3BspacingLeft%3D4%3Bmovable%3D0%3Bresiza%20ble%3D0%3Bconnectable%3D0%3BallowArrows%3D0%3Brotatable%3D0%3B%22%20vertex%3D%221%22%20parent%3D%222)).)  
  - **Set Border Color** – Change the outline (stroke) color (`strokeColor` key).  
  - **Set Text Color** – Change font color (`fontColor`).  
  - **Set Font Size** – Adjust font size (`fontSize`).  
  - **Toggle Bold/Italic** – Set text style (e.g., `fontStyle=1` for bold in mxGraph style).  
  - **Set Line Style** (for connectors) – e.g., make an edge dashed or solid (`dashed=1/0`), or change thickness (`strokeWidth`).  
  - **Set Arrow Style** – Change connector arrowhead shape (e.g., `endArrow=block` for a filled arrow, `none` for no arrow).  

  Each such tool will locate the element’s style string, modify or add the relevant key, and update the model. We can also have a generic `setStyleAttribute` tool, but providing semantic aliases helps the AI.  
  *Pseudocode (example for fill color):*  
  ```fsharp
  /// Set fill color of a shape
  let setFillColor (diagram: DiagramModel, shapeId: string, colorHex: string) : DiagramModel =
      match diagram.Cells.TryFind(shapeId) with
      | Some (Shape shapeData) ->
          // Update or add fillColor in the style string
          let newStyle = Style.setKey shapeData.Style "fillColor" colorHex
          let newShape = { shapeData with Style = newStyle }
          { diagram with Cells = diagram.Cells.Add(shapeId, Shape newShape) }
      | _ -> diagram
  ```  
  **Explanation:** We use a helper to update the style string, ensuring we replace any existing fillColor. Similar functions (or one parameterized function) handle other style keys. For connectors, a similar approach applies (e.g., `setLineStyle` would update an edge’s style string).

- **Query Tools** – To let the LLM *inspect* the diagram, we include read-only tools. These do not modify state (so they might not need undo) but are crucial for reasoning on a large diagram. For example:  
  - **Find Elements by Text** – Search for shapes whose label text matches a query (exact or fuzzy). Returns the IDs or a list of matches.  
  - **Get Element Info** – Return properties of a given element (type, text, position, size, connections). E.g., the AI can call `getElementInfo("Server1")` to get its coordinates and use that to place another element relative to it.  
  - **List Neighbors** – Given a shape, list IDs of shapes directly connected to it by edges.  
  - **Get Diagram Size** – Perhaps the current extents of all shapes (to decide where to add new elements).  

  *Pseudocode (example find by text):*  
  ```fsharp
  /// Find shape IDs by label text (case-insensitive contains match)
  let findByLabel (diagram: DiagramModel, substring: string) : string list =
      diagram.Cells 
      |> Seq.choose (fun (id, cell) -> 
             match cell with 
             | Shape s when s.Value.ToLower().Contains(substring.ToLower()) -> Some id
             | _ -> None )
      |> Seq.toList
  ```  
  These querying capabilities make the system **introspectable** – the LLM can ask about the current state instead of guessing. This is important with large diagrams (e.g. 3000+ lines of XML with embedded images) because the AI may not hold the entire structure in its context at once. By using queries, it can retrieve needed details on demand (e.g., “what is the ID of the shape labeled *Database*?”, “where is it positioned?”).

### Tier 2: Advanced Editing & Layout Tools

These tools improve efficiency and formatting of diagrams. They are not strictly essential for creating a diagram, but they enable higher-quality outputs and easier editing. Each is still an atomic, focused action.

- **Align Shapes** – Given a selection of shapes, align them along a common axis or edge. Options include left-align, right-align, top, bottom, or center alignment (horizontal or vertical center) ([Arrange shapes and connectors](https://www.drawio.com/doc/faq/arrange-tab#:~:text=,shapes%20with%20an%20alternative%20spacing)). For example, *align left* sets all selected shapes’ `x` coordinate to the min `x` among them; *align middle* sets all their center Y to the average Y, etc.  
  *Pseudocode:*  
  ```fsharp
  /// Align a list of shapes to a specified alignment
  type Alignment = Left | Right | Top | Bottom | CenterX | CenterY
  let alignShapes (diagram: DiagramModel, shapeIds: string list, mode: Alignment) : DiagramModel =
      if shapeIds.IsEmpty then diagram else
      // get current geometries of all shapes
      let geoms = 
          shapeIds 
          |> List.choose (fun id -> 
                 match diagram.Cells.TryFind(id) with 
                 | Some (Shape s) -> Some s.Geometry 
                 | _ -> None)
      let alignedValue = 
          match mode with
          | Left   -> geoms |> List.minBy (fun g -> g.X) |> fun g -> g.X
          | Right  -> geoms |> List.maxBy (fun g -> g.X + g.Width) |> fun g -> g.X + g.Width
          | Top    -> geoms |> List.minBy (fun g -> g.Y) |> fun g -> g.Y
          | Bottom -> geoms |> List.maxBy (fun g -> g.Y + g.Height) |> fun g -> g.Y + g.Height
          | CenterX -> // vertical center align (same x-center)
              let avgCenterX = geoms |> List.averageBy (fun g -> g.X + g.Width/2.0)
              avgCenterX
          | CenterY -> // horizontal center align (same y-center)
              let avgCenterY = geoms |> List.averageBy (fun g -> g.Y + g.Height/2.0)
              avgCenterY
      // align each shape's position accordingly
      let updateShape s = 
          let newGeom = 
              match mode with 
              | Left | Right -> { s.Geometry with X = (if mode=Left then alignedValue else alignedValue - s.Geometry.Width) }
              | Top | Bottom -> { s.Geometry with Y = (if mode=Top then alignedValue else alignedValue - s.Geometry.Height) }
              | CenterX -> { s.Geometry with X = alignedValue - s.Geometry.Width/2.0 }
              | CenterY -> { s.Geometry with Y = alignedValue - s.Geometry.Height/2.0 }
          { s with Geometry = newGeom }
      let newCells = shapeIds |> List.fold (fun cells id ->
                        match cells.TryFind(id) with 
                        | Some (Shape s) -> cells.Add(id, Shape (updateShape s))
                        | _ -> cells ) diagram.Cells
      { diagram with Cells = newCells }
  ```  
  **Explanation:** The function computes an alignment target (e.g. leftmost X or average center) and then sets each shape’s geometry accordingly ([Arrange shapes and connectors](https://www.drawio.com/doc/faq/arrange-tab#:~:text=,shapes%20with%20an%20alternative%20spacing)). This replicates drawio’s *Arrange → Align* tools (e.g., *Align Left*, *Align Center*, etc.). It’s atomic from the AI’s perspective (all selected shapes move in one operation).

- **Distribute Shapes** – Evenly distribute spacing between a set of shapes horizontally or vertically ([Arrange shapes and connectors](https://www.drawio.com/doc/faq/arrange-tab#:~:text=,shapes%20with%20an%20alternative%20spacing)). This tool will sort the shapes by their position, then adjust their coordinates so that gaps between them are equal.  
  *Pseudocode:*  
  ```fsharp
  /// Distribute shapes evenly in horizontal or vertical direction
  type Distribution = Horizontal | Vertical
  let distributeShapes (diagram: DiagramModel, shapeIds: string list, mode: Distribution) : DiagramModel =
      if shapeIds.Length < 3 then diagram else
      // sort shapes by left or top coordinate
      let sorted = 
          shapeIds 
          |> List.choose (fun id -> diagram.Cells.TryFind(id) |> Option.bind (function Shape s -> Some(id, s) | _ -> None))
          |> List.sortBy (fun (_, s) -> if mode = Horizontal then s.Geometry.X else s.Geometry.Y)
      // calculate spacing
      let firstPos = if mode = Horizontal then (snd sorted.Head).Geometry.X else (snd sorted.Head).Geometry.Y
      let lastShape = snd sorted.[sorted.Length - 1]
      let lastPos = if mode = Horizontal then lastShape.Geometry.X else lastShape.Geometry.Y
      let totalSpace = lastPos - firstPos
      let gap = totalSpace / float(sorted.Length - 1)
      // move intermediate shapes to evenly spaced positions
      let newCells = 
          sorted 
          |> List.mapi (fun idx (id, s) ->
                 if idx = 0 || idx = sorted.Length-1 then (id, Shape s)  // first and last remain
                 else 
                     let newGeom =
                         if mode = Horizontal then { s.Geometry with X = firstPos + gap * float idx }
                         else { s.Geometry with Y = firstPos + gap * float idx }
                     let newShape = { s with Geometry = newGeom }
                     (id, Shape newShape))
          |> Map.ofList
      { diagram with Cells = diagram.Cells |> Map.addRange newCells }
  ```  
  **Explanation:** We only reposition the interior shapes, leaving the first and last in place (this matches typical distribution behavior where endpoints remain and others are spread between). The result is that the centers (or edges) of shapes are equidistant ([Arrange shapes and connectors](https://www.drawio.com/doc/faq/arrange-tab#:~:text=,shapes%20with%20an%20alternative%20spacing)). The AI could call this after roughly placing several boxes to tidy up spacing.

- **Group / Ungroup** – Create a group from multiple shapes, or ungroup them ([Arrange shapes and connectors](https://www.drawio.com/doc/faq/arrange-tab#:~:text=Group%20multiple%20shapes)). Grouping in drawio wraps shapes in a container `<mxCell>` with `style="group"` acting as a parent ([mxgraph - Format of drawio XML file? - Stack Overflow](https://stackoverflow.com/questions/59416025/format-of-draw-io-xml-file#:~:text=%3CmxCell%20id%3D,geometry)). The tool would create a new group cell, move the selected shapes under it (changing their parent in XML to the group’s ID), and possibly assign a common label or styling to the group. Ungroup does the reverse: remove the group container and promote its children to the group’s parent layer.  
  *Pseudocode:*  
  ```fsharp
  /// Group multiple shapes into a single group container
  let groupShapes (diagram: DiagramModel, shapeIds: string list) : DiagramModel =
      if shapeIds.IsEmpty then diagram else
      let newGroupId = diagram.NextId
      // Create group cell with no visible geometry (it will size to children or can be given a bounds)
      let groupCell = {
          Id = newGroupId; Value = ""; Style = "group;"; Vertex = true; Edge = false;
          Parent = diagram.CurrentLayerId;
          Geometry = { X=0.0; Y=0.0; Width=0.0; Height=0.0; Relative=false }
      }
      // Update each shape to have Parent = newGroupId
      let newCells = shapeIds |> List.fold (fun cells sid ->
                         match cells.TryFind(sid) with
                         | Some (Shape s) -> cells.Add(sid, Shape { s with Parent = newGroupId })
                         | _ -> cells)
                     (diagram.Cells.Add(newGroupId, Shape groupCell))
      { diagram with Cells = newCells; NextId = newGroupId + 1 }
  ```  
  **Explanation:** This simplistic grouping puts all selected shapes under a new parent. A more advanced implementation could also resize the group’s geometry to encompass children and possibly style it (e.g., a dashed outline). drawio’s UI provides a *Group* button ([Arrange shapes and connectors](https://www.drawio.com/doc/faq/arrange-tab#:~:text=Group%20multiple%20shapes)) which essentially does this. **Ungroup** would remove a given group ID: reassign its children’s parent to the group’s parent (usually layer), then remove the group cell.

- **Send to Front/Back (Layering)** – Change the z-order of shapes (within the same layer). *Bring to Front* or *Send to Back* moves a shape above or below others ([Arrange shapes and connectors](https://www.drawio.com/doc/faq/arrange-tab#:~:text=1,back%20via%20the%20Arrange%20tab)), and *Bring Forward* / *Send Backward* moves it one step in the stacking order ([Arrange shapes and connectors](https://www.drawio.com/doc/faq/arrange-tab#:~:text=Send%20shapes%20backwards%20or%20bring,one%20step%20at%20a%20time)). In XML, drawio preserves stacking by the order of cells in the file (later in the XML = front). Our model might maintain an ordering list or use the map insertion order. This tool would reorder the internal list or set a flag. Alternatively, we can utilize a drawio attribute: connectors and shapes have an `zOrder` style in some contexts, but simpler is to reorder the collection.  
  *Pseudocode:*  
  ```fsharp
  /// Change z-order of a shape relative to siblings in the same layer
  type OrderChange = ToFront | ToBack | Forward | Backward
  let reorderShape (diagram: DiagramModel, shapeId: string, action: OrderChange) : DiagramModel =
      // Only consider shapes on the same layer
      match diagram.Cells.TryFind(shapeId) with
      | Some (Shape s) ->
          let siblings = 
              diagram.Cells 
              |> Seq.filter (fun (_, cell) -> 
                     match cell with 
                     | Shape s2 when s2.Parent = s.Parent -> true | _ -> false)
              |> Seq.toList
          // Determine new ordering (e.g., move shapeId to end/start or +/- one position)
          let orderedIds = siblings |> List.map fst 
          let idx = orderedIds.IndexOf shapeId
          let newOrder =
              match action with
              | ToFront   -> orderedIds |> List.filter ((<>) shapeId) @ [shapeId]
              | ToBack    -> shapeId :: (orderedIds |> List.filter ((<>) shapeId))
              | Forward   -> 
                  if idx < orderedIds.Length - 1 then 
                      // swap with next
                      orderedIds.[idx] <- orderedIds.[idx+1]; orderedIds.[idx+1] <- shapeId
                  orderedIds
              | Backward  ->
                  if idx > 0 then 
                      orderedIds.[idx] <- orderedIds.[idx-1]; orderedIds.[idx-1] <- shapeId
                  orderedIds
          // Reconstruct diagram.Cells with shapes in newOrder for that layer
          let newCells = 
              newOrder 
              |> List.fold (fun (cells, orderIdx) id -> 
                     match cells.TryFind(id) with
                     | Some cell -> 
                         // we might rebuild a new map preserving this order 
                         // (in practice, an ordered list might be better than Map for z-order)
                         (cells, orderIdx + 1)
                     | None -> (cells, orderIdx + 1)
                     ) (diagram.Cells, 0)
                     |> fst
          { diagram with Cells = newCells }
      | _ -> diagram
  ```  
  **Explanation:** The pseudocode outlines the approach, though in practice we might manage an ordering structure outside of the cell map (since F# Map is not ordered). The idea is that *ToFront* puts the shape last in its layer’s list (on top of all others) ([Arrange shapes and connectors](https://www.drawio.com/doc/faq/arrange-tab#:~:text=1,back%20via%20the%20Arrange%20tab)), *ToBack* puts it first (bottom) ([Arrange shapes and connectors](https://www.drawio.com/doc/faq/arrange-tab#:~:text=1,back%20via%20the%20Arrange%20tab)), and the other two swap positions with adjacent elements ([Arrange shapes and connectors](https://www.drawio.com/doc/faq/arrange-tab#:~:text=Send%20shapes%20backwards%20or%20bring,one%20step%20at%20a%20time)). This corresponds to drawio’s *Arrange → To Front/Back* commands.

- **Rotate and Flip** – Rotate a shape by a given angle, or flip it horizontally/vertically. Rotation in drawio is stored in the style (e.g., `rotation=90`). Our tool will add/update the `rotation` style on the shape. Flipping horizontally or vertically can be achieved by toggling a flip flag or by applying a 180° rotation on one axis. In drawio, flipping a shape keeps the text orientation by default ([Arrange shapes and connectors](https://www.drawio.com/doc/faq/arrange-tab#:~:text=Flip%20a%20shape%3A%20Click%20Horizontal,its%20label%20text%20in%20place)) – they implement flip as a style transform that doesn’t affect label text. We can mimic this by setting style keys (e.g., `flipH=1` or `flipV=1` for horizontal/vertical flips) or by swapping geometry for connectors.  
  *Pseudocode:*  
  ```fsharp
  /// Rotate a shape by a specified angle (in degrees)
  let rotateShape (diagram: DiagramModel, shapeId: string, angle: float) : DiagramModel =
      match diagram.Cells.TryFind(shapeId) with
      | Some (Shape s) ->
          let newStyle = Style.setKey s.Style "rotation" (string angle)
          let newShape = { s with Style = newStyle }
          { diagram with Cells = diagram.Cells.Add(shapeId, Shape newShape) }
      | _ -> diagram

  /// Flip a shape horizontally or vertically
  type FlipDirection = Horizontal | Vertical
  let flipShape (diagram: DiagramModel, shapeId: string, dir: FlipDirection) : DiagramModel =
      match diagram.Cells.TryFind(shapeId) with
      | Some (Shape s) ->
          let key = if dir = Horizontal then "flipH" else "flipV"
          let newStyle = Style.setKey s.Style key "1"
          // Also ensure label isn't flipped: drawio does this automatically (text stays upright)
          let newShape = { s with Style = newStyle }
          { diagram with Cells = diagram.Cells.Add(shapeId, Shape newShape) }
      | _ -> diagram
  ```  
  **Explanation:** A `rotation` style will rotate the shape (and its label together, unless `rotation` is applied differently). drawio has an option to rotate shape without rotating the label ([Arrange shapes and connectors](https://www.drawio.com/doc/faq/arrange-tab#:~:text=,or%20without%20its%20label%20text)) – this nuance could be handled by setting a separate text direction if needed. Flips are done by style flags; for connectors, drawio allows flipping an edge and also a *reverse* which swaps source and target ([Arrange shapes and connectors](https://www.drawio.com/doc/faq/arrange-tab#:~:text=Image%3A%20Flip%20a%20non,its%20label%20text%20in%20place)). We can implement **Reverse Connector** as a similar tool (swap the `source` and `target` IDs of an edge, and perhaps adjust the waypoints or arrow direction if needed).

- **Reroute Connector** – For complex connectors, sometimes the routing gets messy or manual waypoints are set. drawio provides *Clear Waypoints* to reset an edge to a simple straight or orthogonal path ([Arrange shapes and connectors](https://www.drawio.com/doc/faq/arrange-tab#:~:text=Reset%20connector%20waypoints)). This tool would remove any intermediate waypoints from an edge’s geometry, essentially letting it auto-route anew. It might also reset `edgeStyle` to default.  
  *Pseudocode:*  
  ```fsharp
  /// Clear any custom waypoints on an edge, forcing a reroute
  let resetConnector (diagram: DiagramModel, edgeId: string) : DiagramModel =
      match diagram.Cells.TryFind(edgeId) with
      | Some (Edge e) ->
          let newGeom = { e.Geometry with Waypoints = [] }
          // Optionally also set a default edgeStyle if it was altered
          let newStyle = Style.setKey e.Style "edgeStyle" "orthogonalEdgeStyle"
          let newEdge = { e with Geometry = newGeom; Style = newStyle }
          { diagram with Cells = diagram.Cells.Add(edgeId, Edge newEdge) }
      | _ -> diagram
  ```  
  **Explanation:** This simulates selecting a connector and clicking "Clear Waypoints" ([Arrange shapes and connectors](https://www.drawio.com/doc/faq/arrange-tab#:~:text=Reset%20connector%20waypoints)). The connector will then take the shortest route given the current positions of shapes. If needed, we could also provide a tool to add a specific waypoint or to change the routing style (e.g., to *entity-relationship* style links or elbows).

- **Insert Image** – Import an image (PNG/SVG) into the diagram as a shape. The tool will create a new vertex with a style `shape=image;image=<data URL>;aspect=fixed;...`. The image data (possibly base64-encoded) is embedded in the style ([srl-netbox-demo/srl_netbox_dark.drawio at main · srl-labs/srl-netbox-demo · GitHub](https://github.com/srl-labs/srl-netbox-demo/blob/main/srl_netbox_dark.drawio#:~:text=%3Cobject%20label%3D%22lab01)). For example, a style may contain `image=data:image/png;base64,<...>` which can be very long. The tool might take a URL or raw image bytes; if URL, it can either link or fetch and embed. **Note:** Large base64 strings make the XML huge (the user’s context of 3000+ lines likely stems from this). We ensure to handle it as just another string in the style (no special parsing needed beyond storing it).  
  *Pseudocode:*  
  ```fsharp
  /// Add an image shape at position with given size. `dataUri` is a "data:image/..." URL or external URL.
  let addImage (diagram: DiagramModel, dataUri: string, pos: Point, size: Size) : DiagramModel =
      let id = diagram.NextId
      let styleStr = $"shape=image;aspect=fixed;imageAspect=0;image={dataUri};"
      let cell = {
          Id = id; Value = ""; Style = styleStr;
          Vertex = true; Edge = false; Parent = diagram.CurrentLayerId;
          Geometry = { X = pos.x; Y = pos.y; Width = size.width; Height = size.height; Relative = false }
      }
      { diagram with Cells = diagram.Cells.Add(id, Shape cell); NextId = id + 1 }
  ```  
  **Explanation:** This is similar to **Add Shape** but specifically configures the style for an image. We use `aspect=fixed` to maintain the image’s aspect ratio when resizing, and include the image data. The LLM can use this to insert icons, logos, or any pictures. (If the image should not be fully embedded, one could store a link and have drawio fetch it, but typically a data URI is used for portability.)

- **Layer Management** – Tools to handle multiple layers in the diagram. drawio supports layers (each layer is essentially a top-level group with a name). Tools include: **New Layer** (creates a new empty layer), **Switch Layer** (change current editing layer for subsequent operations), **Rename Layer**, **Delete Layer**, and **Toggle Layer Visibility/Lock**. In the XML, layers are represented by `<mxCell>` entries that are children of the root (parent id "0"), usually with an attribute or style indicating it’s a layer. For instance, in an uncompressed `.drawio` file, layer cells appear with parent="0" and often have a "label" as the layer name. In our model, we can mark certain cells as layers and keep track of current layer ID in the DiagramModel.  
  *Pseudocode (new layer & switch layer):*  
  ```fsharp
  /// Create a new layer (returns updated model with that layer as current)
  let addLayer (diagram: DiagramModel, layerName: string) : DiagramModel =
      let id = diagram.NextId
      let layerCell = {
          Id = id; Value = layerName; Style = ""; Vertex = false; Edge = false;
          Parent = "0";  // top-level
          Geometry = { X=0.0; Y=0.0; Width=0.0; Height=0.0; Relative=false }
      }
      { diagram with Cells = diagram.Cells.Add(id, Shape layerCell); NextId = id + 1; CurrentLayerId = id }

  /// Switch current editing layer by name (for convenience)
  let switchLayer (diagram: DiagramModel, layerName: string) : DiagramModel =
      // find layer cell with given name
      let layerEntry = diagram.Cells |> Seq.tryFind (fun (_, cell) -> 
                          match cell with 
                          | Shape s when s.Parent = "0" && s.Value = layerName -> true 
                          | _ -> false)
      match layerEntry with
      | Some (id, _) -> { diagram with CurrentLayerId = id }
      | None -> diagram  // no change if not found
  ```  
  **Explanation:** *Add Layer* creates a new layer cell at top level (parent "0"). We leave its style empty (drawio might use a placeholder style or attribute to mark layers; since layers are just containers, style isn’t critical). We then set this new layer as the current layer, meaning subsequent shape additions will go into this layer. *Switch Layer* finds a layer by name and updates `CurrentLayerId`. Other possible tools: **removeLayer** (reassign all shapes in that layer to another or delete them, then remove layer cell), **setLayerVisibility** (we could add a style flag or an internal property; in drawio, layers have a visible flag in the file’s JSON section if using `.drawio` compressed format, but for our purposes we can manage it outside of XML or via a convention).

### Tier 3: Specialized & Refinement Tools

These tools handle finer details, rarely-used operations, or advanced editing. They enhance the robustness and completeness of the system:

- **Change Shape Type** – Transform an existing shape to a different shape (e.g., make a rectangle into an ellipse or into a predefined icon). This could be done by replacing the shape’s style keys related to shape (for built-ins, set `ellipse` or `triangle` etc., or for library shapes, use the appropriate `shape=<libraryKey>`). Essentially, it’s like deleting one shape and adding another with same text and position, but doing it in-place. Useful for quickly swapping symbols.  
- **Adjust Connector Waypoint** – Manually add or move a waypoint on a connector. This would update the edge’s geometry by inserting an `<mxPoint x="..." y="..." />` in the `<mxGeometry>` as a waypoint. The tool could take an edge ID and a position and either add a new waypoint or move an existing one to that coordinate. This is advanced as it requires calculating relative positions along the edge.  
- **Edit Metadata / Tags** – Attach custom data to a shape (drawio allows user-defined metadata on shapes ([Manually edit the XML source of your drawio diagram](https://www.drawio.com/doc/faq/diagram-source-edit#:~:text=Image%3A%20Click%20Extras%20,how%20you%20would%20%2020))). This might involve adding a JSON or XML snippet in the cell’s `data` section or as an attribute. A tool could set a metadata key-value for a shape (e.g., tagging a server shape with an IP address or asset ID). This makes the diagram more semantically rich for the AI.  
- **Change Diagram Background** – Set a background color or image for the whole diagram. In the XML, this is stored in `<mxGraphModel background="#FFFFFF" ...>` attribute for color ([srl-netbox-demo/srl_netbox_dark.drawio at main · srl-labs/srl-netbox-demo · GitHub](https://github.com/srl-labs/srl-netbox-demo/blob/main/srl_netbox_dark.drawio#:~:text=%3CmxGraphModel%20dx%3D,4D5766%22%20math%3D%220%22%20shadow%3D%220)), or via an image in a special way. A tool can simply set that attribute (our model would have a property for global settings). Similarly, **Toggle Grid Visibility/Snap** – The grid snapping is controlled by a flag in the model (`grid="1"` or `0` in `<mxGraphModel>` ([srl-netbox-demo/srl_netbox_dark.drawio at main · srl-labs/srl-netbox-demo · GitHub](https://github.com/srl-labs/srl-netbox-demo/blob/main/srl_netbox_dark.drawio#:~:text=%3CmxGraphModel%20dx%3D,4D5766%22%20math%3D%220%22%20shadow%3D%220))). A tool can enable/disable the grid which affects whether shapes snap to grid lines when moved ([Blog - Snap to grid and other helpful alignment tools in drawio](https://www.drawio.com/blog/snap-to-grid#:~:text=When%20Grid%20is%20enabled%2C%20shapes,the%20grid%20or%20page%20center)) ([Blog - Snap to grid and other helpful alignment tools in drawio](https://www.drawio.com/blog/snap-to-grid#:~:text=Disable%20snapping%20temporarily%3A%20Hold%20down,from%20snapping%20to%20the%20grid)). For example, `enableGrid(diagram, true)` would set `diagram.GridEnabled = true` which results in `grid="1"` on save.  
- **Auto-Layout** – Invoke an automatic layout algorithm on all or part of the diagram. This is a high-level tool that could rearrange shapes (e.g., a tree or force-directed layout). Diagrams.net doesn’t have a one-click auto-layout for arbitrary diagrams (except arranging tree structures), so this might involve integrating an external layout library or using the built-in Graphviz integration if any. This is quite advanced and would likely be a composite action (not purely a single atomic tool, but worth noting as a possible extension).  
- **Copy & Paste Style** – Apply the style of one shape to another. This tool reads all style attributes from a source shape and applies them to target shape(s) (excluding geometry and text). drawio has “Copy Style / Paste Style” which does exactly this ([Arrange shapes and connectors](https://www.drawio.com/doc/faq/arrange-tab#:~:text=Copy%20and%20paste%20shape%20sizes%3A,size%2C%20and%20click%20Paste%20Size)). This helps achieve consistent look without manually setting each property.  
- **Precisely Nudge Position** – Move a shape by a small increment, simulating arrow-key nudges (which move by 1 pixel, or  grid size when holding Shift ([Blog - Snap to grid and other helpful alignment tools in drawio](https://www.drawio.com/blog/snap-to-grid#:~:text=Move%20by%20grid%20increments%3A%20Once,move%20it%20in%20that%20direction))). This is essentially a variant of Move Shape but could be convenient if the AI needs to fine-tune placement.  
- **Undo/Redo** – While undo/redo is handled at the architecture level (see below), we can also expose them as tools the AI could invoke explicitly if needed (e.g., an AI could decide to “undo” a mistaken action on its own). Calling the `undo` tool would revert the diagram to the previous state, and `redo` would reapply an undone action if available. These tools interact with the global history mechanism described next.

## Undo/Redo Architecture Design

To support **undo/redo**, the MCP server maintains an immutable history of diagram states. Each editing tool, being a pure function, produces a new **DiagramModel** from the old one. We do not mutate the model in place; instead, we keep a stack (or list) of past states (the *undo stack*) and a stack for redo. This follows the classic **Memento** pattern ([Ever wondered how the undo/redo functionality works in applications like Notepad or how games… | by Abdul Samad | Medium](https://medium.com/@cragzo7/ever-wondered-how-the-undo-redo-functionality-works-in-applications-like-notepad-or-how-games-56794f661ff4#:~:text=Undo%20Logic%3A)) ([Ever wondered how the undo/redo functionality works in applications like Notepad or how games… | by Abdul Samad | Medium](https://medium.com/@cragzo7/ever-wondered-how-the-undo-redo-functionality-works-in-applications-like-notepad-or-how-games-56794f661ff4#:~:text=1,you%20previously%20undid%20an%20action)):

- When a tool is applied, we push the current state onto the undo stack (as a memento) before moving to the new state. The redo stack is cleared (because a new action invalidates future redoing). The new state becomes the “current” state.
- If an **Undo** command is issued (by the user or AI), we pop the last state from the undo stack and make it current (reverting the last action). Simultaneously, we push the state we just left onto the redo stack ([Ever wondered how the undo/redo functionality works in applications like Notepad or how games… | by Abdul Samad | Medium](https://medium.com/@cragzo7/ever-wondered-how-the-undo-redo-functionality-works-in-applications-like-notepad-or-how-games-56794f661ff4#:~:text=1,back%20to%20its%20previous%20state)). This allows Redo later.
- If **Redo** is commanded, we pop from the redo stack back to the current state, and push the state we left onto the undo stack ([Ever wondered how the undo/redo functionality works in applications like Notepad or how games… | by Abdul Samad | Medium](https://medium.com/@cragzo7/ever-wondered-how-the-undo-redo-functionality-works-in-applications-like-notepad-or-how-games-56794f661ff4#:~:text=Redo%20Logic%3A)).

**Immutable State & Snapshots:** Because the DiagramModel is persistent (previous states remain intact when we create a new one), we can store whole snapshots of the diagram efficiently. F#’s immutable data structures (or .NET’s in-memory sharing) mean that unchanged parts of the diagram (like those 3000 lines of XML where only a few lines change per action) can be structurally shared between states ([elm-undo-redo 2.0.0 - Elm Packages](https://package.elm-lang.org/packages/TheSeamau5/elm-undo-redo/latest/#:~:text=Given%20immutability%20lets%20you%20do,The%20library%20is)). Thus, each snapshot is not a completely separate heavy copy – it reuses most of the data except the pieces that changed. This gives us simplicity (each history entry is just a DiagramModel instance) and acceptable memory use. In practice, even if we duplicated the XML text for each step, it’s usually fine for moderate history lengths, but structural sharing makes it compact ([elm-undo-redo 2.0.0 - Elm Packages](https://package.elm-lang.org/packages/TheSeamau5/elm-undo-redo/latest/#:~:text=Given%20immutability%20lets%20you%20do,The%20library%20is)).

**Snapshots vs. Deltas:** Another approach considered is storing only *deltas* (the specific change made). For example, a delta could be “shape X moved from (10,10) to (20,10)”. The undo operation would then apply the inverse delta. While delta storage can save space, it complicates the logic (each tool must produce an invertible command). Given modern memory and the benefits of easier state management, we prefer snapshotting each state. It simplifies undo/redo: restoring a snapshot is O(1) to swap a reference, versus applying an inverse delta which is prone to bugs. Moreover, since our model is relatively lightweight (a few hundred KB of XML for large diagrams), snapshotting is acceptable. We can always impose a history limit or compress older states if needed.

Each tool function itself does not need to know about undo/redo – the server’s control flow handles pushing states. For example, a pseudo-implementation in the server might be:  

```fsharp
type EditorState = { 
    current: DiagramModel
    undoStack: DiagramModel list
    redoStack: DiagramModel list
}

let applyTool (state: EditorState, toolFunc: DiagramModel -> DiagramModel) =
    // push current state to undo stack
    let newUndo = state.current :: state.undoStack
    // clear redo stack (new action)
    let newRedo = []
    // get new state
    let newDiagram = toolFunc state.current
    { current = newDiagram; undoStack = newUndo; redoStack = newRedo }

let undo (state: EditorState) =
    match state.undoStack with
    | prev::rest ->
        let newRedo = state.current :: state.redoStack
        { current = prev; undoStack = rest; redoStack = newRedo }
    | [] -> state  // nothing to undo

let redo (state: EditorState) =
    match state.redoStack with
    | next::rest ->
        let newUndo = state.current :: state.undoStack
        { current = next; undoStack = newUndo; redoStack = rest }
    | [] -> state
```  

This demonstrates the caretaker logic using two stacks ([Ever wondered how the undo/redo functionality works in applications like Notepad or how games… | by Abdul Samad | Medium](https://medium.com/@cragzo7/ever-wondered-how-the-undo-redo-functionality-works-in-applications-like-notepad-or-how-games-56794f661ff4#:~:text=The%20undo%2Fredo%20functionality%20in%20the,Here%E2%80%99s%20a%20simple%20explanation)). The **EditorState** holds the current diagram and the history stacks. Every time a tool is invoked via the server, the server wraps the call with this logic. Notably, because the tools are pure and don’t internally modify global state, we don’t risk half-applied changes or inconsistent history.

We ensure that *undoable operations* are all the editing tools. Query tools don’t alter state (so they don’t push history), and the Preview/Export tools (described below) also don’t alter the state – they just read it.

For large diagrams with images, one optimization: store images in a separate structure and have the model reference them (to avoid duplicating large base64 strings in each snapshot). For example, if the user places a large image and then moves it 5 times, a naive snapshot would copy that base64 5 times. We can mitigate this by storing images in a dictionary (ID -> data) and let the cell’s style refer to the image ID or a token in style. But this adds complexity to the model. Since structural sharing will actually reuse the same style string in memory for the image shape if unchanged, it might not be a big issue. We mention this as a possible enhancement if memory profiling shows a bottleneck.

With the above design, implementing **Undo** and **Redo** as user-visible tools is straightforward: they call the `undo` or `redo` function on the EditorState. This can be exposed to the LLM, but more importantly, it’s exposed to the human supervisor (e.g., a user clicks “Undo” in the interface, or the AI suggests an undo and the user confirms). The architecture cleanly supports unlimited undo/redo and even branching (if we wanted to maintain multiple edit branches, though that’s out of scope).

## Real-Time Visual Feedback Loop (Preview Mechanism)

To foster a tight **feedback loop**, the server will produce an image preview of the diagram after each transformation. The goal is that whenever the LLM applies a tool, the human overseer (and potentially the LLM itself, if it can consume images) gets immediate visual confirmation. Here’s how we achieve this:

1. **Auto-Export on Change:** After any tool that modifies the diagram (add/move/edit/etc.), the MCP server automatically generates a new image of the diagram. This can be done by calling the drawio command-line interface to export the current diagram model to a PNG. The image is then sent to the client (e.g., shown in VSCode or in the chat UI as an embedded image). This happens in near-real-time, allowing the user to see the effect of each step. The LLM, in turn, can be programmed to await or examine the image (some advanced setups might do image analysis, though typically the human will do that part).

2. **On-Demand Previews:** While automatic per-step previews are useful, we can also allow configuration. For example, if the user or AI knows a sequence of many small changes is needed, it might be inefficient to render each intermediate state. The system could be configured to only preview every *n* steps or only when explicitly requested. We can expose a **Render Preview** tool that triggers image generation on demand. By default, though, we assume it runs each time for maximum transparency.

3. **Preview Configuration Options:** We leverage drawio’s export options to tailor the preview:
   - **Format:** PNG is the default for simplicity (and it can be embedded directly in chat or UI), but we could allow SVG for a scalable preview or PDF for high quality. The CLI supports `-f png|svg|jpg|pdf|vsdx` among others ([How I use drawio at the command line | Tom Donohue](https://tomd.xyz/how-i-use-drawio/#:~:text=,set%20transparent%20background%20for%20PNG)).
   - **Size/Scale:** For a quick preview, a smaller size or scale can be used to speed up generation. For instance, if the diagram is very large, we might export a PNG scaled down to fit a certain pixel width. The CLI allows `--scale` or explicit `--width/--height` ([How I use drawio at the command line | Tom Donohue](https://tomd.xyz/how-i-use-drawio/#:~:text=diagram%20%28default%3A%200%29%20,crops%20PDF%20to%20diagram%20size)). We can configure a default (say scale 1.0 for 100% size, or width=800px for chat). The user could request a higher resolution export at any time.
   - **Transparent vs. White background:** By default we might use a white background for clarity. If the diagram has a custom background color or image, we’ll export with that. The CLI’s `--transparent` flag can produce a PNG with transparency ([How I use drawio at the command line | Tom Donohue](https://tomd.xyz/how-i-use-drawio/#:~:text=,the%20generated%20image%2Fpdf%20into%20the)), which might be useful if the image is going into a dark-mode chat, etc.
   - **Bounding Box vs. Page:** drawio normally exports the content’s bounding box. If the user has a multi-page diagram, by default the first page is exported unless specified ([How I use drawio at the command line | Tom Donohue](https://tomd.xyz/how-i-use-drawio/#:~:text=,display%20help%20for%20command)). We should ensure to export the active page (if layering is used as pages). We can also use the `--crop` option for PDF to crop to content ([How I use drawio at the command line | Tom Donohue](https://tomd.xyz/how-i-use-drawio/#:~:text=,is%20an%20image%2C%20the%20first)), though for images it automatically crops.
   - **Embed Diagram Data:** For previews, we likely don’t need this, but drawio CLI can embed the diagram XML into PNGs (`--embed-diagram` for PNG) ([How I use drawio at the command line | Tom Donohue](https://tomd.xyz/how-i-use-drawio/#:~:text=,the%20generated%20image%2Fpdf%20into%20the)). This could be interesting: it means the preview PNG itself contains the XML, so if something goes wrong we could even recover the state from the image. However, since we have the state in memory, this is optional. It might be more relevant for final exports that are shared.

The **visual feedback loop** thus works as follows: The LLM issues a command (tool), the server applies it and updates the state, then calls the CLI to generate `diagram.png`, and sends this image back to the user interface. The human user sees the updated diagram immediately after each step, and can correct or guide the LLM if the result is not as intended. This satisfies the goal of human supervision: the human doesn’t have to trust the LLM blindly – they get to see a picture of each incremental change.

From the LLM’s perspective, if it has vision capabilities (like some multimodal models) or if the interface allows the AI to get the image description, it could also adjust its plan based on the image. But even if not, the human in the loop provides that verification.

In terms of **implementation**, after each state change we have a choice:
- **Option A:** Save the current diagram model to a `.drawio` file (which is essentially an XML, possibly compressed) and then call the external drawio CLI on that file to produce PNG.
- **Option B:** Pipe the diagram data directly to the CLI if supported (the CLI might not accept stdin, so likely we use files).
- **Option C:** Use an in-memory library or headless renderer. Since drawio is an Electron app, using the CLI (which essentially drives that headless) is easiest. There are also third-party tools (like the `drawio-export` using Puppeteer ([drawio-export-puppeteer - Yarn Classic](https://classic.yarnpkg.com/en/package/drawio-export-puppeteer#:~:text=drawio,We%20use))) but the official CLI is sufficient.

We will likely do Option A: write a temp file `current.drawio`, call CLI, get `out.png`. This happens behind the scenes for each step.

**Performance:** The drawio CLI is reasonably fast for typical diagrams, but if the diagram is extremely complex, rendering on each step could become a bottleneck. If performance becomes an issue, one could implement a simpler preview (like a canvas rendered via a lightweight engine or only render the changed part). However, given the broad scope, we stick to using drawio’s robust rendering to ensure accuracy. The user can always throttle the preview frequency if needed.

## Integration with drawio CLI (Validation & Export)

The drawio Desktop application provides a command-line interface that we harness for two main purposes: **validating the diagram XML** and **exporting to various formats**. We outline how the MCP server will use these capabilities and how to invoke them from F#/C#.

### CLI Capabilities for Export and Validation

**Exporting Images and More:** The CLI can export a diagram file to many formats by using the `--export` (or `-x`) flag with format options. Supported output formats include PNG, JPEG, SVG, PDF, and even VSDX (Visio) ([How I use drawio at the command line | Tom Donohue](https://tomd.xyz/how-i-use-drawio/#:~:text=,set%20transparent%20background%20for%20PNG)). For example, one can run:

```
drawio --export --format png --output diagram.png diagram.drawio
``` 

This will open `diagram.drawio` and write out `diagram.png`. The CLI supports additional options to control the export:
- `--scale <factor>`: Scale the output image by a factor (e.g., 2.5 = 250% for high DPI) ([How I use drawio at the command line | Tom Donohue](https://tomd.xyz/how-i-use-drawio/#:~:text=On%20Mac%20,and%20output%20to%20PNG)) ([How I use drawio at the command line | Tom Donohue](https://tomd.xyz/how-i-use-drawio/#:~:text=format%20only%29%20,scales%20the%20diagram%20size)).
- `--width <px>` / `--height <px>`: Fit the image to a specific width/height (preserving aspect ratio) ([How I use drawio at the command line | Tom Donohue](https://tomd.xyz/how-i-use-drawio/#:~:text=diagram%20%28default%3A%200%29%20,crops%20PDF%20to%20diagram%20size)).
- `--transparent`: For PNG exports, makes the background transparent instead of white ([How I use drawio at the command line | Tom Donohue](https://tomd.xyz/how-i-use-drawio/#:~:text=,the%20generated%20image%2Fpdf%20into%20the)).
- `--border <pixels>`: Add a border around the diagram of given size ([How I use drawio at the command line | Tom Donohue](https://tomd.xyz/how-i-use-drawio/#:~:text=format%20only%29%20,scales%20the%20diagram%20size)).
- `--embed-diagram`: If exporting PNG, embeds the diagram XML inside the PNG (as zTXt chunk) ([How I use drawio at the command line | Tom Donohue](https://tomd.xyz/how-i-use-drawio/#:~:text=,the%20generated%20image%2Fpdf%20into%20the)).
- `--page-index N` or `--all-pages`: For multi-page diagrams, choose specific page or all pages (all-pages works for PDF) ([How I use drawio at the command line | Tom Donohue](https://tomd.xyz/how-i-use-drawio/#:~:text=,display%20help%20for%20command)).
- (Standard options like `--output` to set output path, `--recursive` for batch processing multiple files, etc., as shown in the CLI help ([How I use drawio at the command line | Tom Donohue](https://tomd.xyz/how-i-use-drawio/#:~:text=,file%20type%20is)).)

**Validation:** The CLI does not have an explicit *"validate diagram"* command that checks the diagram for errors. However, we can use it implicitly:
- Running an export is itself a validation – if the diagram XML is malformed or has errors, the CLI will likely fail or warn. The `--check` option (`-k`) in CLI help is a bit misleading: it doesn’t validate content, it just prevents overwriting files ([How I use drawio at the command line | Tom Donohue](https://tomd.xyz/how-i-use-drawio/#:~:text=Options%3A%20,based%20on%20the%20given%20options)). So for content validation, our approach is either:
  - Use the CLI to export to an XML or SVG and see if it succeeds. For instance, `drawio -x --format xml --output test.xml diagram.drawio`. This will produce a pretty-printed XML. If the internal XML had issues, the CLI might not output correctly. In practice, drawio is tolerant of its own format, so true “validation errors” are rare unless our tool introduced something nonsensical (like a reference to a missing parent). Such cases would be logic bugs in our server, which we can catch with internal checks.
  - Alternatively, load the XML into an mxGraph model in memory (using the JavaScript library or a .NET port if available) to see if any exceptions are thrown. However, integrating the JS library is complex; instead, one could run a headless instance of drawio to open the file and see if it reports errors. This is probably overkill.

In summary, we assume that if our pure functions maintain consistency (which we ensure by design), explicit validation via CLI is not usually needed. We can always run a quick export to XML to normalize the file (drawio will output a standardized XML) and ensure it’s readable ([How I use drawio at the command line - Tom Donohue](https://tomd.xyz/how-i-use-drawio/#:~:text=script)). Our server might do this as a final step or periodically for safety.

**drawio CLI commands summary:**

- Create an empty file: `drawio --create --output newfile.drawio` – this will create a new blank diagram file if none exists ([How I use drawio at the command line | Tom Donohue](https://tomd.xyz/how-i-use-drawio/#:~:text=,If)). We could use this at initialization (though we can also just start with a known empty `<mxGraphModel>` template).
- Export diagram: `drawio -x -f png -o out.png in.drawio` (with variations as needed) ([How I use drawio at the command line | Tom Donohue](https://tomd.xyz/how-i-use-drawio/#:~:text=On%20Mac%20,and%20output%20to%20PNG)).
- Convert formats: You can input an SVG that has embedded XML and output a PNG, etc. (The CLI will read the embedded diagram in an SVG if the file has one, which is a nifty feature).
- *Check file exists*: `--check` prevents overwriting output ([How I use drawio at the command line | Tom Donohue](https://tomd.xyz/how-i-use-drawio/#:~:text=Options%3A%20,based%20on%20the%20given%20options)) – we might not use this in our context.

### Calling CLI from F#/C#

To use these CLI functions in our F# server, we will call an external process. In .NET, this is done via `System.Diagnostics.Process`. We will prepare the diagram data (by writing the `.drawio` file) and then invoke the CLI with appropriate arguments. The process will run the electron-headless export and exit. Here is a simplified F# example of exporting the current diagram to PNG:

```fsharp
open System.Diagnostics

let exportDiagramToPng (diagramPath:string, pngOutputPath:string, scale: float) =
    // Prepare the process start info for drawio CLI
    let psi = ProcessStartInfo()
    psi.FileName <- "drawio"            // assuming drawio is in PATH; otherwise specify full path
    psi.ArgumentList.Add("--export")    // -x
    psi.ArgumentList.Add("--format")
    psi.ArgumentList.Add("png")
    psi.ArgumentList.Add("--scale")
    psi.ArgumentList.Add(scale.ToString(System.Globalization.CultureInfo.InvariantCulture))
    psi.ArgumentList.Add("--output")
    psi.ArgumentList.Add(pngOutputPath)
    psi.ArgumentList.Add(diagramPath)
    psi.UseShellExecute <- false
    psi.CreateNoWindow <- true
    psi.RedirectStandardOutput <- true
    psi.RedirectStandardError <- true

    use proc = Process.Start(psi)
    proc.WaitForExit()
    let exitCode = proc.ExitCode
    if exitCode <> 0 then 
        let err = proc.StandardError.ReadToEnd()
        printfn "drawio CLI error (code %d): %s" exitCode err
```

This code builds the command `drawio --export --format png --scale <scale> --output <pngOutputPath> <diagramPath>`, runs it hidden (no window), waits for completion, and checks for errors. In C#, the equivalent would use `ProcessStartInfo` similarly or the `System.Management.Automation` if desired. We can adapt the arguments depending on what we need (for PDF export, change format; for multi-page diagrams, maybe loop through pages or use `--page-index`).

For *validation* via CLI, we could do something like:

```fsharp
let validateDiagram (diagramPath:string) =
    let psi = ProcessStartInfo("drawio", $"--export --format xml --output temp.xml {diagramPath}")
    psi.UseShellExecute <- false
    psi.CreateNoWindow <- true
    use proc = Process.Start(psi)
    proc.WaitForExit()
    proc.ExitCode = 0
```

If this returns true (exit code 0), it means drawio was able to parse and export the diagram to XML (which should be essentially a no-op conversion if the input was .drawio). If false, then something was wrong. We might not need this in normal operation, but it’s an option.

**Using the CLI for final outputs:** At the end of an editing session, the user might want to save the diagram or export it in a specific format (Visio, PDF, etc.). The LLM could have a high-level tool like **ExportDiagram(format, path)** which under the hood calls the CLI similarly to produce the requested file. For example, `ExportDiagram("vsdx", "NetworkDiagram.vsdx")` would run `drawio -x -f vsdx -o NetworkDiagram.vsdx current.drawio`. As per the CLI help, VSDX is supported as an output ([How I use drawio at the command line | Tom Donohue](https://tomd.xyz/how-i-use-drawio/#:~:text=,set%20transparent%20background%20for%20PNG)).

**Error Handling:** The CLI might output warnings or errors to stderr. For instance, if an image resource is missing or a font is not found. We will capture those and surface them to the user or logs. Setting `DRAWIO_CLI_SUPPRESS_WARNINGS=true` as mentioned in the third-party tool ([GitHub - rlespinasse/drawio-cli: CLI for drawio](https://github.com/rlespinasse/drawio-cli#:~:text=Tip)) is possible, but likely we want to see warnings in development.

Finally, we ensure that the CLI binary is available in the environment where the server runs. This might involve shipping the drawio desktop app with the server or using a container (the Docker image `jgraph/drawio` exists ([jgraph/drawio - Docker Image](https://hub.docker.com/r/jgraph/drawio#:~:text=jgraph%2Fdrawio%20,as%20an%20ER%20diagram%20tool)), as well as third-party headless images). In a .NET context, we might bundle the drawio app or instruct users to install it. Once installed, calling it as shown is straightforward.

## Conclusion

With the above toolset and architecture, we create a highly **composable** system for diagram manipulation. The LLM can chain together many small operations (e.g., “add a VM shape, label it X, connect it to Y, align it, color it red”) with minimal ambiguity. Each tool is *pure* and well-defined, making it easier to debug and trust each step. The human overseer can watch the diagram evolve via images and intervene as needed. 

Moreover, because the internal state is an XML-based model, it’s fully introspectable and queryable – the AI can answer questions about the diagram by using query tools (which is analogous to a user scanning the diagram). The integration with drawio’s proven rendering ensures that what the AI produces can always be visualized and exported in standard formats. 

By prioritizing atomic actions and a solid undo/redo foundation, we allow the AI to explore creative solutions (it can try something, undo if it didn’t work, and so on) under supervision. This design thus enables a powerful conversational diagramming assistant that leverages drawio’s capabilities to the fullest while keeping the human in control of the creative process.

**Sources:**

- drawio (diagrams.net) represents diagrams in an XML structure (`<mxGraphModel>`) containing shapes, connectors, styles, layers, etc. ([Manually edit the XML source of your drawio diagram](https://www.drawio.com/doc/faq/diagram-source-edit#:~:text=The%20shapes%2C%20connectors%2C%20styles%2C%20and,diagram%2C%20and%20edit%20it%20directly)) This XML can be edited to manipulate the diagram programmatically.  
- drawio’s Arrange tools include alignment and distribution of shapes ([Arrange shapes and connectors](https://www.drawio.com/doc/faq/arrange-tab#:~:text=,shapes%20with%20an%20alternative%20spacing)), rotation and flipping of shapes (flips keep labels upright) ([Arrange shapes and connectors](https://www.drawio.com/doc/faq/arrange-tab#:~:text=Flip%20a%20shape%3A%20Click%20Horizontal,its%20label%20text%20in%20place)), and resetting connector routes ([Arrange shapes and connectors](https://www.drawio.com/doc/faq/arrange-tab#:~:text=Reset%20connector%20waypoints)) for tidy diagrams. Grouping of shapes is supported to treat multiple elements as one ([Arrange shapes and connectors](https://www.drawio.com/doc/faq/arrange-tab#:~:text=Group%20multiple%20shapes)). Layer ordering (front/back) can be controlled to stack shapes ([Arrange shapes and connectors](https://www.drawio.com/doc/faq/arrange-tab#:~:text=1,back%20via%20the%20Arrange%20tab)) ([Arrange shapes and connectors](https://www.drawio.com/doc/faq/arrange-tab#:~:text=Send%20shapes%20backwards%20or%20bring,one%20step%20at%20a%20time)).  
- The drawio editor grid can be toggled; when enabled, shapes snap to grid lines for alignment ([Blog - Snap to grid and other helpful alignment tools in drawio](https://www.drawio.com/blog/snap-to-grid#:~:text=When%20Grid%20is%20enabled%2C%20shapes,the%20grid%20or%20page%20center)). It can be disabled to allow free placement ([Blog - Snap to grid and other helpful alignment tools in drawio](https://www.drawio.com/blog/snap-to-grid#:~:text=Disable%20snapping%20temporarily%3A%20Hold%20down,from%20snapping%20to%20the%20grid)). Guidelines and connection point snapping further assist alignment ([Blog - Snap to grid and other helpful alignment tools in drawio](https://www.drawio.com/blog/snap-to-grid#:~:text=The%20draw,tab%20of%20the%20format%20panel)). These behaviors correspond to global diagram options (grid, guides, connection points) ([Set global diagram options in the format panel](https://www.drawio.com/doc/faq/diagram-options.html#:~:text=View%20settings)) ([Set global diagram options in the format panel](https://www.drawio.com/doc/faq/diagram-options.html#:~:text=Connection%20Arrows%3A%20Hide%20or%20display,you%20hover%20over%20a%20shape)).  
- Undo/redo typically uses an immutable Memento pattern with two stacks (undo and redo) to store previous states ([Ever wondered how the undo/redo functionality works in applications like Notepad or how games… | by Abdul Samad | Medium](https://medium.com/@cragzo7/ever-wondered-how-the-undo-redo-functionality-works-in-applications-like-notepad-or-how-games-56794f661ff4#:~:text=Undo%20Logic%3A)) ([Ever wondered how the undo/redo functionality works in applications like Notepad or how games… | by Abdul Samad | Medium](https://medium.com/@cragzo7/ever-wondered-how-the-undo-redo-functionality-works-in-applications-like-notepad-or-how-games-56794f661ff4#:~:text=1,you%20previously%20undid%20an%20action)). Each new change captures a snapshot of the state, which is efficient with immutable data structures due to structural sharing ([elm-undo-redo 2.0.0 - Elm Packages](https://package.elm-lang.org/packages/TheSeamau5/elm-undo-redo/latest/#:~:text=Given%20immutability%20lets%20you%20do,The%20library%20is)).  
- The drawio CLI (`drawio` command) supports exporting diagrams to PNG, JPEG, SVG, PDF, and VSDX, with options for scale, transparency, border, and embedding the diagram data ([How I use drawio at the command line | Tom Donohue](https://tomd.xyz/how-i-use-drawio/#:~:text=,for%20PNG%20format%20only)) ([How I use drawio at the command line | Tom Donohue](https://tomd.xyz/how-i-use-drawio/#:~:text=format%20only%29%20,crops%20PDF%20to%20diagram%20size)). We leverage these for previews and final exports. The CLI can create new files and perform batch operations ([How I use drawio at the command line | Tom Donohue](https://tomd.xyz/how-i-use-drawio/#:~:text=,If)), and while it lacks a dedicated “validate” command, exporting to an XML or image will implicitly validate the diagram structure. Tom Donohue’s guide provides examples of using the CLI for image exports with scaling ([How I use drawio at the command line | Tom Donohue](https://tomd.xyz/how-i-use-drawio/#:~:text=On%20Mac%20,and%20output%20to%20PNG)) ([How I use drawio at the command line | Tom Donohue](https://tomd.xyz/how-i-use-drawio/#:~:text=%2FApplications%2Fdrawio.app%2FContents%2FMacOS%2Fdrawio%20%5C%20,diagram.png%20mydiagram.svg)).