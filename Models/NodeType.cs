using System;

namespace TreeMethod.Models
{
    public enum NodeType
    {
        Leaf,
        And,
        Or,
    }
    
    // Вспомогательные методы для конвертации между внутренним форматом и JSON форматом
    // JSON формат: 0=And, 1=Or, 2=Leaf
    // Внутренний формат: 0=Leaf, 1=And, 2=Or
    public static class NodeTypeConverter
    {
        // Конвертация из JSON формата в внутренний
        public static NodeType FromJsonFormat(int jsonType)
        {
            return jsonType switch
            {
                0 => NodeType.And,   // JSON: 0 = And
                1 => NodeType.Or,    // JSON: 1 = Or
                2 => NodeType.Leaf,  // JSON: 2 = Leaf
                _ => NodeType.Leaf
            };
        }
        
        // Конвертация из внутреннего формата в JSON формат
        public static int ToJsonFormat(NodeType type)
        {
            return type switch
            {
                NodeType.And => 0,   // JSON: 0 = And
                NodeType.Or => 1,    // JSON: 1 = Or
                NodeType.Leaf => 2,  // JSON: 2 = Leaf
                _ => 2
            };
        }
    }
}
