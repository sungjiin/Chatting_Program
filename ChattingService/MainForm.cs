using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Net;

namespace ChattingService
{
	public partial class MainForm : Form
	{
		private IChat service;
		private ChatUI ui;

		private enum Service {	Server, Client	};

		public MainForm()
		{
			InitializeComponent();
			tabcService.Hide();
			this.Size = new Size(this.Size.Width, this.Size.Height-370);
			txtServerIP.Text = "127.0.0.1";
            cbxFont.SelectedIndex = 1;
            cbxClients.SelectedIndex = 0;
			// ----------------------- ChatUI 연동 -----------------------
			ui = new ChatUI(this, lbMain, txtChat, cbxClients, lbxClients);
			ui.HookServerUI(txtOpenPort);
			ui.HookClientUI(txtServerIP, txtServerPort, txtNick);
			// ----------------------- ChatUI 연동 -----------------------
		}

        // 서버나 클라이언트 시작 버튼
		private void btnServiceStart_Click(object sender, EventArgs e)
		{
			Button btn = (Button)sender;
			Service s = btn.Name == "btnServerStart" ?
					Service.Server : Service.Client;
            //탭 안에 있는 시작 버튼의 이름이 btnServerStart인가 btnClientStart인가에 따라서 함수 사용
			if( btn.Text == "시작" )
			{				
				ServiceStart(s);
				btn.Text = "종료";
			}
			else
			{
				ServiceStop(s);
				btn.Text = "시작";
			}
		}

        //채팅 서버나 채팅을 이용하려는 클라이언트가 시작버튼을 눌렀을 경우 발생하는 함수
		private void ServiceStart(Service type)
		{
            //만약 서버가 시작 버튼을 눌렀을때
            //먼저 포트를 사용할 수 있는 지 확인하고, 그 이후 탭 페이지를 설정하게된다.
            //ChatUI클래스에 구현된 기능들로 값을 넘기거나 서버UI의 설정을 변경한다.
			if( type == Service.Server )
			{
				if( !MainFormTools.CheckPort(txtOpenPort.Text) ) return;

				SetTabPages(true, tabServerStart);
				ui.SetServerUI(true);

				service = new ChattingServer(ui);
				service.Start();
                button1.Enabled = false;

                tabService.Text = "서버 서비스";
			}
			else
			{
                //클라이언트가 시작 버튼을 눌렀을 경우
                //연결 할 수 있는 서버의 아이피인지, 포트인지를 확인한다.
                // 그 이후 ChatUI에 구현된 기능을 이용하여 클라이언트UI의 설정을 변경한다.
				if( !MainFormTools.CheckPort(txtServerPort.Text) ||
				!MainFormTools.CheckIPAddress(txtServerIP.Text) ) return;

				SetTabPages(true, tabClientStart);
				ui.SetClientUI(true);

				service = new ChattingClient(ui);
				service.Start();

				tabService.Text = "클라이언트 서비스";
			}

			txtSend.Focus();
		}

        //서비스 종료 버튼을 눌렀을 경우 서버는 ChatServer의 Stop() 함수를 이용하며 (인터페이스를 활용한 오버라이드)
        //클라이언트는 ChatClient에 있는 Stop()함수를 이용한다.
		private void ServiceStop(Service type)
		{		
			service.Stop();
			service = null;
			GC.Collect();
			GC.WaitForPendingFinalizers();

			SetTabPages(false, null);
			ui.SetClientUI(false);
			ui.SetServerUI(false);

			if( type == Service.Client )
				tabcStart.SelectedIndex = 1;				
		}

        //탬 페이지 설정을 위한 함수
        private void SetTabPages(bool isStart, TabPage tab)
		{
			if( isStart )
			{
				tabcService.Show();
				this.Size = new Size(this.Size.Width, this.Size.Height + 370);
				tabcStart.TabPages.Clear();
				tabcStart.TabPages.Add(tab);
			}
			else
			{
				tabcService.Hide();
				this.Size = new Size(this.Size.Width, this.Size.Height - 370);
				tabcStart.TabPages.Clear();
				tabcStart.TabPages.Add(tabServerStart);
				tabcStart.TabPages.Add(tabClientStart);
			}
		}

        //Send버튼을 눌렀을 경우 원하는 기능들의 ChatCode와 메세지를 함께 서버로 보낸다.
		private void btnSend_Click(object sender, EventArgs e)
		{	
			QueueData d;
			int msgTo = cbxClients.SelectedIndex;
            if (msgTo == 0)
            {
                if (txtSend.Text == "!주사위")
                {
                    d = new QueueData(ChatCode.Msg, ui.Nick, null, "님이 주사위를 던져서 값이 " + RandNum() + "이 나왔습니다.");
                }
                else
                {
                    d = new QueueData(ChatCode.Msg, ui.Nick, null, txtSend.Text);
                }
            }
            else if (msgTo == 1)
            {
                d = new QueueData(ChatCode.AllMessage, ui.Nick, null, txtSend.Text);
            }
            else if(msgTo == 2)
            {
                d = new QueueData
                    (ChatCode.Whisper, ui.Nick, lbxClients.SelectedItem + "", txtSend.Text);

            }
            else
            {
                d = new QueueData
                    (ChatCode.Notify, ui.Nick, lbxClients.SelectedItem + "", txtSend.Text);
            }
            service.Send(d);
			txtSend.Clear();
			txtSend.Focus();
		}
        
        public int RandNum()
        {
            int i;
            Random r = new Random();
            i = r.Next(1, 100);
            return i;
        }

        private void txtSend_KeyDown(object sender, KeyEventArgs e)
		{
			if( e.KeyCode == Keys.Enter )
			{
				btnSend.PerformClick();
				txtSend.Clear();
			}
		}

		private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
		{
			if(service != null) service.Stop();
		}


        //서버의 경우 리스트박스의 인덱스(유저)를 더블클릭할 경우 킥(강제퇴장)기능
        //클라이언트의 경우 리스트박스의 인덱스(유저)를 더블클릭 할 경우 귓속말 기능
        private void lbxClients_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            Service s = tabService.Text == "서버 서비스" ?
                    Service.Server : Service.Client;
            kick(s); //바로 밑의 kick함수이다.
        }


        //이름은 Kick이지만 
        //서버의 경우에는 해당 유저를 강제퇴장 하겠냐는 메세지박스를 뜨며 Yes를 누르면 강제퇴장을 시킨다.
        //클라이언트의 경우 그사람의 닉네임을 이용하여 귓속말 기능을 사용할 수있다.
        private void kick(Service type)
        {
            if(type == Service.Server)
            {
                DialogResult result = 
                MessageBox.Show("해당 유저를 강퇴하시겠습니까?", "Kick", MessageBoxButtons.YesNo);
                if(result == DialogResult.Yes)
                {
                    QueueData d;
                    d = new QueueData(ChatCode.Kick, lbxClients.SelectedItem.ToString(), null, null);
                    service.Send(d);
                    txtSend.Focus();
                }
            }
            else
            {//cbxClients는 [전체 채팅, 전체 쪽지, 귓속말]이 들어있는 콤보박스이다.
                if (lbxClients.SelectedItem.ToString() == ui.Nick)
                {
                    cbxClients.SelectedIndex = 0; // 전체 채팅
                    txtSend.Focus();
                }
                else
                {
                    cbxClients.SelectedIndex = 2; //귓속말
                    txtSend.Focus();
                }
            }
        }

        
        //운영자에게 건의사항을 보내기 위한 버튼기능
        private void button1_Click(object sender, EventArgs e)
        { 
            string input;
            //visual basic의 기능인 inputbox 기능을 사용하였다.
            input = Microsoft.VisualBasic.Interaction.InputBox("채팅 프로그램을 이용중 불편한 점이나 불건전한 사용자가 있을 경우 건의 바랍니다.", "운영자에게 건의하기", "");
            
            if (input.Length > 0)
            {
                QueueData d;
                d = new QueueData(ChatCode.Inquiry, ui.Nick, null, input);
                service.Send(d);
                txtSend.Focus();
            }
            else
            {
            }
            
        }

        //텍스트박스 폰트크기 조절
        private void cbxFont_SelectedIndexChanged(object sender, EventArgs e)
        {
            int size;
            size = Convert.ToInt32(cbxFont.SelectedItem);
            txtChat.Font = new Font("나눔스퀘어", size);
        }

        private void button2_Click(object sender, EventArgs e)
        {
            QueueData d;
            string msg;
            for(int i = 0; i <1000; i++)
            {
                msg = i.ToString();
                d = new QueueData(ChatCode.Msg, ui.Nick, null, msg);
                service.Send(d);
            }
        }

        private void MainForm_Load(object sender, EventArgs e)
        {

        }
    }
}