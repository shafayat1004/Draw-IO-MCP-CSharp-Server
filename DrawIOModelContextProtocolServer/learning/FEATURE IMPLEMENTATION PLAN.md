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
  - Line pattern (solid, dashed)
  - Line width
- ✅ Set Arrow Style (`set_arrow_style`) with:
  - Start arrow type
  - End arrow type
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
- 🔴 Add/remove waypoints on connectors
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

#### Layout & Positioning
19. **Align Shapes** 🟨 - *Partially via `arrange_diagram`*
    - Approach: Add specific alignment options
    - Complexity: Medium
    
20. **Distribute Shapes** 🟨 - *Partially via `arrange_diagram`*
    - Approach: Add distribution options
    - Complexity: Medium
    
21. **Add/Remove Waypoints** 🔴 - *New tool needed*
    - Approach: Implement waypoint manipulation
    - Complexity: Medium
    - Essential for complex routing

#### Grouping & Layering
22. **Group Shapes** 🔴 - *High priority*
    - Approach: Implement parent-child relationships
    - Complexity: High
    - Essential for diagram organization
    
23. **Layer Management** 🔴 - *High priority*
    - Approach: Implement layer hierarchy
    - Complexity: High
    - Required for complex diagrams

#### Advanced Styling
24. **Copy/Paste Styles** 🔴 - *Medium priority*
    - Approach: Store and apply style templates
    - Complexity: Medium
    
25. **Global Styles** 🔴 - *Medium priority*
    - Approach: Implement style inheritance
    - Complexity: Medium

### Tier 3: Enhanced Features (Future Implementation)

#### Text and Labels
26. **Autosize to Text** 🔴
    - Approach: Calculate text bounds
    - Complexity: Medium
    
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
1. **Fix Image Export**
   - Investigate CLI integration
   - Implement reliable export pipeline
   - Support multiple formats

2. **Enhanced Layout Engine**
   - Implement more layout algorithms
   - Add alignment options
   - Support distribution patterns

3. **Style System Refactor**
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

1. **Complete Tier 1 Polish**
   - Fix any remaining issues
   - Improve error handling
   - Add comprehensive tests

2. **Tier 2 Implementation**
   - Focus on grouping and layers
   - Enhance connector features
   - Add advanced styling

3. **Core Architecture**
   - Implement state management
   - Enhance style system
   - Fix image export

4. **Documentation**
   - Update API documentation
   - Add usage examples
   - Create tutorials 