using System;

namespace TreeMethod.Models
{
    public enum NodeType
    {
        Leaf,
        And,
        Or,
    }
    
    public static class NodeTypeConverter
    {
        public static NodeType FromJsonFormat(int jsonType)
        {
            return jsonType switch
            {
                0 => NodeType.And,
                1 => NodeType.Or,
                2 => NodeType.Leaf,
                _ => NodeType.Leaf
            };
        }
        
        public static int ToJsonFormat(NodeType type)
        {
            return type switch
            {
                NodeType.And => 0,
                NodeType.Or => 1,
                NodeType.Leaf => 2,
                _ => 2
            };
        }
    }
}
