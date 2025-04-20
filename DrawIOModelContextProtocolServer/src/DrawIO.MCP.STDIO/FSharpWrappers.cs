using System;
using System.Collections.Generic;
using Microsoft.FSharp.Core;
using Microsoft.FSharp.Collections;
using static DrawIO.MCP.Core.DiagramManipulation;

namespace DrawIO.MCP.STDIO
{
    // Add a type alias for easier reference to F# types
    using FSharpTypes = DrawIO.MCP.Core.Types;

    /// <summary>
    /// Helpers for working with F# option types in C#
    /// </summary>
    public static class FSharpOptionExtensions
    {
        public static bool IsSome<T>(this FSharpOption<T> option) => 
            FSharpOption<T>.get_IsSome(option);
            
        public static bool IsNone<T>(this FSharpOption<T> option) => 
            !IsSome(option);
            
        public static T ValueOrDefault<T>(this FSharpOption<T> option, T defaultValue = default) => 
            IsSome(option) ? option.Value : defaultValue;
    }

    /// <summary>
    /// C# wrapper for F# types in DrawIO.MCP.Core.Types module
    /// </summary>
    public static class CoreTypes
    {
        // Re-export the F# types with proper C# naming conventions
        public static class Geometry
        {
            public static FSharpTypes.Geometry Create(
                double x, double y, double width, double height, bool relative)
            {
                return new FSharpTypes.Geometry(
                    new FSharpTypes.Position(x, y),
                    new FSharpTypes.Size(width, height),
                    relative
                );
            }
        }

        public static FSharpTypes.Diagram CreateEmptyDiagram() => 
            createEmptyDiagram();
            
        // Helper for checking if a geometry option has a value
        public static bool HasGeometry(FSharpOption<FSharpTypes.Geometry> geometryOption) =>
            FSharpOption<FSharpTypes.Geometry>.get_IsSome(geometryOption);
    }

    /// <summary>
    /// C# wrapper for F# functions in DrawIO.MCP.Core.FileOperations module
    /// </summary>
    public static class FileOperations
    {
        public static FSharpTypes.Diagram LoadDiagram(string filePath) => 
            DrawIO.MCP.Core.FileOperations.loadDiagram(filePath);

        public static void SaveDiagram(FSharpTypes.Diagram diagram, string filePath) => 
            DrawIO.MCP.Core.FileOperations.saveDiagram(diagram, filePath);

        public static FSharpTypes.Diagram CreateNewDiagram(string filePath) => 
            DrawIO.MCP.Core.FileOperations.createNewDiagram(filePath);
    }

    /// <summary>
    /// C# wrapper for F# functions in DrawIO.MCP.Core.DiagramManipulation module
    /// </summary>
    public static class DiagramManipulation
    {
        public static (FSharpTypes.Diagram diagram, string id) AddShape(
            FSharpTypes.Diagram diagram, 
            int pageIndex, 
            string value, 
            float x, 
            float y, 
            float width, 
            float height, 
            string shape)
        {
            var result = DrawIO.MCP.Core.DiagramManipulation.addShape(
                diagram, pageIndex, value, x, y, width, height, shape);
            
            // F# tuples are not directly compatible with C# tuples, need to extract items
            return (result.Item1, result.Item2);
        }

        public static (FSharpTypes.Diagram diagram, string id) ConnectShapes(
            FSharpTypes.Diagram diagram,
            int pageIndex,
            string sourceId,
            string targetId)
        {
            var result = DrawIO.MCP.Core.DiagramManipulation.connectShapes(
                diagram, pageIndex, sourceId, targetId);
            
            return (result.Item1, result.Item2);
        }

        public static FSharpTypes.Diagram DeleteShape(
            FSharpTypes.Diagram diagram,
            int pageIndex,
            string shapeId)
        {
            return DrawIO.MCP.Core.DiagramManipulation.deleteShape(
                diagram, pageIndex, shapeId);
        }

        public static FSharpTypes.Diagram UpdateShape(
            FSharpTypes.Diagram diagram,
            int pageIndex,
            string shapeId,
            string value,
            float? x,
            float? y,
            float? width,
            float? height,
            string style)
        {
            // Convert nullable float to FSharpOption<float>
            var xOption = x.HasValue ? 
                FSharpOption<double>.Some(x.Value) : 
                FSharpOption<double>.None;
                
            var yOption = y.HasValue ? 
                FSharpOption<double>.Some(y.Value) : 
                FSharpOption<double>.None;
                
            var widthOption = width.HasValue ? 
                FSharpOption<double>.Some(width.Value) : 
                FSharpOption<double>.None;
                
            var heightOption = height.HasValue ? 
                FSharpOption<double>.Some(height.Value) : 
                FSharpOption<double>.None;
                
            var styleOption = style != null ? 
                FSharpOption<string>.Some(style) : 
                FSharpOption<string>.None;
                
            return DrawIO.MCP.Core.DiagramManipulation.updateShape(
                diagram, pageIndex, shapeId, value, xOption, yOption, widthOption, heightOption, styleOption);
        }

        public static FSharpTypes.Diagram ArrangeLayout(
            FSharpTypes.Diagram diagram,
            int pageIndex,
            string layout)
        {
            return DrawIO.MCP.Core.DiagramManipulation.arrangeLayout(
                diagram, pageIndex, layout);
        }
    }

    /// <summary>
    /// C# wrapper for F# functions in DrawIO.MCP.Core.DiagramOperations module
    /// </summary>
    public static class DiagramOperations
    {
        public static FSharpTypes.Diagram MoveShape(
            FSharpTypes.Diagram diagram, 
            string cellId, 
            float x, 
            float y)
        {
            return DrawIO.MCP.Core.DiagramManipulation.moveShape(diagram, cellId, x, y);
        }

        public static FSharpTypes.Diagram UpdateShapeStyle(
            FSharpTypes.Diagram diagram, 
            string cellId, 
            FSharpMap<string, string> styleProperties)
        {
            return DrawIO.MCP.Core.DiagramManipulation.updateShapeStyle(diagram, cellId, styleProperties);
        }

        public static FSharpTypes.Diagram ArrangeDiagram(
            FSharpTypes.Diagram diagram, 
            FSharpOption<string> pageId)
        {
            return DrawIO.MCP.Core.DiagramManipulation.arrangeDiagram(diagram, pageId);
        }

        public static FSharpTypes.Diagram CreateDiagramPage(
            FSharpTypes.Diagram diagram, 
            string name)
        {
            return DrawIO.MCP.Core.FileOperations.createDiagramPage(diagram, name);
        }

        public static FSharpOption<FSharpTypes.Page> GetDiagramPage(
            FSharpTypes.Diagram diagram, 
            int pageIndex)
        {
            if (pageIndex >= 0 && pageIndex < diagram.Pages.Length)
            {
                return FSharpOption<FSharpTypes.Page>.Some(diagram.Pages[pageIndex]);
            }
            return FSharpOption<FSharpTypes.Page>.None;
        }

        public static FSharpTypes.Diagram UpdateDiagramPage(
            FSharpTypes.Diagram diagram, 
            string pageId, 
            FSharpOption<string> name)
        {
            return DrawIO.MCP.Core.FileOperations.updateDiagramPage(diagram, pageId, name);
        }

        public static FSharpTypes.Diagram DeleteDiagramPage(
            FSharpTypes.Diagram diagram, 
            string pageId)
        {
            return DrawIO.MCP.Core.FileOperations.deleteDiagramPage(diagram, pageId);
        }

        public static FSharpTypes.Diagram MoveCellBetweenPages(
            FSharpTypes.Diagram diagram, 
            string cellId, 
            string sourcePageId, 
            string targetPageId)
        {
            return DrawIO.MCP.Core.FileOperations.moveCellBetweenPages(diagram, cellId, sourcePageId, targetPageId);
        }
    }

    /// <summary>
    /// Helper methods for converting C# collections to F# collections
    /// </summary>
    public static class FSharpWrappers
    {
        /// <summary>
        /// Convert a C# Dictionary to an F# Map
        /// </summary>
        public static FSharpMap<string, string> DictionaryToMap(Dictionary<string, string> dictionary)
        {
            var map = MapModule.Empty<string, string>();
            foreach (var kvp in dictionary)
            {
                map = MapModule.Add(kvp.Key, kvp.Value, map);
            }
            return map;
        }
    }
} 