using System;
using ConsoleEngine.Infrastructure.Rendering;

namespace ConsoleEngine.Infrastructure
{
    public interface IConsoleHandler
    {
        public int Width { get; }
        public int Height { get; }
        
        void InitializeConsole();
        void SetTitle(string title);
        void SetCursorVisible(bool visible);
        void Resizable(bool resizable);
        void SetFont(FontInfo fontInfo);
        void Render(Span<Pixel> pixels);
        void Close();
    }
}