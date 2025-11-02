using System.Collections.Generic;
using System.Linq;

namespace TreeMethod.Models
{
    public static class Algorithm1
    {
        public static List<List<int>> GenerateCombinations(TreeModel tree)
        {
            var nodes = tree.Nodes;
            if (nodes == null || nodes.Count == 0)
                return new List<List<int>>();
                
            var allChildren = nodes.SelectMany(n => n.Children).ToHashSet();
            var root = nodes.FirstOrDefault(n => !allChildren.Contains(n.Id)) ?? nodes.FirstOrDefault();
            
            if (root == null)
                return new List<List<int>>();

            return CombosFrom(root, nodes);
        }

        public static int CalculateRT(TreeModel tree) => GenerateCombinations(tree).Count;

        private static List<List<int>> CombosFrom(Node node, List<Node> nodes)
        {
            if (node.Children.Count == 0)
                return new List<List<int>> { new List<int> { node.Id } };

            var childCombos = node.Children
                                  .Select(id => 
                                  {
                                      var childNode = nodes.FirstOrDefault(n => n.Id == id);
                                      return childNode != null ? CombosFrom(childNode, nodes) : new List<List<int>>();
                                  })
                                  .Where(combo => combo.Count > 0) // Фильтруем пустые комбинации
                                  .ToList();

            if (node.Type == NodeType.And)
            {
                var result = new List<List<int>> { new List<int>() };
                foreach (var combos in childCombos)
                {
                    var next = new List<List<int>>();
                    foreach (var baseSet in result)
                        foreach (var add in combos)
                            next.Add(baseSet.Concat(add).ToList());
                    result = next;
                }
                return result;
            }
            else
            {
                return childCombos.SelectMany(x => x).ToList();
            }
        }
    }
}
