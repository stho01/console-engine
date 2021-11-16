using System.Collections.Generic;
using System.Linq;

namespace ConsoleEngine.Abstractions.Inputs
{
    public interface IInputHandler
    {
        public KeyState GetKey(Key key) => GetKey((int)key);
        public IEnumerable<int> GetPressedKeyCodes();
        KeyState GetKey(int id);
        void Update();
    }
}