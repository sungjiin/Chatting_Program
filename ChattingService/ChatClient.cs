using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace ChattingService
{
    //채팅을 하기위한 클라이언트의 클래스
	public class ChattingClient : TCPTransfer, IChat
	{
		private IChatClientUI ui;
		private TcpClient tcpc;
		private Thread t;
		private event dQueueData SendToUI;

		public ChattingClient(IChatClientUI ui)
		{
			this.ui = ui;
			SendToUI += new dQueueData(this.ui.GetMsg);
			tcpc = new TcpClient();
			t = new Thread(Receive);
		}

        //IChat에서 받은 아이피와 포트를 이용하여 현재 열려 있는 서버와의 소켓을 연결한다.
		public void Start()
		{
			try
			{
				tcpc.Connect(ui.Server);
				base.HookSocket(tcpc.Client);
				t.Start();
				base.Send(new QueueData(ChatCode.LogIn, ui.Nick));
			}
			catch
			{
				SendToUI(new QueueData("연결실패\r\n서버정보를 다시 입력하세요"));
			}
		}

        //클라이언트 UI에서 Stop()함수가 발생하였을경우
		public void Stop()
		{
			base.Send(new QueueData(ChatCode.LogOut, ui.Nick));
			t.Abort();
			if( tcpc != null ) tcpc.Close();			
		}
		public override void Send(QueueData d)
		{
			if( d.code == ChatCode.Msg && (string)d.msg == "" ) return;
			base.Send(d);
		}

		public new void Receive()
		{
			while( tcpc.Connected )
				SendToUI(base.Receive());
		}	
	}
}
