using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace ShareBoard
{
    public class SettingsInfo
    {
        public bool ConnectOnStartup { get; set; }
        public string CopyComboString { get; set; }

        public string RemoteAddress { get; set; }
        public int RemotePort { get; set; }

        public string Username { get; set; }
        public string Password { get; set; }
        
        public SettingsInfo(bool connectOnStartup, string copyComboString)
        {
            ConnectOnStartup = connectOnStartup;
            CopyComboString = copyComboString;
            RemoteAddress = "";
            RemotePort = -1;
            Username = "";
            Password = "";
        }

        public static void WriteSettingsInfo(SettingsInfo info)
        {
            File.WriteAllText(Path.Combine(Environment.CurrentDirectory, "settings.json"), JsonConvert.SerializeObject(info));
        }

        public static SettingsInfo ReadSettingsInfo()
        {
            if (!File.Exists(Path.Combine(Environment.CurrentDirectory, "settings.json"))) return new SettingsInfo(false, "Control+Alt+C");
            return JsonConvert.DeserializeObject<SettingsInfo>(File.ReadAllText(Path.Combine(Environment.CurrentDirectory, "settings.json")));
        }
    }
}
