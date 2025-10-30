using System;
using TreeMethod.Models;

namespace TreeMethod.Models
{
    public static class ProjectData
    {
        public static TreeModel CurrentTree { get; set; } = new TreeModel();

        // Событие, которое оповещает о любых изменениях дерева
        public static event Action TreeChanged;

        public static void RaiseTreeChanged()
        {
            TreeChanged?.Invoke();
        }

        // Обновление матриц после редактирования
        public static void UpdateMatrices(int[,] ep, int[,] ap)
        {
            CurrentTree.EP = ep;
            CurrentTree.AP = ap;
        }
    }
}
