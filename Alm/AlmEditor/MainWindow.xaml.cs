using Love;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Alm
{

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : System.Windows.Window
    {
        [DllImport("User32.dll ", SetLastError = true, EntryPoint = "SetParent")]
        private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);


        [DllImport("user32.dll", EntryPoint = "SendMessage", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int SendMessage(IntPtr hwnd, uint wMsg, int wParam, int lParam); //对外部软件窗口发送一些消息(如 窗口最大化、最小化等)


        [DllImport("user32.dll ", EntryPoint = "ShowWindow")]
        public static extern int ShowWindow(IntPtr hwnd, int nCmdShow);

        private System.Windows.Forms.PictureBox pp;

        public MainWindow()
        {
            InitializeComponent();

            pp = new System.Windows.Forms.PictureBox
            {
                Width = 500,
                Height = 400,
                Bounds = new System.Drawing.Rectangle(0, 0, 500, 400),
                Name = "PictureBox"
            };
            form1.Child = pp;

            Timer.EnableLimitMaxFPS(60);

            var bootConfig = new BootConfig();
            bootConfig.WindowBorderless = true;
            bootConfig.WindowVsync = true;
            bootConfig.WindowResizable = false;

            var game = new EditorGame();
            game.OnLoadCallback = OnLoadCallback;

            //Boot.Init();
            //Boot.Run(game, bootConfig);

            ContentRendered += MainWindow_ContentRendered;
        }

        private void MainWindow_ContentRendered(object? sender, EventArgs e)
        {
            Console.WriteLine("123");
        }

        void OnLoadCallback(IntPtr ptr) {
            IntPtr hpanel1 = pp.Handle;

            SetParent(ptr, hpanel1);

            ShowWindow(ptr, 3);
        }
    }
}
