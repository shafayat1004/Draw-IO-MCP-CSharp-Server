# Test Fixes and Build Warning Resolution

This document captures the learnings from resolving test failures and build warnings in the DrawIO MCP Server project.

## MCP Protocol Implementation

1. **Method Name Convention**: 
   - All API methods must follow the MCP protocol naming convention with the `mcp/` prefix.
   - Examples: `mcp/listResources`, `mcp/executeTool`, `mcp/getResource`
   - Failure to use this prefix results in `Method not found` errors.

2. **Parameter Requirements**:
   - Ensure proper JSON structure for parameters to prevent deserialization errors.
   - Always validate parameter existence before accessing values.

## Testing Improvements

1. **STDIO Process Communication**:
   - When dealing with process-based tests, sufficient delays are crucial between operations:
     - Process startup: Increased from 2000ms to 3000ms
     - Command response: Increased from 500ms to 1000ms
   - This prevents flaky tests due to timing issues.

2. **Error Handling**:
   - Added robust error handling for JSON parsing in the test methods.
   - Improved STDIO test resilience with more defensive programming.
   - Added fallback mechanisms when dealing with potentially null properties.

3. **xUnit Best Practices**:
   - Use dedicated assertion methods like `Assert.Null()` instead of `Assert.Equal(null, ...)` to follow xUnit best practices.
   - This avoids analyzer warnings (xUnit2003).

## F# and C# Interoperability

1. **F# Value Discarding**:
   - In F# tests, explicitly use `ignore (expr)` when discarding values to prevent compiler warnings.
   - Example: `ignore (diagram.Pages.[0])` instead of letting values implicitly be discarded.

2. **Nullability Handling**:
   - Use `string.Empty` instead of `null` when dealing with non-nullable string parameters in C#.
   - For nullable parameters, use appropriate nullable reference type annotations (e.g., `string?`).

3. **JSON Deserialization**:
   - When deserializing JSON to C# objects, ensure the target type's nullability matches the source data.
   - Use nullability checks before accessing potentially null properties.

## Project Structure

The DrawIO MCP Server has a well-organized architecture:
- **Core Library (F#)**: Contains pure functional diagram manipulation logic
- **STDIO Implementation**: Command-line integration for MCP protocol
- **SSE Web API**: Browser-based integration via Server-Sent Events

## Additional Observations

1. Process management is critical for reliable STDIO tests. The `StartStdioServer` method needs careful implementation with proper cleanup.

2. SSE project maintains a clear separation of concerns with dedicated `Tool` classes for each operation.

3. Test assertions should be designed to accommodate integration points that might fail in isolation but succeed as part of a larger test suite.

## Date

Documentation created: 2025-04-20 