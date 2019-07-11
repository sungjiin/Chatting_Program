using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Net;
using System.Threading;

namespace ChattingService
{
	public interface IChatUI
	{
		Label StartInfo { get; }
		TextBox ChatBox { get; }
		ComboBox WhisperList { get; }
		ListBox ClientList { get; }
		void GetMsg(QueueData d);
	}

    

	public interface IChatServerUI : IChatUI
	{
		int Port { get; }
	}

	public interface IChatClientUI : IChatUI
	{		
		IPEndPoint Server { get; }
		string Nick { get; }
	}

	public class ChatUI : IChatServerUI, IChatClientUI
	{
		// 크로스 스레드 작업을 위함
		private Form main;

		// IChatUI
		private Label myInfoLabel;
		private TextBox chatBox;
		private ComboBox whisperList;
		private ListBox clientList;
        
		// IChatServerUI
		private MaskedTextBox openPort;

		// IChatClientUI
		private TextBox serverIP;
		private MaskedTextBox serverPort;		
		private TextBox nick;
        
        //메인 폼 생성자
		public ChatUI
			(Form main, Label myInfoLabel, TextBox chatBox, ComboBox whisperList, ListBox clientList)
		{
			this.main = main;
			this.myInfoLabel = myInfoLabel;
			this.chatBox = chatBox;
            
			this.whisperList = whisperList;
			this.clientList = clientList;
		}

        
        //텍스트의 값을 포트번호로 한다.
		public void HookServerUI(MaskedTextBox openPort)
		{
			this.openPort = openPort;
		}

        //클라이언트 탭의 설정
		public void HookClientUI(TextBox serverIP, MaskedTextBox serverPort, TextBox nick)
		{
			this.serverIP = serverIP;
            serverIP.ReadOnly = true;
			this.serverPort = serverPort;
			this.nick = nick;
		}

        //서버가 시작되었을 경우 UI설정
		public void SetServerUI(bool isStart)
		{
			if( isStart ) 
			{
				openPort.ReadOnly = true;
                
			}
			else
			{
				openPort.ReadOnly = false;
				chatBox.Text = "";
				clientList.Items.Clear();
			}
		}

        //클라이언트가 서버에 연결하였을 경우 UI설정
		public void SetClientUI(bool isStart)
		{
			if( isStart )
			{
				serverIP.ReadOnly = true;
				serverPort.ReadOnly = true;
				nick.ReadOnly = true;
				whisperList.SelectedIndex = 0;
			}
			else
			{
				serverIP.ReadOnly = false;
				serverPort.ReadOnly = false;
				nick.ReadOnly = false;
				chatBox.Clear();
				clientList.Items.Clear();
            }
		}
        
        //ChatCode를 기준으로 각각의 어플리케이션들이 텍스트박스에 받은 메세지들을 적는 기능을 한다.
		public void GetMsg(QueueData d)
		{
			dQueueData o = null;

			switch( d.code )
			{
				case ChatCode.Sys: //시스템 메세지
					o = new dQueueData(WriteSystemMsg);	break;
				case ChatCode.StartInfo: //서버가 시작하거나, 클라이언트가 연결할 경우의 작업
					o = new dQueueData(WriteStartInfo); break;
				case ChatCode.UserList: //유저가 로그인하거나 로그아웃하였을 경우 유저리스트의 변동을 나타냄
					o = new dQueueData(WriteUserList);	break;
				case ChatCode.LogIn: //유저가 로그인하였을 경우 로그인 로그를 받아 적는다.
					o = new dQueueData(WriteLogIn);		break;
				case ChatCode.LogOut: //유저가 로그아웃하였을 경우 로그아웃 로그를 받아 적는다.
					o = new dQueueData(WriteLogOut);	break;
				case ChatCode.Msg: //서버나 유저가 메세지를 받아 적는다.
					o = new dQueueData(WriteChat);		break;			
				case ChatCode.Whisper: //귓속말이 왔을 경우 텍스트박스에 메세지를 받아 적는다.
					o = new dQueueData(WriteWhisper);	break;
                case ChatCode.AllMessage: //전체메세지가 왔을경우 메세지박스를 띄운다.
                    o = new dQueueData(WriteAllMessage); break;
                case ChatCode.Kick: //강제퇴장 당했을 경우 메세지 박스를 띄운다.
                    o = new dQueueData(WriteKick); break;
                case ChatCode.Inquiry: //건의사항을 했을 경우의 기능
                    o = new dQueueData(WriteInquiry); break;
                case ChatCode.Notify: // 신고기능
                    o = new dQueueData(WriteNotify); break;
            }

			if( o != null ) main.Invoke(o, d);
		}

        private void WriteNotify(QueueData d)
        {
            chatBox.AppendText(d.msgTo + "님이 신고당했습니다.\r\n");
        }


        //ChatCode.Sys
        public void WriteSystemMsg(QueueData d)
		{
			chatBox.AppendText("[서버] " + d.msg + "\r\n");
		}

        //Chatcode.StartInfo
		public void WriteStartInfo(QueueData d)
		{
            myInfoLabel.Text = "연결 성공!";
			nick.Text = d.msgFrom;
		}

        //ChatCode.UserList
		public void WriteUserList(QueueData d)
		{
			foreach( string who in ((Dictionary<string, string>)d.msg).Keys )
			{
				clientList.Items.Add(who);
			}
		}

        //ChatCode.Login
		public void WriteLogIn(QueueData d)
		{
			clientList.Items.Add(d.msgFrom);
			chatBox.AppendText("> " + d.msgFrom + " 님이 입장하셨습니다\r\n");
		}

        //ChatCode.LogOut
		public void WriteLogOut(QueueData d)
		{
			clientList.Items.Remove(d.msgFrom);
			chatBox.AppendText("> " + d.msgFrom + " 님이 퇴장하셨습니다\r\n");
		}

        //ChatCode.Kick
        public void WriteKick(QueueData d)
        {
            clientList.Items.Remove(d.msgFrom);
            chatBox.AppendText("> " + d.msgFrom + " 님이"+" "+d.msg +"강제 퇴장 당하셨습니다\r\n");
            if(Nick == d.msgFrom)
            {
                //SetClientUI(false);
                //chatBox.AppendText("[서버] " + Nick + "님은 강제 퇴장 당하였습니다.\r\n");
                using(new CenterWinDialog(main))
                {
                    MessageBox.Show("[서버] "+ Nick + "님은 강제 퇴장 당하였습니다.\r\n");
                    Environment.Exit(0);
                }
            }
        }

        //ChatCode.Msg
        public void WriteChat(QueueData d)
		{
			chatBox.AppendText("[" + d.msgFrom + "] " + d.msg + "\r\n");
		}

        //ChatCode.Whisper
		public void WriteWhisper(QueueData d)
		{
			chatBox.AppendText("(귓말) [" + d.msgFrom + "] "+"->"+" ["+d.msgTo+"] " + d.msg + "\r\n");
		}
        
        //ChatCode.AllMessage
        public void WriteAllMessage(QueueData d)
        {
            using (new CenterWinDialog(main))
            {
                MessageBox.Show("보낸사람 : " + d.msgFrom + "\n(전체 쪽지)[" + d.msg + "]\r\n");
            }
        }

        //ChatCode.Inquiry
        private void WriteInquiry(QueueData d)
        {
            if (d.msgTo == null)
            {
                using (new CenterWinDialog(main))
                {
                    MessageBox.Show("서버 운영자에게 성공적으로 건의사항을 보냈습니다.");

                }
            }
            else
            {
                using (new CenterWinDialog(main))
                {
                    MessageBox.Show(">> " + d.msgFrom + " 님이 건의사항을 보냈습니다.\r\n내용 : " + d.msg);
                }
            }
        }


        // IChatUI 구현	  
        public Label StartInfo
		{	get { return myInfoLabel; }		}
		public TextBox ChatBox
		{	get { return chatBox; }			}
        
		public ComboBox WhisperList
		{	get { return whisperList; }		}
        
		public ListBox ClientList
		{	get { return ClientList; }		}

		// IChatServerUI 구현
		public int Port
		{
			get
			{
				return int.Parse(openPort.Text);
			}
		}

		// IChatClientUI 구현
		public IPEndPoint Server
		{
			get
			{
				return new IPEndPoint
					(IPAddress.Parse(serverIP.Text), int.Parse(serverPort.Text));
			}
		}
		public string Nick
		{
			get
			{
				return nick.Text;
			}
		}
	}
   
    
}
