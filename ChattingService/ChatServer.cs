using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Forms;

namespace ChattingService
{
    //서버 클래스
	public class ChattingServer : IChat
	{
		private TcpListener tcpl;
		private ClientManager manager;
		private Thread t;
		private event dQueueData SendServerMsg, SendToUI;

        // 서버 클래스의 생성자로 IChatServerUI를 상속한다 (IChatServerUI는 ChatUI.cs에 정의되어있다.)
        // IChatServerUI에서 받아온 포트를 이용하여 서버를 생성하게 된다. 아이피는 로컬 아이피를 이용한다.
		public ChattingServer(IChatServerUI ui)
		{	
			tcpl = new TcpListener(IPAddress.Any, ui.Port);
			manager = new ClientManager(WriteServerChat);
			SendServerMsg += new dQueueData(manager.SendToAll); //서버에서 유저들에게 메세지를 보내기위한 delegate
			SendToUI += new dQueueData(ui.GetMsg); //ui에 메세지를 기입하기 위한 delegate
			t = new Thread(Listen);
			
			IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());
			string msg = "> " + host.AddressList[0] + ":" + ui.Port;
			SendToUI(new QueueData(ChatCode.StartInfo, msg));
		}
		
		public void Start()
		{
			t.Start();			
		}

        //서버 Stop()함수
		public void Stop()
        {
            if (tcpl != null) tcpl.Stop();
            t.Abort();
			if( manager != null ) manager.Close();
		}

        //서버의 시작과 소켓 연결을 위한 Listen함수
		private void Listen()
		{
			try
			{
				tcpl.Start();
				while( true )
				{
					Socket s = tcpl.AcceptSocket();
					manager.Add(s);
				}	
			}
			catch(SocketException e)
			{
				SendToUI(new QueueData(e.Message));
				SendToUI(new QueueData("서버를 시작할 수 없습니다. 다른 포트를 입력하세요"));
				if( tcpl != null ) tcpl.Stop();
			}
		}

        //서버의 Send버튼
		public void Send(QueueData d)
		{
            //Kick의 경우 서버가 클라이언트에게 직접 클라이언트를 종료하도록 메세지를 보낸다.
            if (d.code == ChatCode.Kick) 
            {
                WriteServerChat(d);
                manager.kick(d);
                SendServerMsg(d);
            }
            else if(d.code == ChatCode.Whisper)
            {
                d.msgFrom = "[서버]";
                WriteServerChat(d);
                manager.Whisper(d);
                
            }
            //그 외의 경우에는 일반 서버 메세지이다.
            else
            {
                d.code = ChatCode.Sys;
                WriteServerChat(d);
                SendServerMsg(d);
            }
		}

        //서버가 자기 자신의 ui에 보낸 메세지를 적기위한 함수
		public void WriteServerChat(QueueData d)
		{
			SendToUI(d);
		}
	}


    //서버에서 클라이언트에 관한 정보들을 관리한다.
	class ClientManager
	{
		private Dictionary<string, Client> list; //
		private Dictionary<string, string> nlist;
        private Dictionary<string, int> userNotify;
        private MessageQueue que;
		private event dQueueData SendToServer;
		
		public ClientManager(dQueueData sendToServer)
		{
			list = new Dictionary<string, Client>();
			nlist = new Dictionary<string,string>();
            userNotify = new Dictionary<string, int>();
            que = new MessageQueue();
			this.SendToServer = sendToServer;
            
		}

        //클라이언트를 딕셔너리에 저장하고, UserList에 추가하라는 메세지를 보낸다.
		public void Add(Socket s)
		{
			Client c = new Client(s, que, Send);
			c.Send(new QueueData(ChatCode.UserList, nlist));
			list.Add(c.MyIP, c); //MYIP는 클라이언트의 아이피이다.		
			c.Start();		
		}

        //서버가 종료될 경우 모든 클라이언트에게 메세지를 보낸 후 종료한다.
		public void Close()
		{
			SendToAll(new QueueData("서버가 종료되었습니다"));
			foreach( Client c in list.Values )
				c.Stop();
			list.Clear();
			nlist.Clear();
		}

        
        //클라이언트 전체에 전송
		public void SendToAll(QueueData d)
		{
			foreach( Client c in list.Values )
				c.Send(d);
		}

        //클라이언트 전체에 전송 하지만 자기 자신을 제외함.
		public void SendToAll(QueueData d, Client except)
		{
			foreach( Client c in list.Values )
				if( c != except ) c.Send(d);
		}

        //한명에게만 Send
		public void SendToOne(QueueData d, Client c)
		{
			c.Send(d);
		}

		public bool NickCheck(string tryReg)
		{
			return nlist.ContainsKey(tryReg);
		}

        public void kick(QueueData d)
        {
            string ip; //유저의 닉네임을 이용하여 그 닉네임을 가진 클라이언트의 ip를 얻기 위한 변수
            nlist.TryGetValue(d.msgFrom, out ip); //유저 닉네임(Key)을 이용하여 ip(Values)를 얻음
            Client c = list[ip]; //c = 해당 유저
            SendToOne(new QueueData(ChatCode.Kick, d.msgFrom, null, null), c); //해당 유저에게 ChatCode.Kick을 보내 직접 어플리케이션을 종료하도록 메세지를 보낸다.
            //메세지의 클라이언트 정보 삭제
            list.Remove(ip);
            nlist.Remove(d.msgFrom);
            //서버측에서 클라이언트와의 쓰레드를 종료
            c.Stop();
        }

        public void Whisper(QueueData d)
        {
            SendToOne(d, list[nlist[d.msgTo]]);
        }

        private void Send(string IPFrom)
		{
			QueueData d = que.Dequeue();
			
			switch(d.code)
			{
                //클라이언트가 ChatCode.Login 코드를 보냈을 경우 닉네임의 중복 여부를 검사하고, 닉네임을 바꾼다.
				case ChatCode.LogIn:
					int a = 0;
					while( NickCheck(d.msgFrom) || d.msgFrom =="")
					{
						d.msgFrom = "손님" + (list.Count + a);
						a++;
					}
					if( a > 0 )
					{
						string msg = "이미 등록된 대화명입니다\r\n" +
							"대화명은 자동으로 설정됩니다 : " +
							"[" + d.msgFrom + "]";
						list[IPFrom].Send(new QueueData(msg));
					}
					nlist.Add(d.msgFrom, IPFrom);
					list[IPFrom].Send(new QueueData(ChatCode.StartInfo, d.msgFrom));
                    userNotify.Add(d.msgFrom, 0);
                    
                    SendToAll(d);
					break;

                    //클라이언트가 로그아웃했을 경우 딕셔너리에 있는 데이터를 삭제하고 클라이언트의 쓰레드 종료
				case ChatCode.LogOut:
					Client c = list[IPFrom];
					list.Remove(IPFrom);
					nlist.Remove(d.msgFrom);
                    userNotify.Remove(d.msgFrom);
					SendToServer(d);
					SendToAll(d);
					c.Stop();
					break;

                    //ChatCode.Kick 코드를 받을경우 서버와 전체에 강제퇴장을 알리고 클라이언트과의 연결중인 소켓을 종료
                case ChatCode.Kick:
                    int b = 0;
                    Client s;
                    while (NickCheck(d.msgTo))
                    {
                        b++;
                    }
                    if (b > 0)
                    {
                        string msg = "[" + d.msgTo + "]" + " 님은 강제퇴장 당하셨습니다.";
                        list[IPFrom].Send(new QueueData(msg));
                    }
                    list.TryGetValue(d.msgTo, out s);
                    list.Remove(d.msgTo);
                    nlist.Remove(d.msgFrom);
                    SendToServer(d);
                    SendToAll(d);
                    s.Stop();
                    break;

                    //전체 메세지
				case ChatCode.Msg:
					SendToAll(d, list[IPFrom]);
					break;

                    //귓속말
				case ChatCode.Whisper:
                    Whisper(d);
					break;

                    //전체 쪽지
                case ChatCode.AllMessage:
                    SendToAll(d, list[IPFrom]);
                    break;

                    //건의 사항
                case ChatCode.Inquiry:
                    d.msgTo = "Check";
                    break;
                case ChatCode.Notify:
                    int val;
                    userNotify.TryGetValue(d.msgTo, out val);
                    userNotify[d.msgTo] = val + 1;
                    userNotify.TryGetValue(d.msgTo, out val);
                    if (val >= 5)
                    {
                        d.code = ChatCode.Kick;
                        d.msgFrom = d.msgTo;
                        d.msgTo = null; d.msg = "신고 누적으로 인하여";
                        kick(d);
                        SendToAll(d);
                    }
                    break;

			}

			SendToServer(d);
		}		
	}

    // 서버와 클라이언트의 소켓 연결을 위한 클래스
	class Client : TCPTransfer
	{
		private Thread t;
		private MessageQueue que;
		private event dStringMsg SendToManager;

		public Client(Socket s, MessageQueue que, dStringMsg sendToManager)
		{
			this.que = que;
			this.SendToManager += sendToManager;
			t = new Thread(Receive);
			base.HookSocket(s);
		}

		public void Start()
		{
			t.Start();
		}

		public void Stop()
		{
			t.Abort();
		}

		public override void Send(QueueData d)
		{
			base.Send(d);
		}

		private new void Receive()
		{
			while( base.MySocket.Connected )
			{
				QueueData d = base.Receive();
                
                if ( d.code != ChatCode.LogIn ) base.Send(d);
                
                que.Enqueue(d);
				SendToManager(MyIP);
			}	
		}

		public string MyIP
		{
            get {	return (IPEndPoint)base.MySocket.RemoteEndPoint + "";	}	
            
        }
	}
}
