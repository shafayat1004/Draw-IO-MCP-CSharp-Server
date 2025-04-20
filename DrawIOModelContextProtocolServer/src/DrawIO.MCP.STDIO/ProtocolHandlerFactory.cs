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
        STDIO,
        
        /// <summary>
        /// Server-sent events protocol
        /// </summary>
        SSE
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
                case ProtocolType.STDIO:
                    return new StdioProtocolHandler(logWriter);
                case ProtocolType.SSE:
                    throw new NotImplementedException("SSE protocol handler not implemented yet");
                default:
                    throw new ArgumentException($"Unknown protocol type: {protocolType}");
            }
        }
    }
} 