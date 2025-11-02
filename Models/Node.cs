using System.Collections.Generic;

namespace TreeMethod.Models
{
    public class Node
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public NodeType Type { get; set; }
        public List<int> Children { get; set; } = new();
        public int Level { get; set; } = 0; // Уровень узла в дереве (0 - корень)
        public bool IsLevelManual { get; set; } = false; // Флаг: установлен ли уровень вручную
    }
}
