# DrawIO MCP Server - Feature Implementation Plan

This document outlines a structured implementation plan for the tools requested in the comprehensive tier-based framework, analyzing which features are already available in the MCP server and which need to be implemented.

## Current Implementation Status

The DrawIO MCP Server currently implements the following tools:

### Already Implemented and Confirmed Working
- ✅ Create New Diagram (`create_new_diagram`)
- ✅ Generate Sample VPC Layout (`generate_vpc`)
- ✅ Add Shape (`add_shape`) with basic shapes (rectangle, ellipse, cylinder)
- ✅ Connect Shapes (`connect_shapes`)
- ✅ Style Shape (`style_shape`) with:
  - Fill Color
  - Border Color
- ✅ Move Shape (`move_shape`)
- ✅ Create Diagram Page (`create_diagram_page`)
- ✅ Get Diagram Page (`get_diagram_page`)
- ✅ Get Element Info (`get_element_info`)
- ✅ Find Elements by Text (`find_elements_by_text`)
- ✅ List Neighbors (`list_neighbors`)
- ✅ Get Diagram Bounds (`get_diagram_bounds`)
- ✅ Update Shape (`update_shape`)

### Needs Visual Verification
- 🟨 Delete Shape (`delete_shape`) - Implementation appears to work but needs visual confirmation
- 🟨 Update Shape Style (`update_shape_style`) - Changes applied but needs visual confirmation
- 🟨 Auto-arrange Layouts (`arrange_diagram`) - Layout changes need visual confirmation

### Configuration Required
- 🟨 Export to Image (`get_diagram_image`) - drawio CLI found but not working properly

### Known Missing Features
- 🟨 Set Line Style (partial via `style_shape`, but not all options)
- 🟨 Set Arrow Style (partial via `style_shape`)
- 🟨 Styling capabilities (limited set of style properties)

## Implementation Plan by Tier

### Tier 1: Essential Creation & Editing Tools

#### Shape & Connector Management
1. **Create New Diagram** ✅ - *Already implemented*
2. **Add Shape** ✅ - *Already implemented*
3. **Add Connector** ✅ - *Already implemented*
4. **Delete Element** 🟨 - *Implementation status unclear*
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
15. **Find Elements by Text** ✅ - *Already implemented*
16. **Get Element Info** ✅ - *Already implemented*
17. **List Neighbors of Node** ✅ - *Already implemented*
18. **Get Diagram Bounding Box / Size** ✅ - *Already implemented*

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
52-54. **Render & Preview** 🟨 - *Partially implemented, extend `get_diagram_image`*

#### Validation & Output
55-58. **Export Options** 🟨 - *Partially implemented, extend `get_diagram_image`*
    - Approach: Add support for more formats and options
    - Complexity: Medium
    - Requires drawio CLI capability tests

### Tier 4: Network Diagram Specific Tools

#### Canvas Management
59. **Set Canvas Size** 🔴 - *New tool needed*
    - Approach: Implement `set_canvas_size` to modify diagram dimensions
    - Complexity: Low
    - Essential for large network diagrams
    
60. **Set Canvas Background** 🔴 - *New tool needed*
    - Approach: Implement `set_background` for zone coloring
    - Complexity: Low
    - Support for zone differentiation

#### Network-Specific Shapes
61. **Add Network Component** 🔴 - *New tool needed*
    - Approach: Implement `add_network_component` with predefined network shapes
    - Complexity: Medium
    - Include server, firewall, switch, router icons
    
62. **Add Connection Line** 🔴 - *New tool needed*
    - Approach: Implement `add_connection` with network-specific line styles
    - Complexity: Medium
    - Support for different connection types (VPN, HTTPS, etc.)

#### Zone Management
63. **Create Zone** 🔴 - *New tool needed*
    - Approach: Implement `create_zone` for network segmentation
    - Complexity: Medium
    - Support for zone coloring and labeling
    
64. **Group Components in Zone** 🔴 - *New tool needed*
    - Approach: Implement `group_in_zone` for logical grouping
    - Complexity: Medium
    - Maintain zone relationships

#### Network-Specific Styling
65. **Set Connection Type** 🔴 - *New tool needed*
    - Approach: Implement `set_connection_type` for line patterns
    - Complexity: Low
    - Support for dotted, dashed, colored lines
    
66. **Add Network Icon** 🔴 - *New tool needed*
    - Approach: Implement `add_network_icon` for standard network symbols
    - Complexity: Medium
    - Include common network icon library

#### Layout Assistance
67. **Auto-arrange Network Layout** 🔴 - *New tool needed*
    - Approach: Enhance `arrange_diagram` with network-specific layouts
    - Complexity: High
    - Support for hierarchical network layouts
    
68. **Align to Network Grid** 🔴 - *New tool needed*
    - Approach: Implement `align_to_grid` for clean positioning
    - Complexity: Medium
    - Maintain professional spacing

#### Documentation
69. **Add Network Legend** 🔴 - *New tool needed*
    - Approach: Implement `add_legend` for connection types
    - Complexity: Low
    - Auto-generate based on used elements
    
70. **Add Zone Labels** 🔴 - *New tool needed*
    - Approach: Implement `add_zone_label` for clear identification
    - Complexity: Low
    - Support for consistent zone naming

#### Export and Rendering
71. **CLI Integration** 🔴 - *New tool needed*
    - Approach: Implement proper drawio CLI integration
    - Complexity: Medium
    - Required for image export and rendering

72. **Custom Shape Libraries** 🔴 - *New tool needed*
    - Approach: Implement support for loading custom shape libraries
    - Complexity: High
    - Include network equipment shapes

73. **Shape Styling Templates** 🔴 - *New tool needed*
    - Approach: Implement predefined style templates
    - Complexity: Medium
    - Support for consistent styling across components

74. **Multi-layer Text** 🔴 - *New tool needed*
    - Approach: Implement support for title, subtitle, and description text
    - Complexity: Low
    - Better labeling for complex components

75. **Container Hierarchy** 🔴 - *New tool needed*
    - Approach: Implement proper parent-child relationships
    - Complexity: High
    - Better zone and component organization

#### Server Stability
76. **Connection Management** 🔴 - *New feature needed*
    - Approach: Implement robust connection handling
    - Complexity: High
    - Handle disconnects and reconnects gracefully

77. **Session Persistence** 🔴 - *New feature needed*
    - Approach: Implement session state management
    - Complexity: Medium
    - Maintain diagram state across reconnections

78. **Error Recovery** 🔴 - *New feature needed*
    - Approach: Implement automatic error recovery
    - Complexity: Medium
    - Handle and recover from common error conditions

79. **Command Queueing** 🔴 - *New feature needed*
    - Approach: Implement command queue with retry logic
    - Complexity: Medium
    - Ensure commands are not lost during disconnects

80. **Health Monitoring** 🔴 - *New feature needed*
    - Approach: Implement server health checks
    - Complexity: Low
    - Monitor server status and performance

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

8. **Network Diagram Specific Tools** (59-70)
   - Network-specific capabilities
   - Essential for professional network diagrams

9. **Rendering and Styling** (71-75)
   - CLI integration, shape libraries, styling templates, text handling, container management
   - Critical for professional network diagrams

10. **Server Stability** (76-80)
    - Robust connection handling, state persistence, error recovery, command reliability, health monitoring
    - Essential for reliable server operation and diagram creation

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