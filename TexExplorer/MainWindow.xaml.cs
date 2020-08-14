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
using System.Xml;
using Microsoft.Win32;
using TexExplorer.Model;

namespace TexExplorer
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        [DllImport("gdi32")]
        static extern int DeleteObject(IntPtr o);

        private BackgroundWorker backgroundWorker;

        public TexTool Tool;

        public MainWindow()
        {
            InitializeComponent();
            backgroundWorker = new BackgroundWorker();
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
            
        }

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
                ip, IntPtr.Zero, Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            var bitmapFrame = BitmapFrame.Create(bitmapSource);
            ImageViewer.ImageSource = bitmapFrame;
            DeleteObject(ip);
            //zoomLevelToolStripComboBox.Text = string.Format("{0}%", imageBox.Zoom);
        }

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
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
                    string texname = texture.Attributes["filename"].Value;
                    string dictor = System.IO.Path.GetDirectoryName(dialog.FileName) + @"\";
                    string xmlpath = dictor + texname;
                    Console.WriteLine(xmlpath);

                    tool.OpenFile(dialog.SafeFileName, new FileStream(xmlpath, FileMode.Open));
                    int alterwidth = tool.CurrentFileRaw.Width;
                    int alterheight = tool.CurrentFileRaw.Height;
                    XmlNode Elements = atlas.SelectSingleNode("Elements");
                    XmlNodeList elements = Elements.SelectNodes("Element");
                    foreach (XmlNode node in elements)
                    {
                        string name = node.Attributes["name"].Value.Split('.')[0] + ".png";
                        float u1 = Convert.ToSingle(node.Attributes["u1"].Value);
                        float u2 = Convert.ToSingle(node.Attributes["u2"].Value);
                        float v1 = Convert.ToSingle(node.Attributes["v1"].Value);
                        float v2 = Convert.ToSingle(node.Attributes["v2"].Value);
                        int imageheight = (int)(alterheight * v2 - alterheight * v1);
                        int imagewidth = (int)(alterwidth * u2 - alterwidth * u1);
                        Console.WriteLine("(" + (int)(alterwidth * u1) + "," + (int)(alterheight - (alterheight * v1) - imageheight) + ")  (" + (int)(alterwidth * u2) + "," + (int)(alterheight - alterheight * v2 + imageheight) + ")");
                        System.Drawing.Rectangle cloneRect = new System.Drawing.Rectangle((int)(alterwidth * u1), (int)(alterheight - (alterheight * v1) - imageheight), imagewidth, imageheight);
                        //Rectangle cloneRect = new Rectangle(0, (int)(alterheight - (alterheight * v1) - imageheight), (int)(alterwidth * u2), (int)(alterheight - alterheight * v2 + imageheight));
                        System.Drawing.Imaging.PixelFormat format = tool.CurrentFileRaw.PixelFormat;
                        Bitmap cloneBitmap = tool.CurrentFileRaw.Clone(cloneRect, format);
                        FileStream wimage = new FileStream(dictor + name, FileMode.Create);
                        cloneBitmap.Save(wimage, ImageFormat.Png);
                        wimage.Close();
                    }
                    System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo("Explorer.exe");
                    psi.Arguments = "/e,/select," + dialog.FileName;
                    System.Diagnostics.Process.Start(psi);
                    return;
                }
                //backgroundWorker.RunWorkerAsync(dialog);
                Tool.OpenFile(dialog.FileName, dialog.OpenFile());
            }
        }
    }
}
