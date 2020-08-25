using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using TexExplorer.Model;

namespace TexExplorer
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            var args = e.Args;
            if (args.Length == 1) // 打开为
            {
                Global.OpenInPath = args[0];
            }
            else if (args.Length == 2) // 命令行
            {
                var inputFile = args[0];
                var outputFile = args[1];
                var tool = new TexTool();
                tool.FileRawImage += (s, ev) =>
                {
                    tool.SaveFile(outputFile);
                };
                tool.OpenFile(inputFile, new FileStream(inputFile, FileMode.Open, FileAccess.Read));
                Current.Shutdown();
            }
        }
    }
}
