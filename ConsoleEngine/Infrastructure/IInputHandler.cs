namespace ConsoleEngine.Infrastructure
{
    public interface IInputHandler
    {
        public KeyState GetKey(Key key) => GetKey((int)key);
        KeyState GetKey(int id);
        void Update();
    }
}