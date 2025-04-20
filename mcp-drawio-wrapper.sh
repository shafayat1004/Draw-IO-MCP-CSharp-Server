#!/bin/bash

# Default diagrams directory
DIAGRAMS_DIR="${1:-/Volumes/HomeX/shafayat/Documents/Draw-IO-MCP-CSharp-Server/DrawIOModelContextProtocolServer/diagrams}"

# Create a named pipe for bidirectional communication
PIPE_DIR=$(mktemp -d)
IN_PIPE="$PIPE_DIR/in"
OUT_PIPE="$PIPE_DIR/out"
mkfifo "$IN_PIPE" "$OUT_PIPE"

# Log start with diagrams directory info
echo "Starting DrawIO MCP wrapper with diagrams directory: $DIAGRAMS_DIR" > /tmp/drawio-mcp-wrapper.log

# Start the MCP server in the background
/Volumes/HomeX/shafayat/.dotnet/dotnet run --project /Volumes/HomeX/shafayat/Documents/Draw-IO-MCP-CSharp-Server/DrawIOModelContextProtocolServer/src/DrawIO.MCP.STDIO/DrawIO.MCP.STDIO.csproj -- --diagrams-dir "$DIAGRAMS_DIR" --verbose < "$IN_PIPE" > "$OUT_PIPE" 2>/tmp/drawio-mcp-debug.log &
SERVER_PID=$!

# Handle cleanup on exit
cleanup() {
    kill $SERVER_PID 2>/dev/null
    rm -rf "$PIPE_DIR"
    exit 0
}
trap cleanup EXIT

# Main loop: Read stdin, intercept initialization requests, and forward everything else
while IFS= read -r line; do
    if [[ "$line" == *"initialize"* ]]; then
        # Send a dummy initialization response
        echo '{"jsonrpc":"2.0","id":1,"result":{"serverInfo":{"name":"DrawIO MCP Server","version":"1.0.0"},"capabilities":{"tools":true,"resources":true}}}'
    else
        # Forward other requests to the server
        echo "$line" > "$IN_PIPE"
        # Read the response from the server and forward to stdout
        if [[ -p "$OUT_PIPE" ]]; then
            cat "$OUT_PIPE"
        fi
    fi
done

cleanup 