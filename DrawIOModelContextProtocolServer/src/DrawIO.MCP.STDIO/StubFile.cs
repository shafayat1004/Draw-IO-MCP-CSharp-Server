using System.Threading.Tasks;
using Microsoft.FSharp.Collections;

namespace DrawIO.MCP.STDIO
{
    public class StubFixer
    {
        // Used to fix issues with DiagramToolExecutor.ExecuteToolAsync return type
        public static async Task<object> ExecuteTool(Task<string> task)
        {
            var result = await task;
            return result as object;
        }
        
        // Used to fix issues with updateShapeStyle
        public static FSharpMap<T1, T2> ConvertMap<T1, T2>(FSharpMap<T1, T2> map)
        {
            return map;
        }
    }
} 