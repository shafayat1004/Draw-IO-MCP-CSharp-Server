using System;
using System.Collections.Generic;
using System.Linq;
using DrawIO.MCP.Core;
using Microsoft.FSharp.Collections;
using Microsoft.FSharp.Core;
using FSharpTypes = DrawIO.MCP.Core.Types;

namespace DrawIO.MCP.STDIO
{
    /// <summary>
    /// Extension methods for FSharpOption
    /// </summary>
    public static class FSharpOptionExtensions
    {
        public static bool IsSome<T>(this FSharpOption<T> option) => 
            !FSharpOption<T>.get_IsNone(option);
        
        public static bool IsNone<T>(this FSharpOption<T> option) => 
            FSharpOption<T>.get_IsNone(option);
        
        public static T ValueOrDefault<T>(this FSharpOption<T> option, T defaultValue = default) => 
            option.IsSome() ? option.Value : defaultValue;
    }
    
    /// <summary>
    /// C# wrapper for F# core types
    /// </summary>
    public static class CoreTypes
    {
        public static class Geometry
        {
            public static FSharpTypes.Geometry Create(
                double x, double y, double width, double height, bool relative)
            {
                var position = new FSharpTypes.Position(x, y);
                var size = new FSharpTypes.Size(width, height);
                var emptyWaypoints = ListModule.Empty<FSharpTypes.Waypoint>();
                
                return new FSharpTypes.Geometry(position, size, relative, emptyWaypoints);
            }
        }
        
        public static FSharpTypes.Diagram CreateEmptyDiagram() => 
            DiagramManipulation.createEmptyDiagram();
        
        public static bool HasGeometry(FSharpOption<FSharpTypes.Geometry> geometryOption) =>
            geometryOption.IsSome();
    }
    
    /// <summary>
    /// C# wrapper for F# functions in DrawIO.MCP.Core.FileOperations module
    /// </summary>
    public static class FileOperations
    {
        public static FSharpTypes.Diagram LoadDiagram(string filePath) => 
            Core.FileOperations.loadDiagram(filePath);
        
        public static void SaveDiagram(FSharpTypes.Diagram diagram, string filePath) => 
            Core.FileOperations.saveDiagram(diagram, filePath);
        
        public static FSharpTypes.Diagram CreateNewDiagram(string filePath) => 
            Core.FileOperations.createNewDiagram(filePath);
    }

    /// <summary>
    /// Helper methods for converting C# collections to F# collections
    /// </summary>
    public static class FSharpWrappers
    {
        /// <summary>
        /// Convert a C# Dictionary to F# Map
        /// </summary>
        public static FSharpMap<string, string> DictionaryToMap(Dictionary<string, string> dictionary)
        {
            return MapModule.OfSeq(
                dictionary.Select(kvp => Tuple.Create(kvp.Key, kvp.Value))
            );
        }
    }
} 