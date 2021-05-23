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
        public enum LogType { Error, Info, Success }

        static bool isConnected = false;
        static bool isConnecting = false;
        SocketIOClient client;
        Combination copyCombo;
        SettingsInfo info;
        
        public MainForm()
        {
            InitializeComponent();

            info = SettingsInfo.ReadSettingsInfo();

            copyCombo = Combination.FromString(info.CopyComboString);
            autoConnectCheckBox.Checked = info.ConnectOnStartup;

            Dictionary<Combination, Action> assignment = new Dictionary<Combination, Action> { 
                { copyCombo, OnCopy } 
            };
            AppDomain.CurrentDomain.ProcessExit += CurrentDomainProcessExit;
            Hook.GlobalEvents().OnCombination(assignment);
        }

        private void CurrentDomainProcessExit(object sender, EventArgs e)
        {
            SettingsInfo.WriteSettingsInfo(info);
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
            addressTextBox.Enabled = false;
            portTextBox.Enabled = false;
            usernameTextBox.Enabled = false;
            passwordTextBox.Enabled = false;
            if(!isConnected && !isConnecting)
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
            SetStatusLabel(LogType.Info, "연결중");
            Invoke(new Action(() =>
            {
                isConnecting = true;
                toggleBtn.Text = "연결 끊기";
            }));
            string address = addressTextBox.Text;
            bool result = ushort.TryParse(portTextBox.Text, out ushort port);
            if (!result)
            {
                SetStatusLabel(LogType.Error, "포트가 올바르지 않음");
                Invoke(new Action(() =>
                {
                    isConnected = false;
                    toggleBtn.Enabled = true;
                    addressTextBox.Enabled = true;
                    portTextBox.Enabled = true;
                    usernameTextBox.Enabled = true;
                    passwordTextBox.Enabled = true;
                    toggleBtn.Text = "연결";                    
                }));
                return;
            }
            info.RemoteAddress = address;
            info.RemotePort = port;
            info.Username = usernameTextBox.Text;
            info.Password = passwordTextBox.Text;
            client = new SocketIOClient(new SocketIOClientOption(EngineIOScheme.http, address, port));

            client.Connect();

            client.On(SocketIOEvent.CONNECTION, () =>
            {
                client.Emit("login", usernameTextBox.Text, passwordTextBox.Text);
            });

            client.On(SocketIOEvent.DISCONNECT, () =>
            {
                Invoke(new Action(() =>
                {
                    isConnected = false;
                    isConnecting = false;
                    toggleBtn.Enabled = true;
                    addressTextBox.Enabled = true;
                    portTextBox.Enabled = true;
                    usernameTextBox.Enabled = true;
                    passwordTextBox.Enabled = true;
                    toggleBtn.Text = "연결";
                }));
            });

            client.On(SocketIOEvent.ERROR, () =>
            {
                SetStatusLabel(LogType.Error, "연결 오류");
                client.Dispose();
            });

            client.On("login-result", (data) =>
            {

                if(data[0].ToObject<bool>())
                {
                    SetStatusLabel(LogType.Success, "연결됨");
                    isConnected = true;
                    isConnecting = false;
                }
                else
                {
                    SetStatusLabel(LogType.Error, "로그인 실패");
                    client.Dispose();
                }
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
            SetStatusLabel(LogType.Info, "연결 해제됨");
            client.Dispose();
        }

        private void AutoConnectCheckBoxCheckedChanged(object sender, EventArgs e)
        {
            info.ConnectOnStartup = autoConnectCheckBox.Checked;
        }

        public void SetStatusLabel(LogType type, string message)
        {
            Color color;
            switch(type)
            {
                case LogType.Info:
                    color = Color.Black;
                    break;
                case LogType.Success:
                    color = Color.Green;
                    break;
                case LogType.Error:
                    color = Color.Red;
                    break;
                default:
                    color = Color.Black;
                    break;
            }

            Invoke(new Action(() =>
            {
                statusLabel.ForeColor = color;
                statusLabel.Text = message;
            }));
        }

        private void MainFormClosing(object sender, FormClosingEventArgs e)
        {
            if (!isConnected && !isConnecting)
            {
                e.Cancel = false;
                return;
            }
            ToastContentBuilder builder = new ToastContentBuilder();
            builder.AddText("프로그램이 최소화됨").AddText("클립보드 정보를 계속 수신하기 위해 프로그램이 최소화되었습니다.").Show();
            e.Cancel = true;
            WindowState = FormWindowState.Minimized;
            ShowInTaskbar = false;
        }

        private void MainFormLoad(object sender, EventArgs e)
        {
            if (info.ConnectOnStartup)
            {
                addressTextBox.Text = info.RemoteAddress;
                portTextBox.Text = info.RemotePort.ToString();
                usernameTextBox.Text = info.Username;
                passwordTextBox.Text = info.Password;
                addressTextBox.Enabled = false;
                portTextBox.Enabled = false;
                usernameTextBox.Enabled = false;
                passwordTextBox.Enabled = false;
                Connect();
            }
        }

        private void NotifyIconMouseDoubleClick(object sender, MouseEventArgs e)
        {
            WindowState = FormWindowState.Normal;
            ShowInTaskbar = true;
        }
    }
}
