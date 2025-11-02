using System.Collections.Generic;
using System.Linq;

namespace TreeMethod.Models
{
    public static class Algorithm2
    {
        public static List<RationalSolution> FindSolutions(TreeModel tree)
        {
            var combos = Algorithm1.GenerateCombinations(tree);

            int rootId = GetRootId(tree);

            var epNodes = tree.Nodes.Where(n => n.Id != rootId).ToList();
            var rowIndexById = epNodes.Select((n, i) => new { n.Id, i })
                                      .ToDictionary(x => x.Id, x => x.i);

            var results = new List<RationalSolution>();
            foreach (var comboLeaves in combos)
            {
                var active = GetClosureWithoutRoot(comboLeaves, tree, rootId);

                int score = EvaluateActiveSet(active, tree.EP, tree.AP, tree.GoalWeights, rowIndexById, tree);
                var names = comboLeaves.Select(id => tree.Nodes.First(n => n.Id == id).Name).ToList();

                results.Add(new RationalSolution { Elements = names, Score = score });
            }

            return results.OrderByDescending(r => r.Score).ToList();
        }

        private static int EvaluateActiveSet(HashSet<int> activeNodeIds, int[,] EP, int[,] AP, int[] goalWeights,
                                             Dictionary<int, int> rowIndexById, TreeModel tree)
        {
            if (EP == null || AP == null || goalWeights == null) return 0;

            int features = EP.GetLength(1);
            int goals = AP.GetLength(0);
            int epRows = EP.GetLength(0);
            int apCols = AP.GetLength(1);

            int actualFeatures = Math.Min(features, apCols);

            var sumFeat = new double[actualFeatures]; // Используем double для точности при умножении на levelWeight
            foreach (var nodeId in activeNodeIds)
            {
                if (!rowIndexById.TryGetValue(nodeId, out int row)) continue;
                if (row >= epRows) continue;
                
                // Получаем узел для определения уровня
                var node = tree.Nodes.FirstOrDefault(n => n.Id == nodeId);
                if (node == null) continue;
                
                // Вычисляем вес уровня: 1 / (1 + Level) - чем глубже уровень, тем меньше вес
                double levelWeight = 1.0 / (1.0 + node.Level);
                
                for (int p = 0; p < actualFeatures; p++)
                    sumFeat[p] += levelWeight * EP[row, p];
            }

            int total = 0;
            for (int g = 0; g < goals; g++)
            {
                if (g >= goalWeights.Length) continue;
                
                double goalScore = 0;
                for (int p = 0; p < actualFeatures; p++)
                    goalScore += sumFeat[p] * AP[g, p];
                total += (int)Math.Round(goalScore * goalWeights[g]);
            }
            return total;
        }

        private static int GetRootId(TreeModel tree)
        {
            var allChildren = tree.Nodes.SelectMany(n => n.Children).ToHashSet();
            return tree.Nodes.FirstOrDefault(n => !allChildren.Contains(n.Id))?.Id ?? tree.Nodes[0].Id;
        }

        private static HashSet<int> GetClosureWithoutRoot(List<int> leaves, TreeModel tree, int rootId)
        {
            var active = new HashSet<int>(leaves);
            foreach (var leaf in leaves)
            {
                foreach (var anc in GetAncestors(leaf, tree))
                {
                    if (anc == rootId) break;
                    active.Add(anc);
                }
            }
            return active;
        }

        private static IEnumerable<int> GetAncestors(int nodeId, TreeModel tree)
        {
            int current = nodeId;
            while (true)
            {
                var parent = tree.Nodes.FirstOrDefault(n => n.Children.Contains(current));
                if (parent == null) yield break;
                yield return parent.Id;
                current = parent.Id;
            }
        }
    }
}
