using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace DrawIO.MCP.STDIO
{
    public class Program
    {
        private static string _diagramsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "diagrams");
        private static TextWriter _logWriter = Console.Error;
        private static ProtocolType _protocolType = ProtocolType.Stdio;
        private static string _logFilePath;
        private static long _maxLogSizeBytes = 100 * 1024 * 1024; // 100 MB
        private static int _maxLogFiles = 3;
        private static long _currentLogSize;

        public static async Task<int> Main(string[] args)
        {
            try
            {
                // Handle the case when --log-file is concatenated with its value
                args = PreprocessArguments(args);

                // Configure console streams for UTF-8 encoding and disable buffering
                Console.InputEncoding = Encoding.UTF8;
                Console.OutputEncoding = Encoding.UTF8;
                
                // Ensure stdout is not buffered
                var stdout = Console.OpenStandardOutput();
                var writer = new StreamWriter(stdout) { AutoFlush = true };
                Console.SetOut(writer);

                AppDomain.CurrentDomain.ProcessExit += (_, _) => 
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
                
                var maxLogSizeOption = new Option<long>(
                    "--max-log-size",
                    description: "Maximum size of log file in MB before rotation",
                    getDefaultValue: () => _maxLogSizeBytes / (1024 * 1024));
                    
                var maxLogFilesOption = new Option<int>(
                    "--max-log-files",
                    description: "Maximum number of log files to keep",
                    getDefaultValue: () => _maxLogFiles);
                
                var protocolOption = new Option<string>(
                    "--protocol",
                    description: "Protocol to use (stdio, sse)",
                    getDefaultValue: () => "stdio");
                
                rootCommand.AddOption(diagramsDirOption);
                rootCommand.AddOption(verboseOption);
                rootCommand.AddOption(logFileOption);
                rootCommand.AddOption(maxLogSizeOption);
                rootCommand.AddOption(maxLogFilesOption);
                rootCommand.AddOption(protocolOption);

                rootCommand.SetHandler(async (diagramsDir, verbose, logFile, maxLogSizeMb, maxLogFiles, protocol) =>
                {
                    _diagramsDirectory = diagramsDir;
                    _maxLogSizeBytes = maxLogSizeMb * 1024 * 1024;
                    _maxLogFiles = maxLogFiles;
                    
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
                            var logDir = Path.GetDirectoryName(logFile);
                            if (!string.IsNullOrEmpty(logDir) && !Directory.Exists(logDir))
                            {
                                Directory.CreateDirectory(logDir);
                            }
                            
                            _logFilePath = logFile;
                            
                            // Check the current size of the log file if it exists
                            if (File.Exists(_logFilePath))
                            {
                                var fileInfo = new FileInfo(_logFilePath);
                                _currentLogSize = fileInfo.Length;
                                
                                // If the file is already over the limit, rotate it before starting
                                if (_currentLogSize > _maxLogSizeBytes)
                                {
                                    Console.Error.WriteLine($"Log file already exceeds maximum size ({_maxLogSizeBytes / (1024 * 1024)} MB), rotating...");
                                    // Create a temporary StreamWriter to use during rotation
                                    _logWriter = new StreamWriter(_logFilePath, true) { AutoFlush = true };
                                    RotateLogFile();
                                }
                                else
                                {
                                    _logWriter = new StreamWriter(_logFilePath, true) { AutoFlush = true };
                                }
                            }
                            else
                            {
                                _logWriter = new StreamWriter(_logFilePath, true) { AutoFlush = true };
                                _currentLogSize = 0;
                            }
                            
                            LogMessage($"Logs being written to: {logFile} (Max size: {_maxLogSizeBytes / (1024 * 1024)} MB, Max files: {_maxLogFiles})");
                        }
                        catch (Exception ex)
                        {
                            Console.Error.WriteLine($"Failed to create log file: {ex.Message}. Using stderr for logging.");
                        }
                    }

                    // Determine protocol type
                    _protocolType = protocol.ToLower() switch
                    {
                        "stdio" => ProtocolType.Stdio,
                        "sse" => ProtocolType.Sse,
                        _ => ProtocolType.Stdio
                    };
                    
                    LogMessage($"Using protocol: {_protocolType}");

                    // Check standard streams for STDIO protocol
                    if (_protocolType == ProtocolType.Stdio)
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
                }, diagramsDirOption, verboseOption, logFileOption, maxLogSizeOption, maxLogFilesOption, protocolOption);

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

        private static string[] PreprocessArguments(string[] args)
        {
            var processedArgs = new List<string>();
            
            for (var i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                
                // Check for arguments where option and value are concatenated
                if (arg.StartsWith("--log-file") && !arg.Equals("--log-file"))
                {
                    // Split into option and value
                    processedArgs.Add("--log-file");
                    processedArgs.Add(arg.Substring(10)); // Remove "--log-file" prefix
                    
                    // Log the transformation for debugging
                    Console.Error.WriteLine($"Split argument '{arg}' into '--log-file' and '{arg.Substring(10)}'");
                }
                // Check for diagrams-dir with concatenated value
                else if (arg.StartsWith("--diagrams-dir") && !arg.Equals("--diagrams-dir"))
                {
                    processedArgs.Add("--diagrams-dir");
                    processedArgs.Add(arg.Substring(14)); // Remove "--diagrams-dir" prefix
                    
                    Console.Error.WriteLine($"Split argument '{arg}' into '--diagrams-dir' and '{arg.Substring(14)}'");
                }
                // Check for protocol with concatenated value
                else if (arg.StartsWith("--protocol") && !arg.Equals("--protocol"))
                {
                    processedArgs.Add("--protocol");
                    processedArgs.Add(arg.Substring(10)); // Remove "--protocol" prefix
                    
                    Console.Error.WriteLine($"Split argument '{arg}' into '--protocol' and '{arg.Substring(10)}'");
                }
                else
                {
                    // Pass through other arguments unchanged
                    processedArgs.Add(arg);
                }
            }
            
            // Log the processed arguments for debugging
            Console.Error.WriteLine("Processed arguments:");
            for (var i = 0; i < processedArgs.Count; i++)
            {
                Console.Error.WriteLine($"  [{i}]: {processedArgs[i]}");
            }
            
            return processedArgs.ToArray();
        }

        private static void LogMessage(string message)
        {
            try
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                var logLine = $"[{timestamp}] {message}";
                _logWriter.WriteLine(logLine);
                _logWriter.Flush(); // Ensure log is written immediately
                
                // If we're writing to a file, track the size
                if (_logWriter != Console.Error && _logFilePath != null)
                {
                    // Add the size of the log line plus newline characters to the current log size
                    _currentLogSize += Encoding.UTF8.GetByteCount(logLine) + Environment.NewLine.Length;
                    
                    // Check if we need to rotate
                    if (_currentLogSize > _maxLogSizeBytes)
                    {
                        RotateLogFile();
                    }
                }
            }
            catch (Exception ex)
            {
                // Last-resort attempt to log the failure
                try { Console.Error.WriteLine($"Logging failed: {ex.Message}"); } catch { }
            }
        }
        
        private static void RotateLogFile()
        {
            try
            {
                // Close the current log writer
                _logWriter.Close();
                
                // Perform log rotation - shift files
                for (var i = _maxLogFiles - 1; i > 0; i--)
                {
                    var oldFile = $"{_logFilePath}.{i}";
                    var newFile = $"{_logFilePath}.{i + 1}";
                    
                    if (File.Exists(oldFile))
                    {
                        if (i == _maxLogFiles - 1)
                        {
                            // Delete the oldest log file
                            File.Delete(oldFile);
                        }
                        else
                        {
                            // Rename file to the next index
                            if (File.Exists(newFile))
                                File.Delete(newFile);
                            File.Move(oldFile, newFile);
                        }
                    }
                }
                
                // Rename the current log file
                if (File.Exists(_logFilePath))
                {
                    var newFile = $"{_logFilePath}.1";
                    if (File.Exists(newFile))
                        File.Delete(newFile);
                    File.Move(_logFilePath, newFile);
                }
                
                // Create a new log file
                _logWriter = new StreamWriter(_logFilePath, true) { AutoFlush = true };
                _currentLogSize = 0;
                
                // Log the rotation
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                _logWriter.WriteLine($"[{timestamp}] Log file rotated due to size limit ({_maxLogSizeBytes / (1024 * 1024)} MB) being reached");
                _logWriter.Flush();
            }
            catch (Exception ex)
            {
                // If rotation fails, revert to stderr
                _logWriter = Console.Error;
                Console.Error.WriteLine($"Log rotation failed: {ex.Message}. Reverting to stderr for logging.");
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
                        var stdinOk = Console.In != null;
                        var stdoutOk = Console.Out != null;
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
            Console.CancelKeyPress += (_, e) => 
            {
                LogMessage("Cancel key detected. Starting graceful shutdown.");
                e.Cancel = true; // Prevent default behavior
            };

            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
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
                var protocolHandler = ProtocolHandlerFactory.CreateHandler(_protocolType, _logWriter);
                
                // Initialize the protocol handler
                await protocolHandler.InitializeAsync(_diagramsDirectory, verbose);
                
                // Run the appropriate protocol handler
                if (_protocolType == ProtocolType.Stdio)
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
            var stdioValid = true;
            var consecutiveErrors = 0;
            const int maxConsecutiveErrors = 3;

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
                            
                            if (consecutiveErrors >= maxConsecutiveErrors)
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
                                
                                if (consecutiveErrors >= maxConsecutiveErrors)
                                {
                                    LogMessage($"Too many consecutive errors ({consecutiveErrors}), shutting down server");
                                    stdioValid = false;
                                    break;
                                }
                                
                                // Wait a bit before retrying
                                await Task.Delay(1000);
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
                        if (consecutiveErrors >= maxConsecutiveErrors)
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