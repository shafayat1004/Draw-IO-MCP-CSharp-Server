using Microsoft.FSharp.Collections;

namespace DrawIO.MCP.SSE
{
    /// <summary>
    /// Extension methods to help with F# interoperability
    /// </summary>
    public static class FSharpExtensions
    {
        /// <summary>
        /// Converts a C# Dictionary to an F# Map
        /// </summary>
        public static FSharpMap<TKey, TValue> ToFSharpMap<TKey, TValue>(this IDictionary<TKey, TValue> dictionary)
        {
            // Convert the dictionary to a sequence of tuples as required by F# Map
            var tupleSeq = dictionary.Select(kvp => 
                new Tuple<TKey, TValue>(kvp.Key, kvp.Value));
            
            // Use the F# OfSeq function to create a map
            return MapModule.OfSeq(tupleSeq);
        }
        
        /// <summary>
        /// Converts a C# IEnumerable to an F# List
        /// </summary>
        public static FSharpList<T> ToFSharpList<T>(this IEnumerable<T> items)
        {
            return ListModule.OfSeq(items);
        }
    }
} 