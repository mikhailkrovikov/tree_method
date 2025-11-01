using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace TreeMethod.Models
{
    public class TreeModel
    {
        public List<Node> Nodes { get; set; } = new();
        public int[,] EP { get; set; }
        public int[,] AP { get; set; }
        public int[] GoalWeights { get; set; }
        public List<string> FeatureNames { get; set; } = new();
        public List<string> GoalNames { get; set; } = new();

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
            return data?.ToTreeModel();
        }
    }

    public class SerializableNode
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Type { get; set; }
        public List<int> Children { get; set; } = new();
        
        public SerializableNode() { }
        
        public SerializableNode(Node node)
        {
            Id = node.Id;
            Name = node.Name;
            Type = NodeTypeConverter.ToJsonFormat(node.Type);
            Children = node.Children;
        }
        
        public Node ToNode()
        {
            return new Node
            {
                Id = Id,
                Name = Name,
                Type = NodeTypeConverter.FromJsonFormat(Type),
                Children = Children
            };
        }
    }

    public class SerializableTreeModel
    {
        public List<SerializableNode> Nodes { get; set; }
        public List<List<int>> EP { get; set; }
        public List<List<int>> AP { get; set; }
        public List<int> GoalWeights { get; set; }
        public List<string> FeatureNames { get; set; }
        public List<string> GoalNames { get; set; }

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
                int cols = EP.Max(row => row?.Count ?? 0);
                if (cols == 0) cols = 1;
                
                model.EP = new int[rows, cols];
                for (int i = 0; i < rows; i++)
                {
                    int rowCols = EP[i]?.Count ?? 0;
                    for (int j = 0; j < cols; j++)
                    {
                        model.EP[i, j] = (j < rowCols) ? EP[i][j] : 0;
                    }
                }
            }

            if (AP != null && AP.Count > 0)
            {
                int rows = AP.Count;
                int cols = AP.Max(row => row?.Count ?? 0);
                if (cols == 0) cols = 1;
                
                model.AP = new int[rows, cols];
                for (int i = 0; i < rows; i++)
                {
                    int rowCols = AP[i]?.Count ?? 0;
                    for (int j = 0; j < cols; j++)
                    {
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
