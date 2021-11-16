namespace ConsoleEngine.Abstractions.Inputs
{
    public struct KeyState
    {
        public bool Pressed { get; set; }
        public bool Released { get; set; }
        public bool Held { get; set; }
        public int Index { get; set; }
    }
}