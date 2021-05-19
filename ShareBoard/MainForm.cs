using Gma.System.MouseKeyHook;
using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ShareBoard
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
            Combination copyCombo = Combination.FromString("Control+Alt+C");

            Dictionary<Combination, Action> assignment = new Dictionary<Combination, Action> { 
                { copyCombo, OnCopy } 
            };

            Hook.GlobalEvents().OnCombination(assignment);
        }

        void OnCopy()
        {
            ToastContentBuilder builder = new ToastContentBuilder().AddText("클립보드에 복사됨");
            
            //if(Clipboard.ContainsImage())
            //{
            //    Guid guid = Guid.NewGuid();
            //    ResizeImage(Clipboard.GetImage(), 200, 200).Save(guid.ToString() + ".bmp");
            //    builder.AddInlineImage(new Uri("file:///" + Environment.CurrentDirectory + guid.ToString() + ".bmp"));
            //    File.Delete(guid.ToString() + ".bmp");
            //}
            /*else */
            if (Clipboard.ContainsText())
            {
                builder.AddText(Clipboard.GetText());
            }
            //딜레이가 없으면 E_UNEXPECTED 에러를 뱉는다
            //왜 딜레이가 있어야 실행되는지는 모르겠음
            Thread.Sleep(500);
            builder.Show();
        }

        public static Bitmap ResizeImage(Image image, int width, int height)
        {
            var destRect = new Rectangle(0, 0, width, height);
            var destImage = new Bitmap(width, height);

            destImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            using (var graphics = Graphics.FromImage(destImage))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                using (var wrapMode = new ImageAttributes())
                {
                    wrapMode.SetWrapMode(WrapMode.TileFlipXY);
                    graphics.DrawImage(image, destRect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, wrapMode);
                }
            }

            return destImage;
        }
    }
}
