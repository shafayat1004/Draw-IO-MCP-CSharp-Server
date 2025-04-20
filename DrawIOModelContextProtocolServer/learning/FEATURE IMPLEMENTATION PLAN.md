# DrawIO MCP Server - Feature Implementation Plan

This document outlines a structured implementation plan for the tools requested in the comprehensive tier-based framework, analyzing which features are already available in the MCP server and which need to be implemented.

## Current Implementation Status

The DrawIO MCP Server currently implements the following tools:

### Already Implemented
- ✅ Create New Diagram (`create_new_diagram`)
- ✅ Add Shape (`add_shape`)
- ✅ Add Connector (`connect_shapes`)
- ✅ Delete Element (`delete_shape`)
- ✅ Move Shape (`move_shape`)
- ✅ Set Label/Text (via `update_shape`)
- ✅ Set Fill Color (via `style_shape`)
- ✅ Set Border Color (via `style_shape`)
- ✅ Auto-arrange layouts (basic versions via `arrange_diagram`)
- ✅ Export to image (`get_diagram_image`)
- ✅ Multi-page support (`create_diagram_page`, `get_diagram_page`, etc.)
- ✅ Update style properties (`update_shape_style`)

### Partially Implemented
- 🟨 Set Line Style (partial via `style_shape`, but not all options)
- 🟨 Set Arrow Style (partial via `style_shape`)
- 🟨 Styling capabilities (limited set of style properties)

## Implementation Plan by Tier

### Tier 1: Essential Creation & Editing Tools

#### Shape & Connector Management
1. **Create New Diagram** ✅ - *Already implemented*
2. **Add Shape** ✅ - *Already implemented*
3. **Add Connector** ✅ - *Already implemented*
4. **Delete Element** ✅ - *Already implemented*
5. **Move Shape** ✅ - *Already implemented*
6. **Resize Shape** 🔴 - *Implement as new `resize_shape` tool*
   - Approach: Modify shape geometry's width and height
   - Complexity: Low
   - Similar to existing `move_shape` implementation but for dimensions
   
7. **Set Label/Text** ✅ - *Already implemented via `update_shape`*

#### Basic Styling
8. **Set Fill Color** ✅ - *Already implemented via `style_shape`*
9. **Set Border Color** ✅ - *Already implemented via `style_shape`*
10. **Set Text Color** 🔴 - *Implement via extension to `style_shape` or new tool*
    - Approach: Update `fontColor` style property
    - Complexity: Low
    - Reuse style parsing/setting logic from existing tools
    
11. **Set Font Size** 🔴 - *Implement via extension to `style_shape` or new tool*
    - Approach: Update `fontSize` style property
    - Complexity: Low
    
12. **Toggle Bold/Italic** 🔴 - *Implement via extension to `style_shape` or new tool*
    - Approach: Update `fontStyle` style property
    - Complexity: Low
    
13. **Set Line Style** 🟨 - *Partially implemented, enhance existing `style_shape`*
    - Approach: Add support for `dashed`, `strokeWidth` properties
    - Complexity: Low
    
14. **Set Arrow Style** 🟨 - *Partially implemented, enhance existing `style_shape`*
    - Approach: Add support for `endArrow`, `startArrow` properties
    - Complexity: Low

#### Queries & Introspection
15. **Find Elements by Text** 🔴 - *New tool needed*
    - Approach: Implement `find_elements_by_text` to search through cell values
    - Complexity: Low
    - Returns array of matching element IDs
    
16. **Get Element Info** 🔴 - *New tool needed*
    - Approach: Implement `get_element_info` to return detailed properties
    - Complexity: Low
    - Essential for many operations
    
17. **List Neighbors of Node** 🔴 - *New tool needed*
    - Approach: Implement `list_neighbors` to find connected shapes
    - Complexity: Medium
    - Requires traversing edge connections
    
18. **Get Diagram Bounding Box / Size** 🔴 - *New tool needed*
    - Approach: Implement `get_diagram_bounds` to calculate extent
    - Complexity: Low
    - Useful for layout decisions

### Tier 2: Advanced Editing & Layout Tools

#### Layout & Positioning
19. **Align Shapes** 🟨 - *Partially implemented via `arrange_diagram`*
    - Approach: Enhance `arrange_diagram` to support additional alignment options
    - Complexity: Medium
    
20. **Distribute Shapes** 🟨 - *Partially implemented via `arrange_diagram`*
    - Approach: Enhance with horizontal/vertical distribution options
    - Complexity: Medium
    
21. **Precisely Nudge Shape** 🔴 - *New tool needed*
    - Approach: Implement `nudge_shape` with fine-grained movement
    - Complexity: Low
    - Can reuse `move_shape` with small deltas

#### Grouping & Layering
22. **Group Shapes** 🔴 - *New tool needed*
    - Approach: Implement `group_shapes` to create group container
    - Complexity: Medium
    - Requires understanding parent-child relationships
    
23. **Ungroup Shapes** 🔴 - *New tool needed*
    - Approach: Implement `ungroup_shapes` to unlink from group
    - Complexity: Medium
    
24. **Send to Front / Back** 🔴 - *New tool needed*
    - Approach: Implement `reorder_shape` with z-index changes
    - Complexity: Medium
    - Requires XML order manipulation
    
25. **Bring Forward / Send Backward** 🔴 - *New tool needed*
    - Approach: Combine with `reorder_shape` functionality
    - Complexity: Medium

#### Geometry & Appearance
26. **Rotate Shape** 🔴 - *New tool needed*
    - Approach: Implement `rotate_shape` to modify `rotation` style
    - Complexity: Low
    
27. **Flip Shape** 🔴 - *New tool needed*
    - Approach: Implement `flip_shape` to handle `flipH`/`flipV` styles
    - Complexity: Low
    
28. **Reset Connector Path** 🔴 - *New tool needed*
    - Approach: Implement `reset_connector` to clear waypoints
    - Complexity: Medium
    
29. **Reverse Connector Direction** 🔴 - *New tool needed*
    - Approach: Implement `reverse_connector` to swap source/target
    - Complexity: Low
    
30. **Change Shape Type** 🔴 - *New tool needed*
    - Approach: Implement `change_shape_type` to modify shape style
    - Complexity: Medium
    - Must handle different shape requirements

#### Media & Resources
31. **Insert Image** 🔴 - *New tool needed*
    - Approach: Implement `add_image` with data URI handling
    - Complexity: Medium-High
    - Requires proper embedding and sizing

#### Layer Management
32-36. **Layer Management** 🟨 - *Partially implemented via page management tools*
    - Approach: Add dedicated layer tools or document how to use pages as layers
    - Complexity: Medium
    - Evaluate if separate implementation needed beyond pages

#### Style Management
37-38. **Copy/Paste Style** 🔴 - *New tools needed*
    - Approach: Implement `copy_style`/`paste_style` to transfer properties
    - Complexity: Medium
    - Extract style from one shape, apply to others

### Tier 3: Specialized & Refinement Tools

#### Connectivity & Routing
39-42. **Connector Controls** 🔴 - *New tools needed*
    - Approach: Implement waypoint and connection management tools
    - Complexity: High
    - Will require detailed edge geometry manipulation

#### Metadata
43-44. **Metadata Management** 🔴 - *New tools needed*
    - Approach: Implement metadata storage in unused XML attributes
    - Complexity: Medium
    - Define conventions for storing metadata

#### Diagram Settings
45-47. **Diagram Settings** 🔴 - *New tools needed*
    - Approach: Implement global diagram property manipulation
    - Complexity: Medium
    - Update mxGraphModel attributes

#### Auto & Global Adjustments
48-49. **Auto Layout Functions** 🟨 - *Partially implemented via `arrange_diagram`*
    - Approach: Enhance with additional layout algorithms
    - Complexity: High
    - Consider library integration for advanced layouts

#### Undo/Redo
50-51. **Undo/Redo** 🔴 - *New architecture needed*
    - Approach: Implement state management for diagram changes
    - Complexity: High
    - Core architectural change (see below)

### Feedback & Export Tools

#### Visual Feedback Loop
52-54. **Render & Preview** ✅ - *Already implemented via `get_diagram_image`*

#### Validation & Output
55-58. **Export Options** 🟨 - *Partially implemented, extend `get_diagram_image`*
    - Approach: Add support for more formats and options
    - Complexity: Medium
    - Requires draw.io CLI capability tests

## Core Architecture Enhancements

### Undo/Redo System Implementation
The undo/redo functionality will require a significant architectural enhancement:

1. **Memento Pattern**
   - Track diagram states in a history stack
   - Pure functional approach with immutable states
   - Each operation creates a new diagram state

2. **Implementation Approach**
   ```fsharp
   type EditorState = {
       Current: DiagramModel
       UndoStack: DiagramModel list
       RedoStack: DiagramModel list
   }
   ```

3. **Action Wrapper**
   - Wrap all modification tools to automatically push to undo stack
   - Keep read-only tools outside undo management

4. **Storage Optimization**
   - Use structural sharing for efficiency
   - Consider cleanup of image data duplicates

### Overlap Detection Implementation
For the requested overlap detection:

1. **Detection Algorithm**
   - Calculate bounding box intersections
   - Optional: pixel-perfect detection for complex shapes

2. **Implementation Approach**
   ```fsharp
   let detectOverlap (diagram: DiagramModel, shape1Id: string, shape2Id: string) : bool =
       // Get geometries
       // Calculate intersection
       // Return true if shapes overlap
   ```

3. **API Design**
   - `detect_overlapping_shapes` - Return all overlapping pairs
   - `is_shape_overlapping` - Check if specific shape overlaps with any others

## Targeted Query Implementation
For enhanced querying capabilities:

1. **Filtering Mechanism**
   - Support filtering by type, text, style, position
   - Return properly formatted results or sub-diagrams

2. **Implementation Approach**
   ```fsharp
   let queryDiagram (diagram: DiagramModel, criteria: QueryCriteria) : Element list =
       // Filter diagram elements based on criteria
       // Return matching elements
   ```

3. **API Design**
   - `query_diagram_elements` - Flexible filtering tool
   - `get_element_details` - Detailed view of specific element

## Implementation Priority

We recommend implementing tools in this order:

1. **Essential Query Tools** (15-18)
   - These enable intelligent operations on existing diagrams
   - Fundamental for LLM reasoning about diagrams

2. **Basic Shape Manipulation** (6, 10-12)
   - Complete the essential shape editing capabilities
   - Relatively simple implementations

3. **Connector Enhancement** (13-14, 28-29)
   - Improve line styles and connector manipulation
   - Medium complexity, high impact

4. **Advanced Layout** (19-21, 26-27)
   - Alignment, distribution, rotation capabilities
   - Significant UX improvement

5. **Metadata & Structure** (22-25, 37-38, 43-44)
   - Grouping, layering, metadata
   - Foundation for complex diagrams

6. **Undo/Redo Architecture**
   - Core capability for editing confidence
   - Should be carefully designed and tested

7. **Specialized Tools** (39-42, 45-51)
   - Complete advanced functionality
   - Implement based on actual usage patterns

## Technical Approach

1. **Add Core F# Functions**
   - Implement pure functions in DrawIO.MCP.Core/Library.fs
   - Maintain functional approach for consistency

2. **Expose through MCP Tools**
   - Add corresponding tool definitions in STDIO/SSE implementations
   - Follow existing parameter/return patterns

3. **Testing Strategy**
   - Unit test core functions
   - Integration test through actual diagram manipulation
   - Visual verification with image export

4. **Documentation**
   - Update README.md with new capabilities
   - Add examples to USAGE_EXAMPLES.md
   - Create specific tool documentation 