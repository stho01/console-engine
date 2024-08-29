using System;

namespace ConsoleEngine.Extensions;

public static class ArrayExtensions
{
    public static T[] To1D<T>(this T[,] array)
    {
        var columns = array.GetLength(0);
        var rows = array.GetLength(1);
        var index = 0;
        var result = new T[columns*rows];
            
        for (var x = 0; x < array.GetLength(0); x++)
        for (var y = 0; y < array.GetLength(1); y++)
        {
            result[index++] = array[x, y];
        }

        return result;
    } 
}