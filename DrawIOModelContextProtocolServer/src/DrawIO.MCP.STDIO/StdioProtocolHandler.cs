using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Text.Json.Serialization;

namespace DrawIO.MCP.STDIO
{
    /// <summary>
    /// Implementation of the MCP protocol for STDIO
    /// </summary>
    public class StdioProtocolHandler : IMcpProtocolHandler
    {
        private TextWriter _logWriter;
        private McpRequestDispatcher _dispatcher;
        private string _diagramsDirectory;
        private bool _verbose;
        private JsonSerializerOptions _serializerOptions;

        public StdioProtocolHandler(TextWriter logWriter)
        {
            _logWriter = logWriter;
            _serializerOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                WriteIndented = false // Ensure compact JSON output
            };
        }

        public Task InitializeAsync(string diagramsDirectory, bool verbose)
        {
            _diagramsDirectory = diagramsDirectory;
            _verbose = verbose;
            _dispatcher = new McpRequestDispatcher(diagramsDirectory, verbose, _logWriter);
            return Task.CompletedTask;
        }

        public async Task<McpResponse> ProcessRequestAsync(string requestJson)
        {
            if (string.IsNullOrEmpty(requestJson))
            {
                LogMessage("Error: Received empty request");
                return McpResponse.CreateError("null", -32700, "Invalid empty request");
            }
            
            LogMessage($"Processing request: {requestJson}");
            
            try
            {
                var request = JsonSerializer.Deserialize<McpRequest>(requestJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                // Validate the request
                if (request == null)
                {
                    LogMessage("ERROR: Deserialized request is null");
                    return McpResponse.CreateError("null", -32700, "Invalid request format");
                }
                
                if (string.IsNullOrEmpty(request.JsonRpc) || request.JsonRpc != "2.0")
                {
                    LogMessage($"ERROR: Invalid JsonRpc version: {request.JsonRpc}");
                    return McpResponse.CreateError(request.Id ?? "null", -32600, "Invalid JsonRpc version");
                }
                
                // Check if this is a notification (no ID)
                if (request.Id == null)
                {
                    if (request.Method == "notifications/initialized")
                    {
                        LogMessage("Handling initialized notification");
                        // Don't send any response for notifications
                        return null;
                    }
                    // Handle other notifications here if needed
                    return null; // No response for notifications
                }
                
                // Dispatch the request to handler
                var response = await _dispatcher.DispatchRequestAsync(request);
                
                // Ensure we always return a valid response for non-notifications
                if (response == null)
                {
                    return McpResponse.CreateError(request.Id, -32603, "Internal error: null response from dispatcher");
                }
                
                // Ensure response has proper JsonRpc version
                response.JsonRpc = "2.0";
                
                // Ensure response has an ID (use request ID)
                response.Id = request.Id;
                
                // Log response for debugging
                if (response.Error != null)
                {
                    LogMessage($"WARNING: Returning error response: Code={response.Error.Code}, Message={response.Error.Message}");
                }
                
                return response;
            }
            catch (JsonException ex)
            {
                LogMessage($"ERROR: Error parsing request JSON: {ex.Message}");
                LogMessage($"ERROR: Stack trace: {ex.StackTrace}");
                return McpResponse.CreateError("null", -32700, $"Parse error: {ex.Message}");
            }
            catch (Exception ex)
            {
                LogMessage($"ERROR: Error processing request: {ex.Message}");
                LogMessage($"ERROR: Stack trace: {ex.StackTrace}");
                
                // Get error details if available
                object errorDetails = null;
                if (ex.Data.Contains("details"))
                {
                    errorDetails = ex.Data["details"];
                }
                
                // Default error code
                int errorCode = -32603; // Internal error
                
                // Create properly formatted error response
                return McpResponse.CreateError(
                    "null", 
                    errorCode, 
                    $"Internal error: {ex.Message}",
                    errorDetails
                );
            }
        }

        public void SendErrorResponse(string errorMessage, string id = null)
        {
            try
            {
                var errorResponse = McpResponse.CreateError(id ?? "null", -32603, errorMessage);
                var responseJson = JsonSerializer.Serialize(errorResponse, _serializerOptions);
                
                // Ensure the response is valid JSON before sending
                using (JsonDocument.Parse(responseJson))
                {
                    Console.Out.WriteLine(responseJson);
                    Console.Out.Flush();
                    LogMessage($"Sent error response: {responseJson}");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"ERROR: Failed to send error response: {ex.Message}");
                // Don't try to send another error response here to avoid potential infinite loop
            }
        }
        
        private void LogMessage(string message)
        {
            try
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                _logWriter.WriteLine($"[{timestamp}] [STDIO] {message}");
                _logWriter.Flush(); // Ensure log is written immediately
            }
            catch (Exception ex)
            {
                // Last-resort attempt to log the failure
                try { Console.Error.WriteLine($"Logging failed: {ex.Message}"); } catch { }
            }
        }
    }
} 