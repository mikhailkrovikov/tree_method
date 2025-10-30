namespace TreeMethod.Models
{
    public class RationalSolution
    {
        public List<string> Elements { get; set; } = new();
        public int Score { get; set; }

        public override string ToString() =>
            $"{string.Join(", ", Elements)}  →  Оценка: {Score}";
    }
}
