using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;
using System.Text;
using System.Threading;
using System.CommandLine;
using Microsoft.FSharp.Core;
using static DrawIO.MCP.STDIO.FileOperations;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace DrawIO.MCP.STDIO
{
    public class Program
    {
        private static string _diagramsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "diagrams");
        private static TextWriter _logWriter = Console.Error;
        private static ProtocolType _protocolType = ProtocolType.STDIO;

        public static async Task<int> Main(string[] args)
        {
            try
            {
                // Configure console streams for UTF-8 encoding and disable buffering
                Console.InputEncoding = Encoding.UTF8;
                Console.OutputEncoding = Encoding.UTF8;
                
                // Ensure stdout is not buffered
                var stdout = Console.OpenStandardOutput();
                var writer = new StreamWriter(stdout) { AutoFlush = true };
                Console.SetOut(writer);

                AppDomain.CurrentDomain.ProcessExit += (sender, e) => 
                {
                    var writer = _logWriter ?? Console.Error;
                    writer.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] Process exit event triggered");
                };

                var rootCommand = new RootCommand("DrawIO MCP Server");
                
                var diagramsDirOption = new Option<string>(
                    "--diagrams-dir",
                    description: "Directory to store diagram files",
                    getDefaultValue: () => _diagramsDirectory);
                
                var verboseOption = new Option<bool>(
                    "--verbose",
                    description: "Enable verbose logging");
                    
                var logFileOption = new Option<string>(
                    "--log-file",
                    description: "File to write logs to (stderr if not specified)");
                
                var protocolOption = new Option<string>(
                    "--protocol",
                    description: "Protocol to use (stdio, sse)",
                    getDefaultValue: () => "stdio");
                
                rootCommand.AddOption(diagramsDirOption);
                rootCommand.AddOption(verboseOption);
                rootCommand.AddOption(logFileOption);
                rootCommand.AddOption(protocolOption);

                rootCommand.SetHandler(async (string diagramsDir, bool verbose, string logFile, string protocol) =>
                {
                    _diagramsDirectory = diagramsDir;
                    
                    if (!Directory.Exists(_diagramsDirectory))
                    {
                        Directory.CreateDirectory(_diagramsDirectory);
                    }
                    
                    // Set up log file if specified
                    if (!string.IsNullOrEmpty(logFile))
                    {
                        try 
                        {
                            // Create directory if it doesn't exist
                            string logDir = Path.GetDirectoryName(logFile);
                            if (!string.IsNullOrEmpty(logDir) && !Directory.Exists(logDir))
                            {
                                Directory.CreateDirectory(logDir);
                            }
                            
                            _logWriter = new StreamWriter(logFile, true) { AutoFlush = true };
                            LogMessage($"Logs being written to: {logFile}");
                        }
                        catch (Exception ex)
                        {
                            Console.Error.WriteLine($"Failed to create log file: {ex.Message}. Using stderr for logging.");
                        }
                    }

                    // Determine protocol type
                    _protocolType = protocol.ToLower() switch
                    {
                        "stdio" => ProtocolType.STDIO,
                        "sse" => ProtocolType.SSE,
                        _ => ProtocolType.STDIO
                    };
                    
                    LogMessage($"Using protocol: {_protocolType}");

                    // Check standard streams for STDIO protocol
                    if (_protocolType == ProtocolType.STDIO)
                    {
                        LogMessage("Checking stdin stream...");
                        if (Console.IsInputRedirected)
                        {
                            LogMessage("Stdin is redirected - OK");
                        }
                        else
                        {
                            LogMessage("WARNING: stdin is not redirected, may cause issues with MCP protocol");
                        }

                        LogMessage("Checking stdout stream...");
                        if (Console.IsOutputRedirected)
                        {
                            LogMessage("Stdout is redirected - OK");
                        }
                        else
                        {
                            LogMessage("WARNING: stdout is not redirected, may cause issues with MCP protocol");
                        }
                    }

                    await RunServer(verbose);
                    
                    // Close log file if we opened one
                    if (_logWriter != Console.Error)
                    {
                        LogMessage("Closing log file writer");
                        _logWriter.Close();
                    }
                    
                    // Add a delay before exiting to ensure logs are written
                    LogMessage("Server execution completed, waiting before exit...");
                    await Task.Delay(1000);
                }, diagramsDirOption, verboseOption, logFileOption, protocolOption);

                return await rootCommand.InvokeAsync(args);
            }
            catch (Exception ex)
            {
                try
                {
                    var writer = _logWriter ?? Console.Error;
                    writer.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] CRITICAL EXCEPTION IN MAIN: {ex.Message}");
                    writer.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] Stack trace: {ex.StackTrace}");
                }
                catch
                {
                    // Last resort if logging fails
                    Console.Error.WriteLine($"Critical error: {ex.Message}");
                }
                return 1;
            }
        }

        private static void LogMessage(string message)
        {
            try
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                _logWriter.WriteLine($"[{timestamp}] {message}");
                _logWriter.Flush(); // Ensure log is written immediately
            }
            catch (Exception ex)
            {
                // Last-resort attempt to log the failure
                try { Console.Error.WriteLine($"Logging failed: {ex.Message}"); } catch { }
            }
        }

        private static async Task RunServer(bool verbose)
        {
            LogMessage($"DrawIO MCP Server starting (PID: {Environment.ProcessId}, Thread: {Thread.CurrentThread.ManagedThreadId})");
            LogMessage($"Using diagrams directory: {_diagramsDirectory}");
            
            // Start a heartbeat thread to periodically log server status
            var heartbeatCts = new CancellationTokenSource();
            var heartbeatTask = Task.Run(async () => {
                try
                {
                    var counter = 0;
                    while (!heartbeatCts.Token.IsCancellationRequested)
                    {
                        counter++;
                        LogMessage($"SERVER HEARTBEAT #{counter} (Thread: {Thread.CurrentThread.ManagedThreadId})");
                        
                        // Check standard streams
                        bool stdinOk = Console.In != null;
                        bool stdoutOk = Console.Out != null;
                        LogMessage($"HEARTBEAT DETAIL: stdin={stdinOk}, stdout={stdoutOk}, GC.TotalMemory={GC.GetTotalMemory(false) / (1024*1024)}MB");
                        
                        try
                        {
                            await Task.Delay(30000, heartbeatCts.Token); // 30-second intervals
                        }
                        catch (TaskCanceledException)
                        {
                            // Normal when cancellation is requested
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogMessage($"ERROR in heartbeat thread: {ex.Message}");
                }
                finally
                {
                    LogMessage("Heartbeat thread ending");
                }
            }, heartbeatCts.Token);
            
            // Set up console cancellation
            Console.CancelKeyPress += (sender, e) => 
            {
                LogMessage("Cancel key detected. Starting graceful shutdown.");
                e.Cancel = true; // Prevent default behavior
            };

            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                LogMessage($"CRITICAL: Unhandled exception: {ex?.Message}");
                LogMessage($"CRITICAL: Stack trace: {ex?.StackTrace}");
                LogMessage($"CRITICAL: Inner exception: {ex?.InnerException?.Message}");
                LogMessage($"CRITICAL: IsTerminating: {args.IsTerminating}");
            };

            using var cancellationTokenSource = new CancellationTokenSource();

            try
            {
                // Create the appropriate protocol handler
                IMcpProtocolHandler protocolHandler = ProtocolHandlerFactory.CreateHandler(_protocolType, _logWriter);
                
                // Initialize the protocol handler
                await protocolHandler.InitializeAsync(_diagramsDirectory, verbose);
                
                // Run the appropriate protocol handler
                if (_protocolType == ProtocolType.STDIO)
                {
                    await RunStdioServer(protocolHandler, cancellationTokenSource.Token);
                }
                else
                {
                    LogMessage($"Protocol {_protocolType} is not implemented yet");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"FATAL ERROR: {ex.Message}");
                LogMessage($"FATAL ERROR: Stack trace: {ex.StackTrace}");
                LogMessage($"FATAL ERROR: Inner exception: {ex.InnerException?.Message ?? "none"}");
            }
            finally
            {
                // Cancel and wait for the heartbeat thread
                LogMessage("Cancelling heartbeat thread");
                heartbeatCts.Cancel();
                try 
                {
                    await heartbeatTask;
                    LogMessage("Heartbeat thread cancelled successfully");
                }
                catch (Exception ex)
                {
                    LogMessage($"Error waiting for heartbeat task: {ex.Message}");
                }
                
                LogMessage("DrawIO MCP Server shutting down");
            }
        }

        private static async Task RunStdioServer(IMcpProtocolHandler protocolHandler, CancellationToken cancellationToken)
        {
            // Keep track of whether stdin/stdout are still valid
            bool stdioValid = true;
            int consecutiveErrors = 0;
            const int MAX_CONSECUTIVE_ERRORS = 3;

            try
            {
                // Verify that Console.In and Console.Out are available
                if (Console.In == null || Console.Out == null)
                {
                    LogMessage("ERROR: Console.In or Console.Out is null - cannot run STDIO server");
                    return;
                }

                LogMessage("Starting STDIO protocol server");

                // Main processing loop
                while (!cancellationToken.IsCancellationRequested && stdioValid)
                {
                    try
                    {
                        LogMessage($"Waiting for next input line... (Thread: {Thread.CurrentThread.ManagedThreadId})");
                        
                        // Read the next line with a timeout to detect broken pipes
                        string line = null;
                        try
                        {
                            line = await Console.In.ReadLineAsync();
                        }
                        catch (Exception ex)
                        {
                            LogMessage($"ERROR reading from stdin: {ex.Message}");
                            LogMessage($"Stack trace: {ex.StackTrace}");
                            consecutiveErrors++;
                            
                            if (consecutiveErrors >= MAX_CONSECUTIVE_ERRORS)
                            {
                                LogMessage($"Too many consecutive errors ({consecutiveErrors}), shutting down server");
                                stdioValid = false;
                                break;
                            }
                            
                            // Wait a bit before retrying
                            await Task.Delay(1000);
                            continue;
                        }
                        
                        // Skip empty lines and log appropriately
                        if (string.IsNullOrEmpty(line))
                        {
                            LogMessage("Empty line received, continuing...");
                            continue;
                        }

                        // Try to parse as JSON first
                        try
                        {
                            using var doc = JsonDocument.Parse(line);
                            var method = doc.RootElement.GetProperty("method").GetString();
                            LogMessage($"Read input line: {method} request received");

                            // Check for shutdown request
                            if (method == "mcp/shutdown")
                            {
                                LogMessage("Shutdown request received, initiating graceful shutdown");
                                stdioValid = false;
                                break;
                            }
                        }
                        catch (JsonException)
                        {
                            // If it's not valid JSON, log and skip
                            LogMessage($"Skipping non-JSON input: {line.Substring(0, Math.Min(20, line.Length))}...");
                            continue;
                        }

                        // Process the request using the protocol handler
                        var response = await protocolHandler.ProcessRequestAsync(line);
                        
                        // Only send response if it's not null (i.e., not a notification)
                        if (response != null)
                        {
                            var responseJson = JsonSerializer.Serialize(response, new JsonSerializerOptions
                            {
                                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                                WriteIndented = false // Ensure compact JSON
                            });
                            
                            try
                            {
                                await WriteResponseAsync(responseJson);
                                LogMessage($"Response sent for request {response.Id}.");
                            }
                            catch (Exception ex)
                            {
                                LogMessage($"ERROR writing to stdout: {ex.Message}");
                                LogMessage($"Stack trace: {ex.StackTrace}");
                                consecutiveErrors++;
                                
                                if (consecutiveErrors >= MAX_CONSECUTIVE_ERRORS)
                                {
                                    LogMessage($"Too many consecutive errors ({consecutiveErrors}), shutting down server");
                                    stdioValid = false;
                                    break;
                                }
                                
                                // Wait a bit before retrying
                                await Task.Delay(1000);
                                continue;
                            }
                        }
                        else
                        {
                            // For notifications, just log that we processed it
                            LogMessage("Notification processed (no response needed)");
                        }
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"CRITICAL: Error in main processing loop: {ex.Message}");
                        LogMessage($"CRITICAL: Stack trace: {ex.StackTrace}");
                        LogMessage($"CRITICAL: Inner exception: {ex.InnerException?.Message ?? "none"}");
                        
                        consecutiveErrors++;
                        if (consecutiveErrors >= MAX_CONSECUTIVE_ERRORS)
                        {
                            LogMessage($"Too many consecutive errors ({consecutiveErrors}), shutting down server");
                            stdioValid = false;
                            break;
                        }
                        
                        // Wait a bit before retrying
                        await Task.Delay(1000);
                        LogMessage("Attempting to continue processing...");
                    }
                }
                
                if (!stdioValid)
                {
                    LogMessage("CRITICAL: Standard IO became invalid, shutting down server");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"FATAL ERROR in STDIO server: {ex.Message}");
                LogMessage($"FATAL ERROR: Stack trace: {ex.StackTrace}");
                LogMessage($"FATAL ERROR: Inner exception: {ex.InnerException?.Message ?? "none"}");
            }
        }

        private static async Task WriteResponseAsync(string responseJson)
        {
            try
            {
                // Write just the JSON response with a newline
                await Console.Out.WriteLineAsync(responseJson);
                await Console.Out.FlushAsync();
                
                LogMessage($"Response sent: {responseJson}");
            }
            catch (Exception ex)
            {
                LogMessage($"ERROR writing response: {ex.Message}");
                throw;
            }
        }
    }
}