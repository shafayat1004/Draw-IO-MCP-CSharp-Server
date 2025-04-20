using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using DrawIO.MCP.STDIO;

namespace DrawIO.MCP.STDIO.Tests
{
    public class QueryToolTests
    {
        private readonly string _testDiagramsDir;
        private readonly TextWriter _testLogWriter;
        
        public QueryToolTests()
        {
            // Create a test diagrams directory
            _testDiagramsDir = Path.Combine(Path.GetTempPath(), "DrawIOTests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testDiagramsDir);
            
            // Create a test log writer
            _testLogWriter = new StringWriter();
        }
        
        [Fact]
        public async Task TestFindElementsByText()
        {
            // Create a test diagram
            var diagramName = "query_test.drawio";
            var createParams = JsonDocument.Parse(@$"{{ ""name"": ""{diagramName}"" }}").RootElement;
            await DiagramToolExecutor.ExecuteToolAsync("create_new_diagram", createParams, _testDiagramsDir, _testLogWriter, true);
            
            // Add shapes with text
            var addShape1Params = JsonDocument.Parse(@$"{{ 
                ""diagram"": ""{diagramName}"", 
                ""value"": ""Test Shape 1"", 
                ""x"": 100, ""y"": 100, 
                ""width"": 100, ""height"": 50 
            }}").RootElement;
            
            var addShape2Params = JsonDocument.Parse(@$"{{ 
                ""diagram"": ""{diagramName}"", 
                ""value"": ""Another Shape"", 
                ""x"": 300, ""y"": 100, 
                ""width"": 100, ""height"": 50 
            }}").RootElement;
            
            await DiagramToolExecutor.ExecuteToolAsync("add_shape", addShape1Params, _testDiagramsDir, _testLogWriter, true);
            await DiagramToolExecutor.ExecuteToolAsync("add_shape", addShape2Params, _testDiagramsDir, _testLogWriter, true);
            
            // Test finding elements by text
            var findParams = JsonDocument.Parse(@$"{{ 
                ""diagram"": ""{diagramName}"", 
                ""search_text"": ""Test"" 
            }}").RootElement;
            
            var result = await DiagramToolExecutor.ExecuteToolAsync("find_elements_by_text", findParams, _testDiagramsDir, _testLogWriter, true);
            
            // Assert the result
            Assert.NotNull(result);
            var resultDict = Assert.IsType<System.Collections.Generic.Dictionary<string, object>>(result);
            Assert.True(resultDict.ContainsKey("elements"));
            Assert.True(resultDict.ContainsKey("count"));
            
            // Note: This test will likely fail until the query tools are implemented correctly
        }
        
        [Fact]
        public async Task TestGetElementInfo()
        {
            // Create a test diagram
            var diagramName = "element_info_test.drawio";
            var createParams = JsonDocument.Parse(@$"{{ ""name"": ""{diagramName}"" }}").RootElement;
            await DiagramToolExecutor.ExecuteToolAsync("create_new_diagram", createParams, _testDiagramsDir, _testLogWriter, true);
            
            // Add a shape
            var addShapeParams = JsonDocument.Parse(@$"{{ 
                ""diagram"": ""{diagramName}"", 
                ""value"": ""Server"", 
                ""x"": 100, ""y"": 100, 
                ""width"": 120, ""height"": 60 
            }}").RootElement;
            
            var addResult = await DiagramToolExecutor.ExecuteToolAsync("add_shape", addShapeParams, _testDiagramsDir, _testLogWriter, true);
            var addResultDict = Assert.IsType<System.Collections.Generic.Dictionary<string, object>>(addResult);
            var elementId = addResultDict["ElementId"].ToString();
            
            // Test getting element info
            var infoParams = JsonDocument.Parse(@$"{{ 
                ""diagram"": ""{diagramName}"", 
                ""element_id"": ""{elementId}"" 
            }}").RootElement;
            
            var result = await DiagramToolExecutor.ExecuteToolAsync("get_element_info", infoParams, _testDiagramsDir, _testLogWriter, true);
            
            // Assert the result
            Assert.NotNull(result);
            var resultDict = Assert.IsType<System.Collections.Generic.Dictionary<string, object>>(result);
            Assert.True(resultDict.ContainsKey("id"));
            Assert.True(resultDict.ContainsKey("value"));
            Assert.Equal("Server", resultDict["value"]);
            
            // Note: This test will likely fail until the query tools are implemented correctly
        }
        
        [Fact]
        public async Task TestListNeighbors()
        {
            // Create a test diagram
            var diagramName = "neighbors_test.drawio";
            var createParams = JsonDocument.Parse(@$"{{ ""name"": ""{diagramName}"" }}").RootElement;
            await DiagramToolExecutor.ExecuteToolAsync("create_new_diagram", createParams, _testDiagramsDir, _testLogWriter, true);
            
            // Add two shapes
            var addShape1Params = JsonDocument.Parse(@$"{{ 
                ""diagram"": ""{diagramName}"", 
                ""value"": ""Server"", 
                ""x"": 100, ""y"": 100, 
                ""width"": 100, ""height"": 50 
            }}").RootElement;
            
            var addShape2Params = JsonDocument.Parse(@$"{{ 
                ""diagram"": ""{diagramName}"", 
                ""value"": ""Database"", 
                ""x"": 300, ""y"": 100, 
                ""width"": 100, ""height"": 50 
            }}").RootElement;
            
            var addResult1 = await DiagramToolExecutor.ExecuteToolAsync("add_shape", addShape1Params, _testDiagramsDir, _testLogWriter, true);
            var addResult2 = await DiagramToolExecutor.ExecuteToolAsync("add_shape", addShape2Params, _testDiagramsDir, _testLogWriter, true);
            
            var addResultDict1 = Assert.IsType<System.Collections.Generic.Dictionary<string, object>>(addResult1);
            var addResultDict2 = Assert.IsType<System.Collections.Generic.Dictionary<string, object>>(addResult2);
            
            var serverId = addResultDict1["ElementId"].ToString();
            var databaseId = addResultDict2["ElementId"].ToString();
            
            // Connect the shapes
            var connectParams = JsonDocument.Parse(@$"{{ 
                ""diagram"": ""{diagramName}"", 
                ""source_id"": ""{serverId}"", 
                ""target_id"": ""{databaseId}"" 
            }}").RootElement;
            
            await DiagramToolExecutor.ExecuteToolAsync("connect_shapes", connectParams, _testDiagramsDir, _testLogWriter, true);
            
            // Test listing neighbors
            var neighborsParams = JsonDocument.Parse(@$"{{ 
                ""diagram"": ""{diagramName}"", 
                ""element_id"": ""{serverId}"" 
            }}").RootElement;
            
            var result = await DiagramToolExecutor.ExecuteToolAsync("list_neighbors", neighborsParams, _testDiagramsDir, _testLogWriter, true);
            
            // Assert the result
            Assert.NotNull(result);
            var resultDict = Assert.IsType<System.Collections.Generic.Dictionary<string, object>>(result);
            Assert.True(resultDict.ContainsKey("neighbors"));
            Assert.True(resultDict.ContainsKey("count"));
            
            // Note: This test will likely fail until the query tools are implemented correctly
        }
        
        [Fact]
        public async Task TestGetDiagramBounds()
        {
            // Create a test diagram
            var diagramName = "bounds_test.drawio";
            var createParams = JsonDocument.Parse(@$"{{ ""name"": ""{diagramName}"" }}").RootElement;
            await DiagramToolExecutor.ExecuteToolAsync("create_new_diagram", createParams, _testDiagramsDir, _testLogWriter, true);
            
            // Add shapes at different positions
            var addShape1Params = JsonDocument.Parse(@$"{{ 
                ""diagram"": ""{diagramName}"", 
                ""value"": ""Top Left"", 
                ""x"": 50, ""y"": 50, 
                ""width"": 100, ""height"": 50 
            }}").RootElement;
            
            var addShape2Params = JsonDocument.Parse(@$"{{ 
                ""diagram"": ""{diagramName}"", 
                ""value"": ""Bottom Right"", 
                ""x"": 400, ""y"": 300, 
                ""width"": 100, ""height"": 50 
            }}").RootElement;
            
            await DiagramToolExecutor.ExecuteToolAsync("add_shape", addShape1Params, _testDiagramsDir, _testLogWriter, true);
            await DiagramToolExecutor.ExecuteToolAsync("add_shape", addShape2Params, _testDiagramsDir, _testLogWriter, true);
            
            // Test getting diagram bounds
            var boundsParams = JsonDocument.Parse(@$"{{ ""diagram"": ""{diagramName}"" }}").RootElement;
            
            var result = await DiagramToolExecutor.ExecuteToolAsync("get_diagram_bounds", boundsParams, _testDiagramsDir, _testLogWriter, true);
            
            // Assert the result
            Assert.NotNull(result);
            var resultDict = Assert.IsType<System.Collections.Generic.Dictionary<string, object>>(result);
            Assert.True(resultDict.ContainsKey("minX"));
            Assert.True(resultDict.ContainsKey("minY"));
            Assert.True(resultDict.ContainsKey("maxX"));
            Assert.True(resultDict.ContainsKey("maxY"));
            Assert.True(resultDict.ContainsKey("width"));
            Assert.True(resultDict.ContainsKey("height"));
            
            // Note: This test will likely fail until the query tools are implemented correctly
        }
    }
} 