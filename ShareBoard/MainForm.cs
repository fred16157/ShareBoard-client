using EngineIOSharp.Common.Enum;
using Gma.System.MouseKeyHook;
using Microsoft.Toolkit.Uwp.Notifications;
using Microsoft.WindowsAPICodePack.Dialogs;
using SocketIOSharp.Client;
using SocketIOSharp.Common;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Windows.Foundation.Collections;

namespace ShareBoard
{
    public partial class MainForm : Form
    {
        public enum LogType { Error, Info, Success }

        bool isConnected = false;
        bool isConnecting = false;
        
        SocketIOClient client;
        Combination copyCombo;
        SettingsInfo info;
        long maxSize;
        string filename;
        byte[] fileData;
        bool isCompressed;

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

            ToastNotificationManagerCompat.OnActivated += ToastNotificationManagerCompatOnActivated;
        }

        private void ToastNotificationManagerCompatOnActivated(ToastNotificationActivatedEventArgsCompat e)
        { 
            ToastArguments args = ToastArguments.Parse(e.Argument);
            switch(args.Get("action"))
            {
                case "save-file":
                    Invoke(new Action(() =>
                    {
                        CommonOpenFileDialog dialog = new CommonOpenFileDialog();
                        dialog.IsFolderPicker = true;
                        dialog.Multiselect = false;
                        if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
                        {
                            File.WriteAllBytes(Path.Combine(dialog.FileName, filename), fileData);
                            if (isCompressed) WriteZipArchive(dialog.FileName);
                        }
                    }));
                    break;
            }
        }

        private void WriteZipArchive(string path)
        {
            FileStream zipStream = new FileStream(Path.Combine(path, "temp.zip"), FileMode.Open);
            using(ZipArchive archive = new ZipArchive(zipStream, ZipArchiveMode.Read))
            {
                archive.ExtractToDirectory(path);
            }
            zipStream.Close();
            File.Delete(Path.Combine(path, "temp.zip"));
        }

        private void CurrentDomainProcessExit(object sender, EventArgs e)
        {
            SettingsInfo.WriteSettingsInfo(info);
        }

        public void ShowErrorToast(string message)
        {
            ToastContentBuilder builder = new ToastContentBuilder().AddText("복사되지 않음");
            builder.AddText(message);

            Thread.Sleep(500);
            builder.Show();
        }

        void OnCopy()
        {
            if(!isConnected)
            {
                ShowErrorToast("서버에 연결되지 않았습니다.");
                return;
            }

            ToastContentBuilder builder = new ToastContentBuilder().AddText("클립보드에서 복사됨");

            if(Clipboard.ContainsFileDropList())
            {
                StringCollection collection = Clipboard.GetFileDropList();
                string path = collection[0];
                bool isCompressed = false;
                if(collection.Count > 1 || File.GetAttributes(path).HasFlag(FileAttributes.Directory))
                {
                    path = MakeZipArchive(collection);
                    isCompressed = true;
                }
                FileInfo info = new FileInfo(path);
                if (info.Length > maxSize)
                {
                    ShowErrorToast("파일이 최대 크기를 벗어났습니다 - " + maxSize);
                    return;
                }
                builder.AddText("파일: " + info.Name);
                client.Emit("copy-file", info.Name, File.ReadAllBytes(path), isCompressed);
            }
            else if (Clipboard.ContainsImage())
            {
                builder.AddText("(이미지)");
                using (MemoryStream ms = new MemoryStream())
                {
                    Clipboard.GetImage().Save(ms, ImageFormat.Bmp);
                    string data = Convert.ToBase64String(ms.ToArray());
                    client.Emit("copy-image", data);
                }
            }
            else if (Clipboard.ContainsText())
            {
                builder.AddText(Clipboard.GetText());
                client.Emit("copy-text", Clipboard.GetText());
            }
            else
            {
                ShowErrorToast("지원되는 데이터가 발견되지 않았습니다.");
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

        void OnPasteFile(string name, byte[] data, bool isCompressed)
        {
            filename = name;
            fileData = data;
            this.isCompressed = isCompressed;
            ToastContentBuilder builder = new ToastContentBuilder().AddText("클립보드에 복사됨");
            if (isCompressed) builder.AddText("파일 다수: " + data.Length + "바이트");
            else builder.AddText("파일: " + name);

            builder.AddButton(new ToastButton().SetContent("저장").AddArgument("action", "save-file")).SetBackgroundActivation();

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
                    maxSize = data[1].ToObject<long>();
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

            client.On("paste-file", (data) =>
            {
                OnPasteFile(data[0].ToString(), data[1].ToObject<byte[]>(), data[2].ToObject<bool>());
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

        public string MakeZipArchive(StringCollection collection)
        {
            FileStream zipStream = new FileStream(Path.Combine(Environment.CurrentDirectory, "temp.zip"), FileMode.Create);
            using (ZipArchive archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
            {
                foreach(string filepath in collection)
                {
                    CreateEntryFromAny(archive, filepath);
                }
            }

            return Path.Combine(Environment.CurrentDirectory, "temp.zip");
        }

        public void CreateEntryFromAny(ZipArchive archive, string sourceName, string entryName = "")
        {
            var fileName = Path.GetFileName(sourceName);
            if (File.GetAttributes(sourceName).HasFlag(FileAttributes.Directory))
            {
                CreateEntryFromDirectory(archive, sourceName, Path.Combine(entryName, fileName));
            }
            else
            {
                archive.CreateEntryFromFile(sourceName, Path.Combine(entryName, fileName), CompressionLevel.Fastest);
            }
        }

        public void CreateEntryFromDirectory(ZipArchive archive, string sourceDirName, string entryName = "")
        {
            string[] files = Directory.GetFiles(sourceDirName).Concat(Directory.GetDirectories(sourceDirName)).ToArray();
            archive.CreateEntry(Path.Combine(entryName, Path.GetFileName(sourceDirName)));
            foreach (var file in files)
            {
                CreateEntryFromAny(archive, file, entryName);
            }
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

            ContextMenu notifyIconContextMenu = new ContextMenu();
            notifyIconContextMenu.MenuItems.Add("연결 끊기", (_, ev) => {
                Disconnect();
            });
            notifyIconContextMenu.MenuItems.Add("종료", (_, ev) => Application.Exit());
            notifyIcon.ContextMenu = notifyIconContextMenu;
        }

        private void NotifyIconMouseDoubleClick(object sender, MouseEventArgs e)
        {
            WindowState = FormWindowState.Normal;
            ShowInTaskbar = true;
        }

        private void RegisterBtnClick(object sender, EventArgs e)
        {
            new RegisterForm().Show();
        }
    }
}
