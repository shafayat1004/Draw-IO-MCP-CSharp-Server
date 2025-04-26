using System;
using System.IO;

namespace DrawIO.MCP.STDIO
{
    /// <summary>
    /// Protocol handler types supported by the server
    /// </summary>
    public enum ProtocolType
    {
        /// <summary>
        /// Standard input/output protocol
        /// </summary>
        Stdio,
        
        /// <summary>
        /// Server-sent events protocol
        /// </summary>
        Sse
    }
    
    /// <summary>
    /// Factory for creating protocol handlers
    /// </summary>
    public static class ProtocolHandlerFactory
    {
        /// <summary>
        /// Create a protocol handler of the specified type
        /// </summary>
        public static IMcpProtocolHandler CreateHandler(ProtocolType protocolType, TextWriter logWriter)
        {
            switch (protocolType)
            {
                case ProtocolType.Stdio:
                    return new StdioProtocolHandler(logWriter);
                case ProtocolType.Sse:
                    throw new NotImplementedException("SSE protocol handler not implemented yet");
                default:
                    throw new ArgumentException($"Unknown protocol type: {protocolType}");
            }
        }
    }
} 