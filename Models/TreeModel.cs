using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace TreeMethod.Models
{
    public class TreeModel
    {
        public List<Node> Nodes { get; set; } = new();
        public int[,] EP { get; set; }     // Матрица элементов × признаки
        public int[,] AP { get; set; }     // Матрица целей × признаки
        public int[] GoalWeights { get; set; }

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
            return data?.ToTreeModel();
        }
    }

    // вспомогательный класс для сериализации
    public class SerializableTreeModel
    {
        public List<Node> Nodes { get; set; }
        public List<List<int>> EP { get; set; }
        public List<List<int>> AP { get; set; }
        public List<int> GoalWeights { get; set; }

        public SerializableTreeModel() { }

        public SerializableTreeModel(TreeModel model)
        {
            Nodes = model.Nodes;
            EP = ConvertMatrix(model.EP);
            AP = ConvertMatrix(model.AP);
            GoalWeights = model.GoalWeights != null ? new List<int>(model.GoalWeights) : new List<int>();
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
            var model = new TreeModel { Nodes = Nodes };
            if (EP != null && EP.Count > 0)
            {
                int rows = EP.Count;
                int cols = EP[0].Count;
                model.EP = new int[rows, cols];
                for (int i = 0; i < rows; i++)
                    for (int j = 0; j < cols; j++)
                        model.EP[i, j] = EP[i][j];
            }

            if (AP != null && AP.Count > 0)
            {
                int rows = AP.Count;
                int cols = AP[0].Count;
                model.AP = new int[rows, cols];
                for (int i = 0; i < rows; i++)
                    for (int j = 0; j < cols; j++)
                        model.AP[i, j] = AP[i][j];
            }

            model.GoalWeights = GoalWeights?.ToArray() ?? new int[0];
            return model;
        }
    }
}
