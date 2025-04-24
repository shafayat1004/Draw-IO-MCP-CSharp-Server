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

### Needs Investigation
- 🟨 Get Diagram Image (`get_diagram_image`) - drawio CLI integration issues:
  - CLI is installed and available
  - Image generation works but file creation reports errors
  - Need to investigate file paths and permissions
  - Consider alternative export methods if CLI issues persist

### Known Missing Features
Based on testing and documentation review:

#### Shape and Text Manipulation
- 🔴 Group/Ungroup shapes
- 🔴 Rotate shapes
- 🔴 Flip shapes horizontally/vertically
- 🔴 Lock/unlock elements
- 🔴 Autosize shapes to fit text
- 🔴 Add custom fonts support
- 🔴 Text formatting (superscript, subscript)
- 🔴 Change text writing direction
- 🔴 Copy/paste styles between shapes

#### Connector Features
- 🔴 Add/remove/modify waypoints on connectors
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

### Limitations
- 🔴 No manual waypoint control for precise path adjustment
- 🔴 Cannot connect to arbitrary points on shapes (only predefined connection points)
- 🔴 No bidirectional arrow support in a single connector
- 🔴 No waypoint drag-and-drop style editing

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

### Tier 2: Advanced Editing & Layout Tools (Next Priority)

#### Priority 1: Connector Enhancements
21. **Add/Remove/Modify Waypoints** 🔴 - *Highest priority*
    - Approach: Implement waypoint data structure and manipulation functions
    - Add tools for adding, removing, and moving waypoints on connectors
    - Complexity: Medium-High
    - Essential for precise routing control

#### Priority 2: Organizational Features
22. **Group Shapes** 🔴 - *High priority*
    - Approach: Implement parent-child relationships
    - Add tools for grouping/ungrouping elements
    - Complexity: High
    - Essential for diagram organization
    
26. **Autosize to Text** 🔴 - *Medium priority*
    - Approach: Calculate text bounds and adjust shape dimensions
    - Add tool for automatic shape sizing based on content
    - Complexity: Medium
    - Improves usability for text-heavy diagrams

#### Priority 3: Layout & Positioning
19. **Align Shapes** 🟨 - *Medium priority*
    - Approach: Add specific alignment options (left, right, center, top, bottom)
    - Extend `arrange_diagram` or create new alignment tool
    - Complexity: Medium
    
20. **Distribute Shapes** 🟨 - *Medium priority*
    - Approach: Add distribution options (horizontal, vertical spacing)
    - Extend `arrange_diagram` or create new distribution tool
    - Complexity: Medium

#### Priority 4: Advanced Organization
23. **Layer Management** 🔴 - *Medium priority*
    - Approach: Implement layer hierarchy
    - Add tools for creating/managing layers and moving elements between them
    - Complexity: High
    - Required for complex diagrams

#### Advanced Styling
24. **Copy/Paste Styles** 🔴 - *Lower priority*
    - Approach: Store and apply style templates
    - Complexity: Medium
    
25. **Global Styles** 🔴 - *Lower priority*
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
1. **Implement Waypoint Control System**
   - Design data structures for waypoint representation
   - Create manipulation functions in core library
   - Develop user-friendly tools for waypoint editing
   - Add comprehensive tests for waypoint features

2. **Fix Image Export**
   - Investigate CLI integration
   - Implement reliable export pipeline
   - Support multiple formats

3. **Shape Grouping System**
   - Design group hierarchy implementation
   - Add group operations to core library
   - Create tools for group manipulation
   - Ensure proper visual representation of groups

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

## Implementation Strategy

1. **Waypoint Implementation (Highest Priority)**
   - Analyze DrawIO's waypoint data structure
   - Implement core waypoint functions in F#
   - Create tools for adding, removing, and editing waypoints
   - Add comprehensive tests for waypoint manipulation

2. **Group/Ungroup Implementation**
   - Design parent-child relationship model
   - Implement group operations in core library
   - Create tools for group management
   - Add tests for grouping functionality

3. **Autosize to Text**
   - Research text measurement techniques
   - Implement autosize functionality in core library
   - Create tool for automatic shape resizing
   - Add tests for text measurement and shape resizing

4. **Tier 2 Implementation**
   - Focus on remaining tier 2 features
   - Add alignment and distribution options
   - Implement layer management

5. **Documentation**
   - Update API documentation
   - Add usage examples
   - Create tutorials 