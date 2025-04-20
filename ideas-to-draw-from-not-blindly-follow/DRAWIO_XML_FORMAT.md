# DrawIO XML Format Guide

## Overview
This document outlines the correct format for DrawIO XML files to ensure proper rendering in DrawIO/diagrams.net applications. These guidelines were established after troubleshooting the "Could not add object for mxGeometry" error.

## Critical Format Requirements

### XML Declaration
```xml
<?xml version="1.0" encoding="UTF-8"?>
```
- Always use double quotes for attribute values
- UTF-8 encoding is recommended

### mxFile Structure
```xml
<mxfile host="app.diagrams.net" modified="2023-05-17T00:00:00.000Z" agent="MCP-DrawIO" version="15.0.0" type="device">
  <diagram id="diagram-id" name="Page-1">
    <!-- diagram content -->
  </diagram>
</mxfile>
```
- Attribute ordering can affect parsing
- Recommended order: `host`, `modified`, `agent`, `version`, `type`

### mxGraphModel Structure
```xml
<mxGraphModel dx="800" dy="800" grid="1" gridSize="10" guides="1" tooltips="1" connect="1" arrows="1" fold="1" page="1" pageScale="1" pageWidth="800" pageHeight="600" math="0" shadow="0">
  <root>
    <!-- root cells -->
  </root>
</mxGraphModel>
```

### Cell Structure

- Each element in the diagram is represented by an `mxCell` element
- Cells have a unique `id` attribute
- Common attributes include `style`, `value`, `vertex`, `edge`, `source`, and `target`
- Parent-child relationships are critical for proper rendering:
  - Root cells use `parent="1"` (where 1 is the root layer ID)
  - Children cells must reference their parent's ID in the `parent` attribute
  - Edge cells (connectors) must properly reference source and target cells
  - Incorrect parent references will cause elements to be invisible or misplaced
- Cells must maintain proper hierarchy:
  - Container cells (like groups) must have their child cells reference them as parent
  - When moving or duplicating cells, parent references must be maintained
- Example of proper parent-child relationship:
  ```xml
  <mxCell id="1" parent="0"/> <!-- Root layer -->
  <mxCell id="2" value="Parent Shape" vertex="1" parent="1">
    <mxGeometry x="100" y="100" width="200" height="200" as="geometry"/>
  </mxCell>
  <mxCell id="3" value="Child Shape" vertex="1" parent="2">
    <mxGeometry x="50" y="50" width="100" height="100" as="geometry"/>
  </mxCell>
  ```
  In this example, cell "3" is a child of cell "2", which is a child of the root layer "1".

### Cell Structure and Parent-Child Relationships

- The `mxCell` elements form the backbone of any DrawIO diagram through their parent-child relationships
- **CRITICAL STRUCTURE RULES**:
  - **Root Cell (id="0")**: Must have NO parent attribute at all
    - Incorrect: `<mxCell id="0" parent="someId"/>`
    - Correct: `<mxCell id="0"/>`
  
  - **Layer Cell (id="1")**: Must have `parent="0"` to establish correct hierarchy
    - Incorrect: `<mxCell id="1"/>`
    - Correct: `<mxCell id="1" parent="0"/>`
  
  - **All Regular Cells**: Must have a valid parent reference that creates a proper tree structure
    - Top-level objects typically use `parent="1"` (attaching to the root layer)
    - Objects in groups must reference their containing group's ID as parent
  
  - **Edge Cells (Connectors)**: Should typically have `parent="1"` and valid source/target references
    - Proper format: `<mxCell id="edge1" edge="1" parent="1" source="cell1" target="cell2">`
  
- **Hierarchical Nesting Effects**:
  - When a parent cell is moved, all its children move with it
  - When a parent cell is deleted, all its children are also deleted
  - Child geometries are often relative to their parent's position
  - Group cells (with style="group") have special parent-child behavior

- **Advanced Example of Complex Nesting**:
  ```xml
  <!-- Basic structure -->
  <mxCell id="0"/>  <!-- Root cell - NO PARENT -->
  <mxCell id="1" parent="0"/>  <!-- Layer cell -->
  
  <!-- Group container -->
  <mxCell id="group1" value="Group" style="group;" vertex="1" parent="1">
    <mxGeometry x="100" y="100" width="300" height="200" as="geometry"/>
  </mxCell>
  
  <!-- Elements inside the group - note they reference group1 as parent -->
  <mxCell id="rect1" value="Rectangle" style="rectangle;" vertex="1" parent="group1">
    <mxGeometry x="20" y="30" width="100" height="60" as="geometry"/>
  </mxCell>
  
  <!-- Nested group (a group inside another group) -->
  <mxCell id="group2" value="Nested Group" style="group;" vertex="1" parent="group1">
    <mxGeometry x="150" y="40" width="120" height="120" as="geometry"/>
  </mxCell>
  
  <!-- Element in the nested group - references group2 as parent -->
  <mxCell id="circle1" value="Circle" style="ellipse;" vertex="1" parent="group2">
    <mxGeometry x="20" y="20" width="80" height="80" as="geometry"/>
  </mxCell>
  
  <!-- Connection between elements across groups -->
  <mxCell id="edge1" style="edgeStyle=orthogonalEdgeStyle;" edge="1" parent="1" source="rect1" target="circle1">
    <mxGeometry relative="1" as="geometry"/>
  </mxCell>
  ```

- **Common Errors and Their Symptoms**:
  - **Empty Diagram**: Often caused by incorrect parent references, especially for root cells
  - **Missing Elements**: Check if the parent exists and if the parent reference is correct
  - **Elements Positioned Incorrectly**: May occur when child cells are not properly referencing their container groups
  - **Unable to Select Objects**: Can happen with invalid parent references

- **Tips for Debugging**:
  - Always ensure the root cell (id="0") has no parent attribute
  - Verify all parent references point to existing cell IDs
  - Check for circular parent references (a cell can't be its own ancestor)
  - When troubleshooting complex groups, validate the complete parent chain

### mxGeometry Format
- **IMPORTANT**: The `mxGeometry` element must be a child of an `mxCell` element
- The `as="geometry"` attribute must be at the end of the tag
- Incorrect: `<mxGeometry as="geometry" x="200" y="200" width="120" height="60"/>`
- Correct: `<mxGeometry x="200" y="200" width="120" height="60" as="geometry"/>`
- **Attribute Order**: Coordinate attributes must appear in this exact order:
  1. `x` (horizontal position)
  2. `y` (vertical position)
  3. `width`
  4. `height`
  5. `as="geometry"` (always last)
- If using `relative="1"` for edge geometry, it should be placed before `as="geometry"`:
  - Correct: `<mxGeometry relative="1" as="geometry"/>`

### Self-Closing Tags
- Self-closing tags should not have spaces before the closing slash
- Incorrect: `<mxCell id="0" />`
- Correct: `<mxCell id="0"/>`

### Attribute Order
- For regular cells: `id`, `value`, `style`, then `parent`, then `vertex`/`edge`
- For mxGeometry: coordinates first, then `as="geometry"`

## Testing XML Generation
Before using generated XML, validate that:
1. All mxGeometry elements are properly formatted
2. Parent-child relationships are correct
3. Self-closing tags are properly formatted
4. Cell IDs and references are consistent

## Common Error Patterns
If your diagram shows as empty in DrawIO, check for these common errors:

1. **Circular Parent References**: Make sure the root cell (id="0") has NO parent attribute
2. **mxGeometry Format**: The as="geometry" attribute must be at the end of the tag
3. **Invalid Parent References**: All cells must have valid parent references
4. **Attribute Order**: Some attributes must be in a specific order

## Example of Valid DrawIO XML
```xml
<?xml version="1.0" encoding="UTF-8"?>
<mxfile host="app.diagrams.net" modified="2023-05-17T00:00:00.000Z" agent="MCP-DrawIO" version="15.0.0" type="device">
  <diagram id="simple-diagram" name="Page-1">
    <mxGraphModel dx="800" dy="800" grid="1" gridSize="10" guides="1" tooltips="1" connect="1" arrows="1" fold="1" page="1" pageScale="1" pageWidth="800" pageHeight="600" math="0" shadow="0">
      <root>
        <mxCell id="0"/>
        <mxCell id="1" parent="0"/>
        <mxCell id="2" value="Rectangle" style="rounded=0;whiteSpace=wrap;html=1;" parent="1" vertex="1">
          <mxGeometry x="200" y="200" width="120" height="60" as="geometry"/>
        </mxCell>
        <mxCell id="3" value="Ellipse" style="ellipse;whiteSpace=wrap;html=1;" parent="1" vertex="1">
          <mxGeometry x="400" y="200" width="120" height="80" as="geometry"/>
        </mxCell>
        <mxCell id="4" value="Diamond" style="rhombus;whiteSpace=wrap;html=1;" parent="1" vertex="1">
          <mxGeometry x="300" y="350" width="100" height="100" as="geometry"/>
        </mxCell>
        <mxCell id="5" style="edgeStyle=orthogonalEdgeStyle;rounded=0;orthogonalLoop=1;jettySize=auto;html=1;endArrow=classic;endFill=1;" parent="1" source="2" target="3" edge="1">
          <mxGeometry relative="1" as="geometry"/>
        </mxCell>
      </root>
    </mxGraphModel>
  </diagram>
</mxfile>
``` 

## Lessons Learned from Troubleshooting

In the process of debugging DrawIO XML issues, we've discovered several critical requirements:

1. **Parent-Child Hierarchy is Absolutely Critical**:
   - The root cell (id="0") MUST have NO parent attribute
   - The layer cell (id="1") MUST have parent="0"
   - Regular cells should have parent="1" (or to another valid cell)
   - Any circular parent references will cause diagrams to be empty

2. **XML Formatting Matters**:
   - The ordering of certain attributes affects how DrawIO interprets the XML
   - Position of the `as="geometry"` attribute at the end of mxGeometry tags is important
   - Self-closing tags should not have spaces before the slash

3. **Error Diagnosis**:
   - Empty diagrams often indicate parent reference problems
   - "Could not add object for mxGeometry" errors usually mean incorrect mxGeometry formatting
   - Creating a small test diagram and comparing it to a working example is a good way to diagnose issues

4. **Testing Strategy**:
   - Always test generated XML with a small set of elements first
   - Compare the XML structure with known working examples
   - Validate parent-child relationships when troubleshooting empty diagrams

These lessons have been incorporated into our XML generation code to ensure DrawIO compatibility. 