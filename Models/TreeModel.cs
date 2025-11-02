using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace TreeMethod.Models
{
    public class TreeModel
    {
        public List<Node> Nodes { get; set; } = new();
        public int[,] EP { get; set; }     // Матрица элементов × признаки
        public int[,] AP { get; set; }     // Матрица целей × признаки
        public int[] GoalWeights { get; set; }
        public List<string> FeatureNames { get; set; } = new(); // Названия признаков (P1, P2, ...)
        public List<string> GoalNames { get; set; } = new(); // Названия целей (A1, A2, ...)

        // ===============================
        //  Назначение уровней узлам
        // ===============================

        /// <summary>
        /// Назначает уровни всем узлам дерева. Вызывается автоматически при загрузке или изменении дерева.
        /// </summary>
        public void AssignLevels()
        {
            if (Nodes == null || Nodes.Count == 0) return;

            // Находим корень (узел без родителей)
            var allChildren = Nodes.SelectMany(n => n.Children).ToHashSet();
            var root = Nodes.FirstOrDefault(n => !allChildren.Contains(n.Id)) ?? Nodes.FirstOrDefault();
            
            if (root != null)
            {
                AssignLevelsRecursive(root, 0);
            }
        }

        private void AssignLevelsRecursive(Node node, int level)
        {
            // Не перезаписываем уровень, если он был установлен вручную
            if (!node.IsLevelManual)
            {
                node.Level = level;
            }
            
            // Для детей используем либо ручной уровень узла, либо вычисленный
            int childBaseLevel = node.IsLevelManual ? node.Level : level;
            
            foreach (var childId in node.Children)
            {
                var child = Nodes.FirstOrDefault(n => n.Id == childId);
                if (child != null)
                {
                    // Если у ребенка нет ручного уровня, вычисляем его относительно родителя
                    if (!child.IsLevelManual)
                    {
                        AssignLevelsRecursive(child, childBaseLevel + 1);
                    }
                    else
                    {
                        // У ребенка ручной уровень, но всё равно проверяем его потомков
                        foreach (var grandChildId in child.Children)
                        {
                            var grandChild = Nodes.FirstOrDefault(n => n.Id == grandChildId);
                            if (grandChild != null && !grandChild.IsLevelManual)
                            {
                                AssignLevelsRecursive(grandChild, child.Level + 1);
                            }
                        }
                    }
                }
            }
        }

        // ===============================
        //  Сохранение / Загрузка проекта
        // ===============================

        public void SaveProject(string path)
        {
            var serializable = new SerializableTreeModel(this);
            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(path, JsonSerializer.Serialize(serializable, options));
        }

        public static TreeModel LoadProject(string path)
        {
            var json = File.ReadAllText(path);
            var data = JsonSerializer.Deserialize<SerializableTreeModel>(json);
            var model = data?.ToTreeModel();
            // Назначаем уровни после загрузки
            model?.AssignLevels();
            return model;
        }
    }

    // Класс для сериализации Node с правильным форматом Type
    public class SerializableNode
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Type { get; set; } // JSON формат: 0=And, 1=Or, 2=Leaf
        public List<int> Children { get; set; } = new();
        public int Level { get; set; } = 0;
        public bool IsLevelManual { get; set; } = false;
        
        public SerializableNode() { }
        
        public SerializableNode(Node node)
        {
            Id = node.Id;
            Name = node.Name;
            Type = NodeTypeConverter.ToJsonFormat(node.Type);
            Children = node.Children;
            Level = node.Level;
            IsLevelManual = node.IsLevelManual;
        }
        
        public Node ToNode()
        {
            return new Node
            {
                Id = Id,
                Name = Name,
                Type = NodeTypeConverter.FromJsonFormat(Type),
                Children = Children,
                Level = Level,
                IsLevelManual = IsLevelManual
            };
        }
    }

    // вспомогательный класс для сериализации
    public class SerializableTreeModel
    {
        public List<SerializableNode> Nodes { get; set; }
        public List<List<int>> EP { get; set; }
        public List<List<int>> AP { get; set; }
        public List<int> GoalWeights { get; set; }
        public List<string> FeatureNames { get; set; } // Названия признаков
        public List<string> GoalNames { get; set; } // Названия целей

        public SerializableTreeModel() { }

        public SerializableTreeModel(TreeModel model)
        {
            Nodes = model.Nodes.Select(n => new SerializableNode(n)).ToList();
            EP = ConvertMatrix(model.EP);
            AP = ConvertMatrix(model.AP);
            GoalWeights = model.GoalWeights != null ? new List<int>(model.GoalWeights) : new List<int>();
            FeatureNames = model.FeatureNames != null && model.FeatureNames.Any() 
                ? new List<string>(model.FeatureNames) 
                : new List<string>();
            GoalNames = model.GoalNames != null && model.GoalNames.Any() 
                ? new List<string>(model.GoalNames) 
                : new List<string>();
        }

        private static List<List<int>> ConvertMatrix(int[,] matrix)
        {
            if (matrix == null) return new();
            var list = new List<List<int>>();
            for (int i = 0; i < matrix.GetLength(0); i++)
            {
                var row = new List<int>();
                for (int j = 0; j < matrix.GetLength(1); j++)
                    row.Add(matrix[i, j]);
                list.Add(row);
            }
            return list;
        }

        public TreeModel ToTreeModel()
        {
            var model = new TreeModel 
            { 
                Nodes = Nodes?.Select(n => n.ToNode()).ToList() ?? new List<Node>() 
            };
            if (EP != null && EP.Count > 0)
            {
                int rows = EP.Count;
                // Определяем максимальное количество столбцов среди всех строк
                int cols = EP.Max(row => row?.Count ?? 0);
                if (cols == 0) cols = 1; // Минимум один столбец
                
                model.EP = new int[rows, cols];
                for (int i = 0; i < rows; i++)
                {
                    int rowCols = EP[i]?.Count ?? 0;
                    for (int j = 0; j < cols; j++)
                    {
                        // Если строка короче, заполняем нулями
                        model.EP[i, j] = (j < rowCols) ? EP[i][j] : 0;
                    }
                }
            }

            if (AP != null && AP.Count > 0)
            {
                int rows = AP.Count;
                // Определяем максимальное количество столбцов среди всех строк
                int cols = AP.Max(row => row?.Count ?? 0);
                if (cols == 0) cols = 1; // Минимум один столбец
                
                model.AP = new int[rows, cols];
                for (int i = 0; i < rows; i++)
                {
                    int rowCols = AP[i]?.Count ?? 0;
                    for (int j = 0; j < cols; j++)
                    {
                        // Если строка короче, заполняем нулями
                        model.AP[i, j] = (j < rowCols) ? AP[i][j] : 0;
                    }
                }
            }

            model.GoalWeights = GoalWeights?.ToArray() ?? Array.Empty<int>();
            model.FeatureNames = FeatureNames != null && FeatureNames.Any() 
                ? new List<string>(FeatureNames) 
                : new List<string>();
            model.GoalNames = GoalNames != null && GoalNames.Any() 
                ? new List<string>(GoalNames) 
                : new List<string>();
            return model;
        }
    }
}
