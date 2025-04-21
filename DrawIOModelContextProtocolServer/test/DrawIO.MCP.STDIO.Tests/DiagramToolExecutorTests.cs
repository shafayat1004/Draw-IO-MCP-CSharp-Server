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
    }
} 