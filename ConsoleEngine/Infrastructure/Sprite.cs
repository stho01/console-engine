namespace ConsoleEngine.Infrastructure
{
    public class Sprite
    {
        // public Sprite(string[] data) : this() {}
        public Sprite(char[,] data)
        {
            Data = data;
            Width = data.GetLength(0);
            Height = data.GetLength(1);
        }

        public char[,] Data { get; }
        public int Width { get; }
        public int Height { get; }
    }
}