using System;
using System.Linq;
using Microsoft.Toolkit.HighPerformance;

namespace ConsoleEngine.Infrastructure.Rendering
{
    public class Sprite
    { 
        public static Sprite FromCharArray(char[,] data)
        {
            var pxData = new Pixel[data.GetLength(0), data.GetLength(1)];
            
            for (var x = 0; x < data.GetLength(0); x++)
            for (var y = 0; y < data.GetLength(1); y++)
            {
                pxData[x, y] = new Pixel {
                    Char = data[x, y],
                    ForegroundColor = ConsoleColor.White,
                    BackgroundColor = ConsoleColor.Black
                };
            }
            return new Sprite(pxData);
        }
        
        public static Sprite FromString(string data)
        {
            var lines = data.Split(Environment.NewLine);
            return FromStringArray(lines);
        }
        
        public static Sprite FromStringArray(string[] data)
        {
            var width = data.Max(x => x.Length);
            
            var pxData = new Pixel[width, data.Length];
            
            for (var y = 0; y < data.Length; y++)
            {
                var row = data[y].PadRight(width, ' ');
                for (var x = 0; x < row.Length; x++)
                {
                    pxData[x, y] = new Pixel {
                        Char = row[x],
                        ForegroundColor = ConsoleColor.White,
                        BackgroundColor = ConsoleColor.Black
                    };
                }
            }

            return new Sprite(pxData);
        }
        
        public Sprite(Pixel[,] data)
        {
            Data = data;
            Width = data.GetLength(0);
            Height = data.GetLength(1);
        }

        public Pixel[,] Data { get; }
        public Span2D<Pixel> DataSpan => new(Data);
        
        public int Width { get; }
        public int Height { get; }
    }
}