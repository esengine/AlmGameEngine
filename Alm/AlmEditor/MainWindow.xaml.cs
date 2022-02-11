using Love;
using System;
using System.Collections.Generic;
using System.Linq;
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
        public MainWindow()
        {
            InitializeComponent();

            var task = new Task(() =>
            {
                var bootConfig = new BootConfig();
                bootConfig.WindowBorderless = true;
                bootConfig.WindowVsync = true;
                bootConfig.WindowResizable = false;

                var game = new EditorGame();
                Boot.Init();
                Boot.Run(game, bootConfig);
            });

            task.Start();
        }
    }
}
