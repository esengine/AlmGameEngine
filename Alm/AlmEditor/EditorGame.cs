using Love;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Alm
{
    internal class EditorGame : Scene
    {
        public Action<IntPtr>? OnLoadCallback;

        public override void Load()
        {
            Love2dDll.inner_wrap_love_dll_get_win32_handle_value(out IntPtr ptr);
            if (OnLoadCallback != null) OnLoadCallback(ptr);
        }

        public override void Draw()
        {
            Graphics.Print("Hello, World!", 400, 300);
        }
    }
}
