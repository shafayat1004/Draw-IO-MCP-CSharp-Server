using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using DrawIO.MCP.Core;
using Xunit;

namespace DrawIO.MCP.STDIO.Tests
{
    public class DiagramToolExecutorTests
    {
        private readonly string _testDiagramsDirectory;
        private readonly string _testDiagramPath;
        private readonly TextWriter _testLogWriter;

        public DiagramToolExecutorTests()
        {
            _testDiagramsDirectory = Path.Combine(Path.GetTempPath(), "DrawIOTests");
            _testDiagramPath = Path.Combine(_testDiagramsDirectory, "test.drawio");
            _testLogWriter = new StringWriter();
            
            Directory.CreateDirectory(_testDiagramsDirectory);
            var diagram = DiagramManipulation.createEmptyDiagram();
            File.WriteAllText(_testDiagramPath, JsonSerializer.Serialize(diagram));
        }

        private async Task<string> CreateTestConnectorAsync()
        {
            // Add two shapes and connect them
            var addShape1Params = JsonDocument.Parse(@$"{{ 
                ""diagram"": ""test.drawio"", 
                ""value"": ""Shape 1"", 
                ""x"": 100, ""y"": 100, 
                ""width"": 100, ""height"": 50 
            }}").RootElement;
            
            var addShape2Params = JsonDocument.Parse(@$"{{ 
                ""diagram"": ""test.drawio"", 
                ""value"": ""Shape 2"", 
                ""x"": 300, ""y"": 100, 
                ""width"": 100, ""height"": 50 
            }}").RootElement;
            
            var result1 = await DiagramToolExecutor.ExecuteToolAsync("add_shape", addShape1Params, _testDiagramsDirectory, _testLogWriter, true);
            var result2 = await DiagramToolExecutor.ExecuteToolAsync("add_shape", addShape2Params, _testDiagramsDirectory, _testLogWriter, true);
            
            var shape1Id = ((System.Collections.Generic.Dictionary<string, object>)result1)["elementId"].ToString();
            var shape2Id = ((System.Collections.Generic.Dictionary<string, object>)result2)["elementId"].ToString();
            
            var connectParams = JsonDocument.Parse(@$"{{ 
                ""diagram"": ""test.drawio"", 
                ""source_id"": ""{shape1Id}"", 
                ""target_id"": ""{shape2Id}"" 
            }}").RootElement;
            
            var connectResult = await DiagramToolExecutor.ExecuteToolAsync("connect_shapes", connectParams, _testDiagramsDirectory, _testLogWriter, true);
            return ((System.Collections.Generic.Dictionary<string, object>)connectResult)["elementId"].ToString();
        }

        [Fact]
        public async Task SetLineStyle_ValidParameters_Success()
        {
            // Arrange
            var connectorId = await CreateTestConnectorAsync();
            var parameters = JsonDocument.Parse(@$"{{
                ""diagram"": ""test.drawio"",
                ""connector_id"": ""{connectorId}"",
                ""line_style"": ""dashed"",
                ""line_width"": 2.5
            }}").RootElement;

            // Act
            var result = await DiagramToolExecutor.ExecuteToolAsync("set_line_style", parameters, _testDiagramsDirectory, _testLogWriter, true);

            // Assert
            Assert.NotNull(result);
            var resultDict = Assert.IsType<System.Collections.Generic.Dictionary<string, object>>(result);
            Assert.Equal("success", resultDict["status"]);
            Assert.Contains("dashed=1;strokeWidth=2.5;", resultDict["style"].ToString());
        }

        [Fact]
        public async Task SetArrowStyle_ValidParameters_Success()
        {
            // Arrange
            var connectorId = await CreateTestConnectorAsync();
            var parameters = JsonDocument.Parse(@$"{{
                ""diagram"": ""test.drawio"",
                ""connector_id"": ""{connectorId}"",
                ""start_arrow"": ""diamond"",
                ""end_arrow"": ""classic""
            }}").RootElement;

            // Act
            var result = await DiagramToolExecutor.ExecuteToolAsync("set_arrow_style", parameters, _testDiagramsDirectory, _testLogWriter, true);

            // Assert
            Assert.NotNull(result);
            var resultDict = Assert.IsType<System.Collections.Generic.Dictionary<string, object>>(result);
            Assert.Equal("success", resultDict["status"]);
            Assert.Contains("startArrow=diamond;endArrow=classic;", resultDict["style"].ToString());
        }

        [Fact]
        public async Task ResetConnector_ValidParameters_Success()
        {
            // Arrange
            var connectorId = await CreateTestConnectorAsync();
            var parameters = JsonDocument.Parse(@$"{{
                ""diagram"": ""test.drawio"",
                ""connector_id"": ""{connectorId}""
            }}").RootElement;

            // Act
            var result = await DiagramToolExecutor.ExecuteToolAsync("reset_connector", parameters, _testDiagramsDirectory, _testLogWriter, true);

            // Assert
            Assert.NotNull(result);
            var resultDict = Assert.IsType<System.Collections.Generic.Dictionary<string, object>>(result);
            Assert.Equal("success", resultDict["status"]);
            Assert.Contains("Reset connector", ((object[])resultDict["content"])[0].ToString());
        }

        [Fact]
        public async Task ReverseConnector_ValidParameters_Success()
        {
            // Arrange
            var connectorId = await CreateTestConnectorAsync();
            var parameters = JsonDocument.Parse(@$"{{
                ""diagram"": ""test.drawio"",
                ""connector_id"": ""{connectorId}""
            }}").RootElement;

            // Act
            var result = await DiagramToolExecutor.ExecuteToolAsync("reverse_connector", parameters, _testDiagramsDirectory, _testLogWriter, true);

            // Assert
            Assert.NotNull(result);
            var resultDict = Assert.IsType<System.Collections.Generic.Dictionary<string, object>>(result);
            Assert.Equal("success", resultDict["status"]);
            Assert.True(resultDict.ContainsKey("newConnectorId"));
            Assert.Contains("Reversed connector direction", ((object[])resultDict["content"])[0].ToString());
        }

        [Theory]
        [InlineData("set_line_style", "invalid_style")]
        [InlineData("set_arrow_style", "invalid_arrow")]
        public async Task SetStyles_InvalidStyle_ThrowsArgumentException(string toolName, string invalidStyle)
        {
            // Arrange
            var connectorId = await CreateTestConnectorAsync();
            var parameters = JsonDocument.Parse(@$"{{
                ""diagram"": ""test.drawio"",
                ""connector_id"": ""{connectorId}"",
                ""line_style"": ""{invalidStyle}"",
                ""start_arrow"": ""{invalidStyle}""
            }}").RootElement;

            // Act & Assert
            var result = await DiagramToolExecutor.ExecuteToolAsync(toolName, parameters, _testDiagramsDirectory, _testLogWriter, true);
            var resultDict = Assert.IsType<System.Collections.Generic.Dictionary<string, object>>(result);
            Assert.True(resultDict.ContainsKey("error"));
            Assert.Contains("Invalid", resultDict["error"].ToString());
        }

        [Theory]
        [InlineData("set_line_style")]
        [InlineData("set_arrow_style")]
        [InlineData("reset_connector")]
        [InlineData("reverse_connector")]
        public async Task ConnectorTools_DiagramNotFound_ThrowsFileNotFoundException(string toolName)
        {
            // Arrange
            var parameters = JsonDocument.Parse(@"{
                ""diagram"": ""nonexistent.drawio"",
                ""connector_id"": ""edge1""
            }").RootElement;

            // Act & Assert
            var result = await DiagramToolExecutor.ExecuteToolAsync(toolName, parameters, _testDiagramsDirectory, _testLogWriter, true);
            var resultDict = Assert.IsType<System.Collections.Generic.Dictionary<string, object>>(result);
            Assert.True(resultDict.ContainsKey("error"));
            Assert.Contains("not found", resultDict["error"].ToString());
        }

        [Theory]
        [InlineData("set_line_style")]
        [InlineData("set_arrow_style")]
        [InlineData("reset_connector")]
        [InlineData("reverse_connector")]
        public async Task ConnectorTools_InvalidConnectorId_ReturnsError(string toolName)
        {
            // Arrange
            var parameters = JsonDocument.Parse(@"{
                ""diagram"": ""test.drawio"",
                ""connector_id"": ""invalid_id""
            }").RootElement;

            // Act & Assert
            var result = await DiagramToolExecutor.ExecuteToolAsync(toolName, parameters, _testDiagramsDirectory, _testLogWriter, true);
            var resultDict = Assert.IsType<System.Collections.Generic.Dictionary<string, object>>(result);
            Assert.True(resultDict.ContainsKey("error"));
            Assert.Contains("not found", resultDict["error"].ToString());
        }

        [Fact]
        public async Task AddShape_ShouldAddShapeAndReturnUpdatedDiagram()
        {
            // Arrange
            var parameters = JsonDocument.Parse(@"{
                ""diagram"": ""test.drawio"",
                ""value"": ""Test Shape"",
                ""x"": 100,
                ""y"": 100,
                ""width"": 120,
                ""height"": 80
            }").RootElement;

            // Act
            var result = await DiagramToolExecutor.ExecuteToolAsync("add_shape", parameters, _testDiagramsDirectory, _testLogWriter, true);

            // Assert
            Assert.NotNull(result);
            var resultDict = Assert.IsType<System.Collections.Generic.Dictionary<string, object>>(result);
            Assert.Equal("success", resultDict["status"]);
            Assert.NotNull(resultDict["elementId"]);
            Assert.NotEmpty(resultDict["elementId"].ToString());
            
            var content = Assert.IsType<object[]>(resultDict["content"]);
            Assert.Single(content);
            Assert.Contains("Added shape with ID", content[0].ToString());

            // Verify the diagram was actually updated
            var updatedDiagram = JsonSerializer.Deserialize<object>(File.ReadAllText(_testDiagramPath));
            Assert.NotNull(updatedDiagram);
        }

        [Fact]
        public async Task ConnectShapes_ShouldConnectShapesAndReturnUpdatedDiagram()
        {
            // Arrange - First add two shapes
            var shape1Params = JsonDocument.Parse(@"{
                ""diagram"": ""test.drawio"",
                ""value"": ""Source Shape"",
                ""x"": 50,
                ""y"": 50,
                ""width"": 100,
                ""height"": 60
            }").RootElement;

            var shape2Params = JsonDocument.Parse(@"{
                ""diagram"": ""test.drawio"",
                ""value"": ""Target Shape"",
                ""x"": 200,
                ""y"": 50,
                ""width"": 100,
                ""height"": 60
            }").RootElement;

            var shape1Result = await DiagramToolExecutor.ExecuteToolAsync("add_shape", shape1Params, _testDiagramsDirectory, _testLogWriter, true);
            var shape2Result = await DiagramToolExecutor.ExecuteToolAsync("add_shape", shape2Params, _testDiagramsDirectory, _testLogWriter, true);

            var shape1Id = ((System.Collections.Generic.Dictionary<string, object>)shape1Result)["elementId"].ToString();
            var shape2Id = ((System.Collections.Generic.Dictionary<string, object>)shape2Result)["elementId"].ToString();

            var connectParams = JsonDocument.Parse(@$"{{
                ""diagram"": ""test.drawio"",
                ""source_id"": ""{shape1Id}"",
                ""target_id"": ""{shape2Id}""
            }}").RootElement;

            // Act
            var result = await DiagramToolExecutor.ExecuteToolAsync("connect_shapes", connectParams, _testDiagramsDirectory, _testLogWriter, true);

            // Assert
            Assert.NotNull(result);
            var resultDict = Assert.IsType<System.Collections.Generic.Dictionary<string, object>>(result);
            Assert.Equal("success", resultDict["status"]);
            Assert.NotNull(resultDict["elementId"]);
            Assert.NotEmpty(resultDict["elementId"].ToString());
            
            var content = Assert.IsType<object[]>(resultDict["content"]);
            Assert.Single(content);
            Assert.Contains("Connected shapes with connector ID", content[0].ToString());

            // Verify the diagram was actually updated
            var updatedDiagram = JsonSerializer.Deserialize<object>(File.ReadAllText(_testDiagramPath));
            Assert.NotNull(updatedDiagram);
        }

        [Fact]
        public async Task AddShape_WithInvalidParameters_ShouldReturnError()
        {
            // Arrange
            var parameters = JsonDocument.Parse(@"{
                ""diagram"": ""test.drawio"",
                ""value"": ""Test Shape"",
                ""x"": ""invalid"",
                ""y"": 100,
                ""width"": 120,
                ""height"": 80
            }").RootElement;

            // Act
            var result = await DiagramToolExecutor.ExecuteToolAsync("add_shape", parameters, _testDiagramsDirectory, _testLogWriter, true);

            // Assert
            Assert.NotNull(result);
            var resultDict = Assert.IsType<System.Collections.Generic.Dictionary<string, object>>(result);
            Assert.True(resultDict.ContainsKey("isError"));
            Assert.True(resultDict.ContainsKey("error"));
            Assert.Contains("Error", ((object[])resultDict["content"])[0].ToString());
        }

        [Fact]
        public async Task ConnectShapes_WithInvalidShapeIds_ShouldReturnError()
        {
            // Arrange
            var parameters = JsonDocument.Parse(@"{
                ""diagram"": ""test.drawio"",
                ""source_id"": ""invalid_source_id"",
                ""target_id"": ""invalid_target_id""
            }").RootElement;

            // Act
            var result = await DiagramToolExecutor.ExecuteToolAsync("connect_shapes", parameters, _testDiagramsDirectory, _testLogWriter, true);

            // Assert
            Assert.NotNull(result);
            var resultDict = Assert.IsType<System.Collections.Generic.Dictionary<string, object>>(result);
            Assert.True(resultDict.ContainsKey("isError"));
            Assert.True(resultDict.ContainsKey("error"));
            Assert.Contains("Error", ((object[])resultDict["content"])[0].ToString());
        }

        [Fact]
        public async Task GenerateVpc_ShouldCreateVpcDiagramAndReturnUpdatedDiagram()
        {
            // Arrange
            var executor = new DiagramToolExecutor();
            var diagram = "vpc_test.drawio";

            // Act
            var result = await executor.GenerateVpcAsync(diagram);

            // Assert
            Assert.NotNull(result);
        }

        [Fact]
        public async Task ReverseConnector_ShouldPreserveConnectorId()
        {
            // Arrange
            var testDiagram = "test_reverse_connector.drawio";
            var sourceId = "source1";
            var targetId = "target1";
            
            // Create test diagram with a connector
            var diagramObj = DrawIO.MCP.Core.DiagramManipulation.createNewDiagram();
            diagramObj = DrawIO.MCP.Core.DiagramManipulation.addShape(diagramObj, 0, "rectangle", "Source", 100, 100, 80, 40).Item1;
            diagramObj = DrawIO.MCP.Core.DiagramManipulation.addShape(diagramObj, 0, "rectangle", "Target", 300, 100, 80, 40).Item1;
            var (diagram, connectorId) = DrawIO.MCP.Core.DiagramManipulation.connectShapes(diagramObj, 0, sourceId, targetId);
            
            var filePath = Path.Combine(_testDiagramsDirectory, testDiagram);
            DrawIO.MCP.Core.DiagramManipulation.saveDiagram(diagram, filePath);
            
            // Create parameters for reverse operation
            var parameters = JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["diagram"] = testDiagram,
                ["connector_id"] = connectorId
            });
            
            // Act
            var result = await DiagramToolExecutor.ExecuteToolAsync("reverse_connector", JsonDocument.Parse(parameters).RootElement, _testDiagramsDirectory);
            var resultDict = (Dictionary<string, object>)result;
            
            // Assert
            Assert.Equal("success", resultDict["status"]);
            Assert.Equal(connectorId, resultDict["elementId"]); // Verify ID remains the same
            
            // Verify the connector direction is actually reversed
            var updatedDiagram = DrawIO.MCP.Core.DiagramManipulation.loadDiagram(filePath);
            var elementInfo = DrawIO.MCP.Core.DiagramManipulation.getElementInfo(updatedDiagram, 0, connectorId).Value;
            Assert.Equal(targetId, elementInfo.Source.Value);
            Assert.Equal(sourceId, elementInfo.Target.Value);
        }
    }
} 