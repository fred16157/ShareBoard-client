using EngineIOSharp.Common.Enum;
using Gma.System.MouseKeyHook;
using Microsoft.Toolkit.Uwp.Notifications;
using SocketIOSharp.Client;
using SocketIOSharp.Common;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
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

            if (Clipboard.ContainsImage())
            {
                builder.AddText("(이미지)");
                if (client != null && client.ReadyState == EngineIOReadyState.OPEN)
                {
                    using (MemoryStream ms = new MemoryStream())
                    {
                        Clipboard.GetImage().Save(ms, ImageFormat.Bmp);
                        string data = Convert.ToBase64String(ms.ToArray());
                        client.Emit("copy-image", data);
                    }
                }
            }
            else if (Clipboard.ContainsText())
            {
                builder.AddText(Clipboard.GetText());
                if(client != null && client.ReadyState == EngineIOReadyState.OPEN)
                {
                    client.Emit("copy-text", Clipboard.GetText());
                }
            }
            
            //딜레이가 없으면 E_UNEXPECTED 에러를 뱉는다
            //왜 딜레이가 있어야 실행되는지는 모르겠음
            Thread.Sleep(500);
            builder.Show();
        }

        void OnPasteText(string data)
        {
            ToastContentBuilder builder = new ToastContentBuilder().AddText("클립보드에 복사됨");
            builder.AddText(data);

            Invoke(new Action(() =>
            {
                Clipboard.SetText(data);
            }));
            
            Thread.Sleep(500);
            builder.Show();
        }

        void OnPasteImage(string data)
        {
            ToastContentBuilder builder = new ToastContentBuilder().AddText("클립보드에 복사됨");
            builder.AddText("(이미지)");

            using(MemoryStream ms = new MemoryStream())
            {
                byte[] buffer = Convert.FromBase64String(data);
                ms.Write(buffer, 0, buffer.Length);
                Invoke(new Action(() =>
                {
                    Clipboard.SetImage(Image.FromStream(ms));
                }));
            }

            Thread.Sleep(500);
            builder.Show();
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

            client.On(SocketIOEvent.ERROR, () =>
            {
                client.Dispose();
            });

            client.On("paste-text", (data) =>
            {
                OnPasteText(data[0].ToString());
            });

            client.On("paste-image", (data) =>
            {
                OnPasteImage(data[0].ToString());
            });
        }

        public void Disconnect()
        {
            client.Dispose();
        }
    }
}
