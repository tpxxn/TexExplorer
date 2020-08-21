using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
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

        public MainWindow()
        {
            InitializeComponent();
            backgroundWorker.DoWork += BackgroundWorker_DoWork;
            backgroundWorker.RunWorkerCompleted += BackgroundWorker_RunWorkerCompleted;
            Tool = new TexTool();
            Tool.FileRawImage += Tool_FileRawImage;
        }

        private void BackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            var dialog = (OpenFileDialog)e.Argument;
            Tool.OpenFile(dialog.FileName, dialog.OpenFile());
        }

        private void BackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            Dispatcher.Invoke(() => {ImageViewer.ImageSource = bitmapFrame;});
        }

        private BitmapFrame bitmapFrame;

        void Tool_FileRawImage(object sender, FileRawImageEventArgs e)
        {
            //atlasElementsCountIntToolStripLabel.Text = e.AtlasElements.Count.ToString();
            //atlasElementsListToolStripComboBox.ComboBox.SelectedIndex = -1;
            //atlasElementsListToolStripComboBox.ComboBox.Items.Clear();

            //graphicsPath = null;
            //atlasElementsListToolStripComboBox.Enabled = atlasElementBorderColors.Enabled = false;
            //atlasElementWidthToolStrip.Text = atlasElementHeightToolStrip.Text = atlasElementXToolStrip.Text = atlasElementYToolStrip.Text = "0";

            //if (e.AtlasElements.Count > 0)
            //{
            //    graphicsPath = new GraphicsPath();
            //    atlasElementsListToolStripComboBox.Enabled = atlasElementBorderColors.Enabled = true;
            //    foreach (KleiTextureAtlasElement el in e.AtlasElements)
            //    {
            //        atlasElementsListToolStripComboBox.Items.Add(el);
            //    }
            //}

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
                foreach (var atlas in e.AtlasElements)
                {
                    var atlasElement = new HandyControl.Data.KleiTextureAtlasElement(atlas.Name, atlas.ImgHMin, atlas.ImgHMax, atlas.ImgVMin, atlas.ImgVMax);
                    atlasElements.Add(atlasElement);
                }
            }
            ImageViewer.AtlasElements = atlasElements;
            ImageViewer.FileName = e.FileName;
            ImageViewer.FileDirectory = e.FileDirectory;
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
                (ThreadStart) delegate
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
            ImageViewer.SaveFile(false);
            Growl.Success("图片已保存!");
        }

        private void SaveAll_OnClick(object sender, RoutedEventArgs e)
        {
            ImageViewer.SaveFile(true);
            Growl.Success($"图片已保存至{ImageViewer.FileDirectory}\\{ImageViewer.FileName}文件夹!");
        }

        private void Grid_OnClick(object sender, RoutedEventArgs e)
        {

        }
    }
}
