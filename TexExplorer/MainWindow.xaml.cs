using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
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
using System.Windows.Threading;
using System.Xml;
using HandyControl.Controls;
using HandyControl.Data;
using Microsoft.Win32;
using TexExplorer.Model;
using TexExplorer.ViewModel;
using TextBox = HandyControl.Controls.TextBox;
using Window = System.Windows.Window;

namespace TexExplorer
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        [DllImport("gdi32")]
        static extern int DeleteObject(IntPtr o);

        private readonly BackgroundWorker backgroundWorker = new BackgroundWorker();

        public TexTool Tool;

        private MainViewModel ViewModel;

        public MainWindow()
        {
            InitializeComponent();
            backgroundWorker.DoWork += BackgroundWorker_DoWork;
            backgroundWorker.RunWorkerCompleted += BackgroundWorker_RunWorkerCompleted;
            Tool = new TexTool();
            Tool.FileOpened += Tool_FileOpened;
            Tool.FileRawImage += Tool_FileRawImage;
            GridSizeWidthTextBox.VerifyFunc = UnitVerifyFunc;
            GridSizeHeightTextBox.VerifyFunc = UnitVerifyFunc;
            ViewModel = ViewModelLocator.Current.Main;
            VersionTextBlock.Text = $"版本: {Assembly.GetExecutingAssembly().GetName().Version}";
        }

        /// <summary>
        /// 尺寸文本框验证函数
        /// </summary>
        private OperationResult<bool> UnitVerifyFunc(string str)
        {
            return uint.TryParse(str, out uint v)
                ? v < 10 ? OperationResult.Failed("非有效数字!")
                : OperationResult.Success()
                : OperationResult.Failed("非有效数字!");
        }

        private void BackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            var dialog = (OpenFileDialog)e.Argument;
            Tool.OpenFile(dialog.FileName, dialog.OpenFile());
        }

        private void BackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            Dispatcher.Invoke(() => { ImageViewer.ImageSource = bitmapFrame; });
        }

        private BitmapFrame bitmapFrame;

        private void Tool_FileOpened(object sender, FileOpenedEventArgs e)
        {
            Title = $"TexExplorer - [{e.FileName}]";
            FormatTextBlock.Text = $"Format: {e.Format}";
            SizeTextBlock.Text = $"Size: {e.Size}";
            MipmapsTextBlock.Text = $"Mipmaps: {e.Mipmaps}";
            PlatformTextBlock.Text = $"Platform: {e.Platform}";
            TextureTypeTextBlock.Text = $"Texture Type: {e.TexType}";
            if (e.PreCave)
                HandyControl.Controls.MessageBox.Show(@"Error, this is a pre 'Cave Update' TEX file. If you want to convert this, please use an older version of TEXTool or 'update' the file using the converter found in the offical thread.");
        }

        void Tool_FileRawImage(object sender, FileRawImageEventArgs e)
        {
            IntPtr ip = e.Image.GetHbitmap();
            BitmapSource bitmapSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                ip,
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            bitmapFrame = BitmapFrame.Create(bitmapSource);
            ImageViewer.ImageSource = bitmapFrame;
            var atlasElements = new List<HandyControl.Data.KleiTextureAtlasElement>();
            if (e.AtlasElements.Count != 0)
            {
                SaveAllButton.IsEnabled = true;
                ShowGridButton.Visibility = Visibility.Collapsed;
                GridSizeGrid.Visibility = Visibility.Collapsed;
                ViewModel.IsShowGrid = false;
                foreach (var atlas in e.AtlasElements)
                {
                    var atlasElement = new HandyControl.Data.KleiTextureAtlasElement(atlas.Name, atlas.ImgHMin, atlas.ImgHMax, atlas.ImgVMin, atlas.ImgVMax);
                    atlasElements.Add(atlasElement);
                }
            }
            else
            {
                ShowGridButton.Visibility = Visibility.Visible;
                GridSizeGrid.Visibility = Visibility.Visible;
                ViewModel.IsShowGrid = true;
                ShowGridText.Text = "隐藏网格";
                GridSizeWidthTextBox.Text = "64";
                GridSizeHeightTextBox.Text = "64";
                ImageViewer.ShowGrid = ViewModel.IsShowGrid;
                SaveAllButton.IsEnabled = false;
            }
            ImageViewer.AtlasElements = atlasElements;
            ImageViewer.FileName = e.FileName;
            ImageViewer.FileDirectory = e.FileDirectory;
            AntiElectionButton.Visibility = Visibility.Visible;
            SaveButton.Visibility = Visibility.Visible;
            SaveAllButton.Visibility = Visibility.Visible;
            DeleteObject(ip);
            //zoomLevelToolStripComboBox.Text = string.Format("{0}%", imageBox.Zoom);
        }

        private void Open_OnClick(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                Filter = "Klei Texture Files (*.tex)|*.tex|(Klei Xml File)|*.xml|All Files (*.*)|*.*",
                DefaultExt = "tex"
            };

            if (dialog.ShowDialog(this) == true)
            {
                string[] file = dialog.FileName.Split('.');
                if (file[file.Length - 1] == "xml")
                {

                    TexTool tool = new TexTool();
                    XmlDocument anxml = new XmlDocument();
                    anxml.LoadXml(File.ReadAllText(dialog.FileName));
                    XmlNode atlas = anxml.GetElementsByTagName("Atlas")[0];
                    XmlNode texture = atlas.SelectSingleNode("Texture");
                    string texName = texture.Attributes["filename"].Value;
                    string dictor = System.IO.Path.GetDirectoryName(dialog.FileName) + @"\";
                    string xmlPath = dictor + texName;
                    Console.WriteLine(xmlPath);

                    tool.OpenFile(dialog.SafeFileName, new FileStream(xmlPath, FileMode.Open));
                    int alterWidth = tool.CurrentFileRaw.Width;
                    int alterHeight = tool.CurrentFileRaw.Height;
                    XmlNode Elements = atlas.SelectSingleNode("Elements");
                    XmlNodeList elements = Elements.SelectNodes("Element");
                    foreach (XmlNode node in elements)
                    {
                        string name = node.Attributes["name"].Value.Split('.')[0] + ".png";
                        float u1 = Convert.ToSingle(node.Attributes["u1"].Value);
                        float u2 = Convert.ToSingle(node.Attributes["u2"].Value);
                        float v1 = Convert.ToSingle(node.Attributes["v1"].Value);
                        float v2 = Convert.ToSingle(node.Attributes["v2"].Value);
                        int imageHeight = (int)(alterHeight * v2 - alterHeight * v1);
                        int imageWidth = (int)(alterWidth * u2 - alterWidth * u1);
                        Console.WriteLine("(" + (int)(alterWidth * u1) + "," + (int)(alterHeight - (alterHeight * v1) - imageHeight) + ")  (" + (int)(alterWidth * u2) + "," + (int)(alterHeight - alterHeight * v2 + imageHeight) + ")");
                        System.Drawing.Rectangle cloneRect = new System.Drawing.Rectangle((int)(alterWidth * u1), (int)(alterHeight - (alterHeight * v1) - imageHeight), imageWidth, imageHeight);
                        //Rectangle cloneRect = new Rectangle(0, (int)(alterheight - (alterheight * v1) - imageheight), (int)(alterwidth * u2), (int)(alterheight - alterheight * v2 + imageheight));
                        System.Drawing.Imaging.PixelFormat format = tool.CurrentFileRaw.PixelFormat;
                        Bitmap cloneBitmap = tool.CurrentFileRaw.Clone(cloneRect, format);
                        FileStream wImage = new FileStream(dictor + name, FileMode.Create);
                        cloneBitmap.Save(wImage, ImageFormat.Png);
                        wImage.Close();
                    }
                    System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo("Explorer.exe")
                    {
                        Arguments = "/e,/select," + dialog.FileName
                    };
                    System.Diagnostics.Process.Start(psi);
                    return;
                }
                //backgroundWorker.RunWorkerAsync(dialog);
                OpenFile(dialog);
            }
        }

        private void OpenFile(OpenFileDialog dialog)
        {
            Dispatcher.Invoke(
                DispatcherPriority.Normal,
                (ThreadStart)delegate
               {
                   Tool.OpenFile(dialog.FileName, dialog.OpenFile());
               });
        }

        private void AntiElection_OnClick(object sender, RoutedEventArgs e)
        {
            ImageViewer.AntiElection();
        }

        private void Save_OnClick(object sender, RoutedEventArgs e)
        {
            var result = ImageViewer.SaveFile(false);
            Growl.Clear();
            if (result)
            {
                Growl.Success("图片已导出!");
            }
            else
            {
                Growl.Warning("取消导出!");
            }
        }

        private void SaveAll_OnClick(object sender, RoutedEventArgs e)
        {
            var result = ImageViewer.SaveFile(true);
            Growl.Clear();
            if (result)
            {
                Growl.Success($"图片已导出至{ImageViewer.FileDirectory}\\{ImageViewer.FileName}文件夹!");
            }
            else
            {
                Growl.Warning("未选中需要导出的图片!");
            }
        }

        private void Grid_OnClick(object sender, RoutedEventArgs e)
        {
            ViewModel.IsShowGrid = !ViewModel.IsShowGrid;
            if (ViewModel.IsShowGrid)
            {
                ShowGridText.Text = "隐藏网格";
                GridSizeGrid.Visibility = Visibility.Visible;
            }
            else
            {
                ShowGridText.Text = "显示网格";
                GridSizeGrid.Visibility = Visibility.Collapsed;
                SaveAllButton.IsEnabled = false;
            }
            ImageViewer.ShowGrid = ViewModel.IsShowGrid;
        }

        private void SplitButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            if (GridSizeHeightTextBox != null && GridSizeWidthTextBox != null && ImageViewer != null && ViewModel.IsShowGrid)
            {
                if (!GridSizeHeightTextBox.IsError && !GridSizeWidthTextBox.IsError)
                {
                    ImageViewer.GridWidth = uint.Parse(GridSizeWidthTextBox.Text);
                    ImageViewer.GridHeight = uint.Parse(GridSizeHeightTextBox.Text);
                    ImageViewer.SetGridSize();
                    SaveAllButton.IsEnabled = true;
                }
            }
        }

        private void QQQunButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            Process.Start("tencent://groupwpa/?subcmd=all&param=7B2267726F757055696E223A3538303333323236382C2274696D655374616D70223A313437303132323238337D0A");
        }
        private void GithubButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            Process.Start("https://github.com/tpxxn/TexExplorer");
        }
        private void HandyControlButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            Process.Start("https://gitee.com/tpxxn/HandyControl");
        }
    }
}
