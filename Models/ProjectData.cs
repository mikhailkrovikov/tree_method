using System;

namespace TreeMethod.Models
{
    public static class ProjectData
    {
        public static TreeModel CurrentTree { get; set; } = new TreeModel();

        public static event Action TreeChanged;

        public static void RaiseTreeChanged()
        {
            TreeChanged?.Invoke();
        }

        public static void UpdateMatrices(int[,] ep, int[,] ap)
        {
            CurrentTree.EP = ep;
            CurrentTree.AP = ap;
        }
    }
}
