using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Forms;

namespace ShareBoard
{
    public partial class RegisterForm : Form
    {
        public RegisterForm()
        {
            InitializeComponent();
        }

        private async void SubmitBtnClick(object sender, EventArgs e)
        {
            if(!passwordTextBox.Text.Equals(checkTextBox.Text))
            {
                MessageBox.Show("비밀번호가 일치하지 않습니다.");
                return;
            }
            addressTextBox.Enabled = false;
            portTextBox.Enabled = false;
            usernameTextBox.Enabled = false;
            passwordTextBox.Enabled = false;
            checkTextBox.Enabled = false;
            submitBtn.Enabled = false;
            JObject obj = new JObject(
                new JProperty("username", usernameTextBox.Text),
                new JProperty("password", passwordTextBox.Text)
            );

            HttpClient client = new HttpClient();
            HttpContent content = new StringContent(obj.ToString(), Encoding.UTF8, "application/json");

            HttpResponseMessage res = await client.PostAsync("http://" + addressTextBox.Text + ":" + portTextBox.Text + "/api/register", content);
            content = res.Content;
            if (!res.IsSuccessStatusCode)
            {
                obj = JObject.Parse(await content.ReadAsStringAsync());
                switch (obj.Value<string>("reason"))
                {
                    case "user_exists":
                        MessageBox.Show("회원가입에 실패했습니다: 이미 존재하는 회원입니다.");
                        break;
                }
            }
            else
            {
                MessageBox.Show("회원가입이 완료되었습니다.");
            }

            addressTextBox.Enabled = true;
            portTextBox.Enabled = true;
            usernameTextBox.Enabled = true;
            passwordTextBox.Enabled = true;
            checkTextBox.Enabled = true;
            submitBtn.Enabled = true;
        }
    }
}
