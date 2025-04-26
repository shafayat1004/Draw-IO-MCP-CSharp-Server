using System.Threading.Tasks;

namespace DrawIO.MCP.STDIO
{
    /// <summary>
    /// Interface for MCP protocol handlers
    /// </summary>
    public interface IMcpProtocolHandler
    {
        /// <summary>
        /// Initialize the protocol handler
        /// </summary>
        Task InitializeAsync(string diagramsDirectory, bool verbose);
        
        /// <summary>
        /// Process an incoming request
        /// </summary>
        Task<McpResponse> ProcessRequestAsync(string requestJson);
        
        /// <summary>
        /// Send an error response
        /// </summary>
        void SendErrorResponse(string errorMessage, string id = null);
    }
} 