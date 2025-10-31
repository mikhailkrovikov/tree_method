using System.Collections.Generic;

namespace TreeMethod.Models
{
    public class Node
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public NodeType Type { get; set; }
        public List<int> Children { get; set; } = new();
    }
}
