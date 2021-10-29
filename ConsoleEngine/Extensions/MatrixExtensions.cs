namespace ConsoleEngine.Extensions
{
    public static class MatrixExtensions
    {
        public static T[,] Transpose<T>(this T[,] source)
        {
            var dest = new T[source.GetLength(1), source.GetLength(0)];
            for (var x = 0; x < source.GetLength(0); x++)
            for (var y = 0; y < source.GetLength(1); y++) {
                dest[y, x] = source[x, y];
            }
            return dest;
        }
    }
}