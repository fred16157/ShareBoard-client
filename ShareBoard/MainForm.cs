using EngineIOSharp.Common.Enum;
using Gma.System.MouseKeyHook;
using Microsoft.Toolkit.Uwp.Notifications;
using SocketIOSharp.Client;
using SocketIOSharp.Common;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Threading;
using System.Windows.Forms;

namespace ShareBoard
{
    public partial class MainForm : Form
    {
        static bool isConnected = false;
        SocketIOClient client;
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
                if(client != null && client.ReadyState == EngineIOReadyState.OPEN)
                {
                    client.Emit("copy", Clipboard.GetText());
                }
            }
            
            //딜레이가 없으면 E_UNEXPECTED 에러를 뱉는다
            //왜 딜레이가 있어야 실행되는지는 모르겠음
            Thread.Sleep(500);
            builder.Show();
        }

        void OnPaste(string data)
        {
            ToastContentBuilder builder = new ToastContentBuilder().AddText("클립보드에 복사됨");
            builder.AddText(data);

            Clipboard.SetText(data);

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

        private void ToggleBtnClick(object sender, EventArgs e)
        {
            toggleBtn.Enabled = false;
            addressTextBox.Enabled = false;
            portTextBox.Enabled = false;
            if(!isConnected)
            {
                Connect();
            }
            else
            {
                Disconnect();
            }
        }

        public void Connect()
        {
            string address = addressTextBox.Text;
            ushort port = ushort.Parse(portTextBox.Text);
            client = new SocketIOClient(new SocketIOClientOption(EngineIOScheme.http, address, port));

            client.Connect();

            client.On(SocketIOEvent.CONNECTION, () =>
            {
                Invoke(new Action(() =>
                {
                    toggleBtn.Enabled = true;
                    isConnected = true;
                    toggleBtn.Text = "연결 끊기";
                }));
            });

            client.On(SocketIOEvent.DISCONNECT, () =>
            {
                Invoke(new Action(() =>
                {
                    isConnected = false;
                    toggleBtn.Enabled = true;
                    addressTextBox.Enabled = true;
                    portTextBox.Enabled = true;
                    toggleBtn.Text = "연결";
                }));
            });

            client.On("paste", (data) =>
            {
                OnPaste(data[0].ToString());
            });
        }

        public void Disconnect()
        {
            client.Dispose();
        }
    }
}
