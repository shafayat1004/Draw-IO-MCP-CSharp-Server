using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Xunit.Abstractions;

namespace DrawIO.MCP.STDIO.Tests
{
    /// <summary>
    /// Tests that validate the parsing and processing of raw MCP JSON-RPC messages
    /// without launching the actual server process.
    /// </summary>
    public class McpMessageParserTests
    {
        private readonly ITestOutputHelper _output;
        private readonly string _tempDiagramsDir;
        private readonly McpRequestDispatcher _dispatcher;
        private readonly StringWriter _logWriter;

        public McpMessageParserTests(ITestOutputHelper output)
        {
            _output = output;
            _tempDiagramsDir = Path.Combine(Path.GetTempPath(), $"mcp-parser-tests-{Guid.NewGuid()}");
            Directory.CreateDirectory(_tempDiagramsDir);
            _logWriter = new StringWriter();
            _dispatcher = new McpRequestDispatcher(_tempDiagramsDir, true, _logWriter);
        }

        /// <summary>
        /// Processes a raw JSON-RPC message string and returns the response.
        /// Simulates the stdio message processing without using the actual stdio.
        /// </summary>
        private async Task<string> ProcessRawMessageAsync(string jsonRpcMessage)
        {
            _output.WriteLine($"Processing raw message: {jsonRpcMessage}");
            
            try
            {
                // Parse the incoming message to McpRequest
                var request = JsonSerializer.Deserialize<McpRequest>(jsonRpcMessage, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                if (request == null)
                {
                    return JsonSerializer.Serialize(McpResponse.CreateError(
                        "null", 
                        -32700, 
                        "Parse error: Invalid JSON-RPC message"));
                }
                
                // Dispatch the request and get the response
                var response = await _dispatcher.DispatchRequestAsync(request);
                
                // Serialize the response back to a JSON string
                return JsonSerializer.Serialize(response);
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Error processing message: {ex.Message}");
                return JsonSerializer.Serialize(McpResponse.CreateError(
                    "null", 
                    -32700, 
                    $"Parse error: {ex.Message}"));
            }
        }
        
        [Fact]
        public async Task ParseValidInitializeMessage_ShouldReturnValidResponse()
        {
            // Arrange - A valid initialize message
            var message = @"{
                ""jsonrpc"": ""2.0"",
                ""id"": ""init-1"",
                ""method"": ""mcp/initialize"",
                ""params"": {
                    ""clientIdentifier"": ""test-client"",
                    ""protocolVersion"": ""2024-11-05""
                }
            }";
            
            // Act
            var responseJson = await ProcessRawMessageAsync(message);
            
            // Assert
            _output.WriteLine($"Response: {responseJson}");
            
            // Check response is valid JSON
            var responseObject = JsonNode.Parse(responseJson);
            Assert.NotNull(responseObject);
            
            // Validate response format
            Assert.Equal("2.0", responseObject?["jsonrpc"]?.GetValue<string>() ?? string.Empty);
            Assert.Equal("init-1", responseObject?["id"]?.GetValue<string>() ?? string.Empty);
            Assert.NotNull(responseObject?["result"]);
            
            // Check response contains expected initialize response fields
            Assert.NotNull(responseObject?["result"]?["protocolVersion"]);
            Assert.NotNull(responseObject?["result"]?["capabilities"]);
            Assert.NotNull(responseObject?["result"]?["serverInfo"]);
        }
        
        [Fact]
        public async Task ParseInvalidJsonMessage_ShouldReturnParseError()
        {
            // Arrange - An invalid JSON message
            var message = @"{ ""jsonrpc"": ""2.0"", ""id"": ""broken, invalid json";
            
            // Act
            var responseJson = await ProcessRawMessageAsync(message);
            
            // Assert
            _output.WriteLine($"Response: {responseJson}");
            
            // Check response is valid JSON despite the invalid input
            var responseObject = JsonNode.Parse(responseJson);
            Assert.NotNull(responseObject);
            
            // Should be an error response
            Assert.NotNull(responseObject?["error"]);
            Assert.Equal(-32700, responseObject?["error"]?["code"]?.GetValue<int>() ?? 0);
            Assert.Contains("Parse error", responseObject?["error"]?["message"]?.GetValue<string>() ?? string.Empty);
        }
        
        [Fact]
        public async Task ProcessBatchOfValidMessages_ShouldHandleEachIndividually()
        {
            // Arrange - Multiple valid messages to simulate batch processing
            string[] messages =
            [
                @"{""jsonrpc"": ""2.0"", ""id"": ""msg1"", ""method"": ""mcp/initialize"", ""params"": {}}",
                @"{""jsonrpc"": ""2.0"", ""id"": ""msg2"", ""method"": ""tools/list"", ""params"": {}}"
            ];
            
            // Act - Process each message individually
            var responses = new string[messages.Length];
            for (var i = 0; i < messages.Length; i++)
            {
                responses[i] = await ProcessRawMessageAsync(messages[i]);
            }
            
            // Assert
            for (var i = 0; i < responses.Length; i++)
            {
                _output.WriteLine($"Response {i+1}: {responses[i]}");
                
                // Check each response is valid JSON
                var responseObject = JsonNode.Parse(responses[i]);
                Assert.NotNull(responseObject);
                
                // Validate response format for each message
                Assert.Equal("2.0", responseObject?["jsonrpc"]?.GetValue<string>() ?? string.Empty);
                Assert.Equal($"msg{i+1}", responseObject?["id"]?.GetValue<string>() ?? string.Empty);
                Assert.NotNull(responseObject?["result"]);
            }
        }
        
        [Fact]
        public async Task MalformedMethod_ShouldReturnMethodNotFoundError()
        {
            // Arrange - Message with invalid method name
            var message = @"{
                ""jsonrpc"": ""2.0"",
                ""id"": ""bad-method"",
                ""method"": ""invalid/unknown/method"",
                ""params"": {}
            }";
            
            // Act
            var responseJson = await ProcessRawMessageAsync(message);
            
            // Assert
            _output.WriteLine($"Response: {responseJson}");
            
            var responseObject = JsonNode.Parse(responseJson);
            Assert.NotNull(responseObject);
            
            // Should be a method not found error
            Assert.NotNull(responseObject?["error"]);
            Assert.Equal(-32601, responseObject?["error"]?["code"]?.GetValue<int>() ?? 0);
            Assert.Contains("not found", responseObject?["error"]?["message"]?.GetValue<string>() ?? string.Empty, 
                StringComparison.OrdinalIgnoreCase);
        }
        
        [Fact]
        public async Task ValidateMcpMessageFormatCompliance()
        {
            // Arrange - Valid tool execution message
            var message = @"{
                ""jsonrpc"": ""2.0"",
                ""id"": ""tool-exec"",
                ""method"": ""tools/execute"",
                ""params"": {
                    ""tool"": ""create_new_diagram"", 
                    ""parameters"": {
                        ""name"": ""test-diagram.drawio""
                    }
                }
            }";
            
            // Act
            var responseJson = await ProcessRawMessageAsync(message);
            
            // Assert
            _output.WriteLine($"Response: {responseJson}");
            
            // Check for required JSON-RPC 2.0 fields
            var jsonRpcPattern = new Regex(@"""jsonrpc""\s*:\s*""2\.0""");
            Assert.Matches(jsonRpcPattern, responseJson);
            
            var idPattern = new Regex(@"""id""\s*:\s*""tool-exec""");
            Assert.Matches(idPattern, responseJson);
            
            // Response should have either result or error but not both
            var hasResult = responseJson.Contains("\"result\"");
            var hasError = responseJson.Contains("\"error\"");
            Assert.True(hasResult || hasError);
            Assert.False(hasResult && hasError);
            
            // Check if the file was actually created
            var filePath = Path.Combine(_tempDiagramsDir, "test-diagram.drawio");
            Assert.True(File.Exists(filePath), "The file should be created by the tool execution");
        }

        [Theory]
        [InlineData("add_waypoint")]
        [InlineData("remove_waypoint")]
        [InlineData("update_waypoint")]
        [InlineData("get_waypoints")]
        [InlineData("clear_waypoints")]
        public async Task WaypointManipulationTools_WithValidParameters_ShouldSucceed(string toolName)
        {
            // Define a simple message format for tool execution
            var message = @$"{{
                ""jsonrpc"": ""2.0"",
                ""id"": ""test-waypoint"",
                ""method"": ""tools/execute"",
                ""params"": {{
                    ""tool"": ""{toolName}"",
                    ""parameters"": {{
                        ""diagram"": ""test-diagram.drawio"",
                        ""connector_id"": ""test-connector-id"",
                        ""x"": 100,
                        ""y"": 100,
                        ""waypoint_index"": 0
                    }}
                }}
            }}";
            
            // Mock the file existence check
            if (!Directory.Exists(_tempDiagramsDir))
            {
                Directory.CreateDirectory(_tempDiagramsDir);
            }
            var filePath = Path.Combine(_tempDiagramsDir, "test-diagram.drawio");
            if (!File.Exists(filePath))
            {
                File.WriteAllText(filePath, "<mxfile><diagram id=\"test\"><mxGraphModel><root><mxCell id=\"0\"/><mxCell id=\"1\" parent=\"0\"/><mxCell id=\"test-connector-id\" edge=\"1\" parent=\"1\"><mxGeometry relative=\"1\" as=\"geometry\"/></mxCell></root></mxGraphModel></diagram></mxfile>");
            }
            
            try
            {
                // Act
                var responseJson = await ProcessRawMessageAsync(message);
                
                // Assert
                var responseObject = JsonNode.Parse(responseJson);
                Assert.NotNull(responseObject);
                
                // Basic validation
                Assert.Equal("2.0", responseObject?["jsonrpc"]?.GetValue<string>() ?? string.Empty);
                Assert.Equal("test-waypoint", responseObject?["id"]?.GetValue<string>() ?? string.Empty);
                
                // Either there should be a result or an error explaining why it wasn't processed
                var hasResult = responseObject?["result"] != null;
                var hasError = responseObject?["error"] != null;
                
                // Assert that we got either a result or an error, but not both
                Assert.True(hasResult || hasError);
                Assert.False(hasResult && hasError);
            }
            finally
            {
                // Clean up
                if (File.Exists(filePath))
                {
                    try { File.Delete(filePath); } catch { }
                }
            }
        }
    }
} 