# DrawIO MCP Server - Feature Implementation Plan

This document outlines a structured implementation plan for the tools requested in the comprehensive tier-based framework, analyzing which features are already available in the MCP server and which need to be implemented.

## Current Implementation Status

The DrawIO MCP Server currently implements the following tools:

### Fully Implemented and Tested Working
- ✅ Create New Diagram (`create_new_diagram`)
- ✅ Generate Sample VPC Layout (`generate_vpc`)
- ✅ Add Shape (`add_shape`) with basic shapes (rectangle, ellipse)
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
- ✅ Auto-arrange Layouts (`arrange_diagram`) with:
  - Horizontal layout
  - More layouts to be implemented
- ✅ Set Text Style (`set_text_style`) with:
  - Font color
  - Font size
  - Font style (bold, italic)
- ✅ Set Line Style (`set_line_style`) with:
  - Line pattern (solid, dashed, dotted)
  - Line width
  - Edge style (sharp, rounded, curved)
  - Routing style (straight, orthogonal, curved)
  - Jump style (overlapped, arc, gap)
- ✅ Set Arrow Style (`set_arrow_style`) with:
  - Start arrow type (none, classic, diamond, oval, open, block)
  - End arrow type (none, classic, diamond, oval, open, block)
- ✅ Reset Connector (`reset_connector`)
- ✅ Reverse Connector (`reverse_connector`)
- ✅ Resize Shape (`resize_shape`)
- ✅ Waypoint Management for Connectors:
  - Add Waypoint (`add_waypoint`)
  - Remove Waypoint (`remove_waypoint`)
  - Update Waypoint (`update_waypoint`)
  - Get Waypoints (`get_waypoints`)
  - Clear Waypoints (`clear_waypoints`)
- ✅ Group/Ungroup Shapes:
  - Group Shapes (`group_shapes`)
  - Ungroup Shapes (`ungroup_shapes`)
- ✅ Get Diagram Image (`get_diagram_image`):
  - Output diagram as PNG image
  - Include diagram image in tool responses with `return_diagram` parameter
  - All tools now support returning the diagram image after execution

### Enhanced Tool Functionality
- ✅ Return Diagram Image with Tool Response:
  - All tools support the `return_diagram` parameter
  - When set to `true`, tools return both the operation result and a rendered image of the current diagram state
  - Provides immediate visual feedback after each operation
  - Eliminates the need for separate image requests after edits

### Known Missing Features
Based on testing and documentation review:

#### Shape and Text Manipulation
- ✅ Group/Ungroup shapes - IMPLEMENTED
- 🔴 Rotate shapes
- 🔴 Flip shapes horizontally/vertically
- 🔴 Lock/unlock elements
- 🔴 Autosize shapes to fit text
- 🔴 Add custom fonts support
- 🔴 Text formatting (superscript, subscript)
- 🔴 Change text writing direction
- 🔴 Copy/paste styles between shapes

#### Connector Features
- ✅ Add/remove/modify waypoints on connectors - IMPLEMENTED
- 🔴 Connect to arbitrary points on shapes
- 🔴 Hide connection arrows
- 🔴 Hide fixed connection points
- 🔴 Animate connectors
- 🔴 Bidirectional arrows
- 🔴 Join connectors
- 🔴 Copy on Connect feature

#### Layout and Organization
- 🔴 Layer support (add, remove, move between)
- 🔴 Background image support
- 🔴 Change page size and orientation
- 🔴 Grid customization
- 🔴 Snap to grid/points
- 🔴 Custom shape libraries
- 🔴 Container shapes with collapse/expand
- 🔴 Tables and cross-functional tables
- 🔴 Merge/unmerge table cells

#### Advanced Features
- 🔴 Custom properties
- 🔴 Global styles
- 🔴 Mathematical typesetting
- 🔴 Comments support
- 🔴 Revision history
- 🔴 Undo/Redo support
- 🔴 Export to various formats (PDF, SVG, etc.)
- 🔴 Import from other formats
- 🔴 Plugins support

## Connector Styling and Routing Capabilities

Our current implementation supports sophisticated connector styling and routing through the following features:

### Right-Angle (Orthogonal) Routing
- ✅ Orthogonal edge style via `set_line_style` with `routing_style="orthogonal"`
- ✅ Control of edge appearance with options for sharp, rounded, or curved corners
- ✅ Jump style configuration for connector crossings (overlapped, arc, gap)

### Arrow Styling
- ✅ Customization of both start and end arrow styles
- ✅ Multiple arrow head types: none, classic, diamond, oval, open, block
- ✅ Arrow direction reversal via `reverse_connector` tool

### Connector Reset & Management
- ✅ Reset connectors to default routing paths
- ✅ Automatic connector routing between shapes

### Waypoint Control
- ✅ Manual waypoint addition, removal and modification
- ✅ Support for multiple waypoints per connector
- ✅ Clear all waypoints from a connector
- ✅ Query all waypoints for a connector
- ✅ Create right-angled paths using strategic waypoint placement

### Limitations
- 🔴 Cannot connect to arbitrary points on shapes (only predefined connection points)
- 🔴 No bidirectional arrow support in a single connector
- 🔴 No waypoint drag-and-drop style editing in the API (would need UI implementation)

## Implementation Plan by Tier

### Tier 1: Essential Creation & Editing Tools (Current Focus)

#### Shape & Connector Management
1. **Create New Diagram** ✅ - *Implemented and tested*
2. **Add Shape** ✅ - *Implemented and tested*
3. **Add Connector** ✅ - *Implemented and tested*
4. **Delete Element** ✅ - *Implemented and tested*
5. **Move Shape** ✅ - *Implemented and tested*
6. **Resize Shape** ✅ - *Implemented and tested*
7. **Set Label/Text** ✅ - *Implemented via `update_shape`*

#### Basic Styling
8. **Set Fill Color** ✅ - *Implemented via `style_shape`*
9. **Set Border Color** ✅ - *Implemented via `style_shape`*
10. **Set Text Color** ✅ - *Implemented via `set_text_style`*
11. **Set Font Size** ✅ - *Implemented via `set_text_style`*
12. **Toggle Bold/Italic** ✅ - *Implemented via `set_text_style`*
13. **Set Line Style** ✅ - *Implemented via `set_line_style`*
14. **Set Arrow Style** ✅ - *Implemented via `set_arrow_style`*

#### Queries & Introspection
15. **Find Elements by Text** ✅ - *Implemented and tested*
16. **Get Element Info** ✅ - *Implemented and tested*
17. **List Neighbors** ✅ - *Implemented and tested*
18. **Get Diagram Bounds** ✅ - *Implemented and tested*

#### Advanced Connector Control
19. **Add/Remove/Modify Waypoints** ✅ - *Implemented and tested*
    - Waypoint data structure and manipulation functions are now in place
    - Tools for adding, removing, moving and clearing waypoints implemented
    - Fixed XML structure to use `<Array as="points">` container for waypoints
    - Backward compatibility for parsing older waypoint formats

#### Advanced Organization
20. **Group/Ungroup Shapes** ✅ - *Implemented and tested*
    - Parent-child relationships implemented
    - Proper XML structure for groups created
    - Tools for grouping and ungrouping shapes implemented
    - Comprehensive tests for grouping functionality added

### Tier 2: Advanced Editing & Layout Tools (Next Priority)

#### Priority 1: Organizational Features
21. **Autosize to Text** 🔴 - *High priority*
    - Approach: Calculate text bounds and adjust shape dimensions
    - Add tool for automatic shape sizing based on content
    - Complexity: Medium
    - Improves usability for text-heavy diagrams

#### Priority 2: Layout & Positioning
22. **Align Shapes** 🟨 - *Medium priority*
    - Approach: Add specific alignment options (left, right, center, top, bottom)
    - Extend `arrange_diagram` or create new alignment tool
    - Complexity: Medium
    
23. **Distribute Shapes** 🟨 - *Medium priority*
    - Approach: Add distribution options (horizontal, vertical spacing)
    - Extend `arrange_diagram` or create new distribution tool
    - Complexity: Medium

#### Priority 3: Advanced Organization
24. **Layer Management** 🔴 - *Medium priority*
    - Approach: Implement layer hierarchy
    - Add tools for creating/managing layers and moving elements between them
    - Complexity: High
    - Required for complex diagrams

#### Advanced Styling
25. **Copy/Paste Styles** 🔴 - *Lower priority*
    - Approach: Store and apply style templates
    - Complexity: Medium
    
26. **Global Styles** 🔴 - *Lower priority*
    - Approach: Implement style inheritance
    - Complexity: Medium

### Tier 3: Enhanced Features (Future Implementation)

#### Text and Labels
27. **Custom Fonts** 🔴
    - Approach: Font embedding/linking
    - Complexity: High

#### Advanced Connectors
28. **Bidirectional Arrows** 🔴
    - Approach: Extend arrow styling
    - Complexity: Low
    
29. **Join Connectors** 🔴
    - Approach: Implement connection points
    - Complexity: High

#### Container Features
30. **Collapse/Expand** 🔴
    - Approach: Implement container logic
    - Complexity: High
    
31. **Tables** 🔴
    - Approach: Special container type
    - Complexity: High

### Tier 4: Professional Features (Long-term Goals)

#### Advanced Export/Import
32. **Multiple Formats** 🔴
    - Approach: Implement format converters
    - Complexity: High
    
33. **High-Resolution Export** 🔴
    - Approach: Scale rendering
    - Complexity: Medium

#### Collaboration Features
34. **Comments** 🔴
    - Approach: Metadata storage
    - Complexity: Medium
    
35. **Revision History** 🔴
    - Approach: State management
    - Complexity: High

## Core Architecture Enhancements

### Immediate Priorities
1. ✅ **Implement Waypoint Control System** - COMPLETED
   - ✅ Designed data structures for waypoint representation
   - ✅ Created manipulation functions in core library
   - ✅ Developed user-friendly tools for waypoint editing
   - ✅ Added comprehensive tests for waypoint features
   - ✅ Fixed XML structure to wrap waypoints in `<Array as="points">` elements
   - ✅ Added backward compatibility for waypoint parsing

2. ✅ **Implement Shape Grouping System** - COMPLETED
   - ✅ Designed parent-child relationship model
   - ✅ Implemented group operations in core library
   - ✅ Created tools for group manipulation
   - ✅ Added tests for grouping functionality

3. ✅ **Fix Image Export** - COMPLETED
   - ✅ Implemented diagram image export functionality
   - ✅ Added `return_diagram` parameter to all tool calls
   - ✅ Created reliable pipeline to render diagrams after operations
   - ✅ Supporting PNG image format

4. **Enhanced Layout Engine**
   - Implement more layout algorithms
   - Add alignment options
   - Support distribution patterns

5. **Style System Refactor**
   - Create style templates
   - Implement inheritance
   - Support global styles

### Future Enhancements
1. **State Management**
   - Implement undo/redo
   - Support revision history
   - Enable collaboration

2. **Plugin Architecture**
   - Design plugin interface
   - Support custom shapes
   - Enable extensions

3. **Protocol Compliance Enhancements**
   - Implement `$/cancelRequest` handler (for 2025-03-26 compliance)
   - Investigate `CancellationToken` integration for cancellable tool execution

## Implementation Strategy

1. ✅ **Waypoint Implementation** - COMPLETED
   - ✅ Analyzed DrawIO's waypoint data structure
   - ✅ Implemented core waypoint functions in F#
   - ✅ Created tools for adding, removing, and editing waypoints
   - ✅ Added comprehensive tests for waypoint manipulation
   - ✅ Fixed XML structure to properly wrap waypoints in arrays

2. ✅ **Group/Ungroup Implementation** - COMPLETED
   - ✅ Designed parent-child relationship model
   - ✅ Implemented group operations in core library
   - ✅ Created tools for group management
   - ✅ Added tests for grouping functionality

3. **Autosize to Text** (Next Priority)
   - Research text measurement techniques
   - Implement autosize functionality in core library
   - Create tool for automatic shape resizing
   - Add tests for text measurement and shape resizing

4. **Remaining Tier 2 Implementation**
   - Focus on alignment and distribution options
   - Implement layer management
   - Add advanced styling features

5. **Documentation**
   - Update API documentation
   - Add usage examples
   - Create tutorials for new features
   - Document waypoint handling and connector styling
   - Document grouping functionality 