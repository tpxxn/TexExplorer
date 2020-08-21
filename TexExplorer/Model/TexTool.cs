#region License
/*
Klei Studio is licensed under the MIT license.
Copyright © 2013 Matt Stevens

All rights reserved.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/
#endregion License

using System;
using System.Drawing;
using System.ComponentModel;
using System.IO;
using System.Linq;
using KleiLib;
using SquishNET;

using System.Collections.Generic;
using System.Xml;

namespace TexExplorer.Model
{
    public class KleiTextureAtlasElement
    {
        public string Name { get; set; }
        public int ImgHMin { get; set; }
        public int ImgHMax { get; set; }
        public int ImgVMin { get; set; }
        public int ImgVMax { get; set; }

        public KleiTextureAtlasElement(string name, int u1, int u2, int v1, int v2)
        {
            this.Name = name;
            this.ImgHMin = u1;
            this.ImgHMax = u2;
            this.ImgVMin = v1;
            this.ImgVMax = v2;
        }
    }

    public class FileOpenedEventArgs : EventArgs
    {
        public string FileName { get; set; }
        public string Platform { get; set; }
        public string Format { get; set; }
        public string Size { get; set; }
        public string Mipmaps { get; set; }
        public string TexType { get; set; }
        public bool PreCave { get; set; }

        public FileOpenedEventArgs(string filename)
        {
            this.FileName = filename;
        }
    }

    public class FileRawImageEventArgs : EventArgs
    {
        public Bitmap Image { get; set; }
        public List<KleiTextureAtlasElement> AtlasElements { get; set; }
        public string FileName { get; set; }

        public FileRawImageEventArgs(Bitmap image, List<KleiTextureAtlasElement> elements, string fileName)
        {
            this.Image = image;
            this.AtlasElements = elements;
            this.FileName = fileName;
        }
    }

    public delegate void FileOpenedEventHandler(object sender, FileOpenedEventArgs e);
    public delegate void FileRawImageEventHandler(object sender, FileRawImageEventArgs e);
    public delegate void ProgressUpdate(int value);

    public class TexTool
    {
        public TEXFile CurrentFile;
        public Bitmap CurrentFileRaw;

        public event FileOpenedEventHandler FileOpened;
        public event FileRawImageEventHandler FileRawImage;

        public event ProgressUpdate OnProgressUpdate;


        #region Util

        public static class EnumHelper<T>
        {
            public static string GetEnumDescription(string value)
            {
                Type type = typeof(T);
                var name = Enum.GetNames(type).Where(f => f.Equals(value, StringComparison.CurrentCultureIgnoreCase)).Select(d => d).FirstOrDefault();

                if (name == null)
                {
                    return string.Empty;
                }
                var field = type.GetField(name);
                var customAttribute = field.GetCustomAttributes(typeof(DescriptionAttribute), false);
                return customAttribute.Length > 0 ? ((DescriptionAttribute)customAttribute[0]).Description : name;
            }

            public static T GetValueFromDescription(string description)
            {
                var type = typeof(T);
                if (!type.IsEnum) throw new InvalidOperationException();
                foreach (var field in type.GetFields())
                {
                    if (Attribute.GetCustomAttribute(field,
                        typeof(DescriptionAttribute)) is DescriptionAttribute attribute)
                    {
                        if (attribute.Description == description)
                            return (T)field.GetValue(null);
                    }
                    else
                    {
                        if (field.Name == description)
                            return (T)field.GetValue(null);
                    }
                }
                throw new ArgumentException("Not found.", nameof(description));
            }
        }

        #endregion

        public void OpenFile(string filename, Stream stream)
        {
            CurrentFile = new TEXFile(stream);

            var eArgs = new FileOpenedEventArgs(filename)
            {
                Platform = ((TEXFile.Platform) this.CurrentFile.File.Header.Platform).ToString("F"),
                Format = ((TEXFile.PixelFormat) this.CurrentFile.File.Header.PixelFormat).ToString("F"),
                Mipmaps = this.CurrentFile.File.Header.NumMips.ToString(),
                PreCave = this.CurrentFile.IsPreCaveUpdate(),
                TexType = EnumHelper<TEXFile.TextureType>.GetEnumDescription(
                    ((TEXFile.TextureType) this.CurrentFile.File.Header.TextureType).ToString())
            };

            var mipmap = CurrentFile.GetMainMipmap();

            eArgs.Size = mipmap.Width + "x" + mipmap.Height;

            OnOpenFile(eArgs);

            byte[] argbData;

            switch ((TEXFile.PixelFormat)CurrentFile.File.Header.PixelFormat)
            {
                case TEXFile.PixelFormat.DXT1:
                    argbData = Squish.DecompressImage(mipmap.Data, mipmap.Width, mipmap.Height, SquishFlags.Dxt1);
                    break;
                case TEXFile.PixelFormat.DXT3:
                    argbData = Squish.DecompressImage(mipmap.Data, mipmap.Width, mipmap.Height, SquishFlags.Dxt3);
                    break;
                case TEXFile.PixelFormat.DXT5:
                    argbData = Squish.DecompressImage(mipmap.Data, mipmap.Width, mipmap.Height, SquishFlags.Dxt5);
                    break;
                case TEXFile.PixelFormat.ARGB:
                    argbData = mipmap.Data;
                    break;
                default:
                    throw new Exception("Unknown pixel format?");
            }

            string atlasExt = "xml";
            FileInfo fileInfo = new FileInfo(filename);
            string fileDir = fileInfo.DirectoryName;
            string fileNameWithoutExt = fileInfo.Name.Replace(fileInfo.Extension, "");
            string atlasDataPath = fileDir + @"\" + fileNameWithoutExt + "." + atlasExt;
            Console.WriteLine("XmlPath:" + atlasDataPath);
            List<KleiTextureAtlasElement> atlasElements = new List<KleiTextureAtlasElement>();

            if (File.Exists(atlasDataPath))
            {
                atlasElements = ReadAtlasData(atlasDataPath, mipmap.Width, mipmap.Height);
            }
            else
            {
                Console.WriteLine("不存在");
            }

            var imgReader = new BinaryReader(new MemoryStream(argbData));

            Bitmap pt = new Bitmap(mipmap.Width, mipmap.Height);

            for (int y = 0; y < mipmap.Height; y++)
            {
                for (int x = 0; x < mipmap.Width; x++)
                {
                    byte r = imgReader.ReadByte();
                    byte g = imgReader.ReadByte();
                    byte b = imgReader.ReadByte();
                    byte a = imgReader.ReadByte();
                    pt.SetPixel(x, y, Color.FromArgb(a, r, g, b));
                }

                OnProgressUpdate?.Invoke(y * 100 / mipmap.Height);
            }

            pt.RotateFlip(RotateFlipType.RotateNoneFlipY);

            CurrentFileRaw = pt;

            OnRawImage(new FileRawImageEventArgs(pt, atlasElements, fileNameWithoutExt));
        }

        private List<KleiTextureAtlasElement> ReadAtlasData(string path, int mipmapWidth, int mipmapHeight)
        {
            var atlasElements = new List<KleiTextureAtlasElement>();
            try
            {
                XmlDocument xDoc = new XmlDocument();
                xDoc.Load(path);

                XmlNode xNodeElements = xDoc.SelectSingleNode("Atlas/Elements");
                foreach (XmlNode xChild in xNodeElements.ChildNodes)
                {
                    string name = xChild.Attributes.GetNamedItem("name").Value;
                    double u1 = Convert.ToDouble(xChild.Attributes.GetNamedItem("u1").Value);
                    double u2 = Convert.ToDouble(xChild.Attributes.GetNamedItem("u2").Value);

                    /* NORMAL THE Y-AXIS */
                    double v1 = Convert.ToDouble(xChild.Attributes.GetNamedItem("v1").Value);
                    double v2 = Convert.ToDouble(xChild.Attributes.GetNamedItem("v2").Value);

                    /* INVERT THE Y-AXIS */
                    v1 = 1 - v1;
                    v2 = 1 - v2;

                    const double margin = 0.5;
                    int imgHMin = Convert.ToInt16(u1 * mipmapWidth - margin);
                    int imgHMax = Convert.ToInt16(u2 * mipmapWidth - margin);
                    int imgVMin = Convert.ToInt16(v1 * mipmapHeight - margin);
                    int imgVMax = Convert.ToInt16(v2 * mipmapHeight - margin);

                    atlasElements.Add(new KleiTextureAtlasElement(name, imgHMin, imgHMax, imgVMin, imgVMax));
                }
            }
            catch (Exception e)
            {

                Console.WriteLine("错误：" + e.Message);
            }
            return atlasElements;
        }



        public void SaveFile(string filePath)
        {
            CurrentFileRaw.Save(filePath);
        }

        public void SaveFile(Stream fileStream)
        {
            CurrentFileRaw.Save(fileStream, System.Drawing.Imaging.ImageFormat.Png);
        }

        protected virtual void OnOpenFile(FileOpenedEventArgs args)
        {
            FileOpenedEventHandler handler = FileOpened;
            if (handler != null)
            {
                if (handler.Target is ISynchronizeInvoke target && target.InvokeRequired)
                    target.Invoke(handler, new object[] { this, args });
                else
                    handler(this, args);
            }
        }

        protected virtual void OnRawImage(FileRawImageEventArgs args)
        {
            FileRawImageEventHandler handler = FileRawImage;
            if (handler != null)
            {
                if (handler.Target is ISynchronizeInvoke target && target.InvokeRequired)
                    target.Invoke(handler, new object[] { this, args });
                else
                    handler(this, args);
            }
        }
    }
}
