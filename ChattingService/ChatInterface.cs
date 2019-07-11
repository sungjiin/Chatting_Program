using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Threading;
using System.Runtime.Serialization.Formatters.Binary;

namespace ChattingService
{
	public delegate void dStringMsg(string msgFrom);
	public delegate void dQueueData(QueueData d);

	public enum ChatCode
	{
		Sys, StartInfo, UserList, LogIn, LogOut,
		Msg, Whisper, Nick, AllMessage, Kick, Inquiry, Notify
	}


    //Serializable을 이용하여 데이터를 보낼 수 있도록 준비하는 클래스
	[Serializable]
	public class QueueData
	{
		public ChatCode code;
		public string msgFrom;
		public string msgTo;
		public object msg;

		public QueueData(object serverMsg)
		{
			this.code = ChatCode.Sys;
			this.msg = serverMsg;
		}
        

		public QueueData(ChatCode code, string msgFrom)
		{
			this.code = code;
			this.msgFrom = msgFrom;
		}


        public QueueData(ChatCode code, object msg)
		{
			this.code = code;
			this.msg = msg;
		}

		public QueueData(ChatCode code, string msgFrom, string msgTo, object msg)
		{
			this.code = code;
			this.msgFrom = msgFrom;
			this.msgTo = msgTo;					
			this.msg = msg;
		}
    }

	public interface IChat
	{
		void Start();
		void Stop();
		void Send(QueueData d);
	}


    //연결한 스트림을 통해 서버와 클라이언트의 통신을 위한 클래스
    //서버와 클라이언트의 Serialize와 Deserialize를 하여 메세지를 Send하는 역활을 한다.
	public class TCPTransfer
	{
		private Socket s;
		private NetworkStream sendStm, recvStm;

		public void HookSocket(Socket s)
		{
			this.s = s;
			sendStm = new NetworkStream(s);
			recvStm = new NetworkStream(s);
		}

		public Socket MySocket
		{	get {	return s;	}	}


        //QueueData로 준비한 데이터를 Serialize하여 서버나 클라이언트에 보내는 오버라이딩 기능
		public virtual void Send(QueueData d)
		{			
			BinaryFormatter bf = new BinaryFormatter();
			
			try
			{							
				bf.Serialize(sendStm, d);
				sendStm.Flush();
			}
			catch
			{
				return;
			}
		}

        //QueueData로 받은 데이터를 서버나 클라이언트가 사용하기 위해 Deserialize하여 받는 오버라이드 기능
		public virtual QueueData Receive()
		{			
			QueueData d = null;
			BinaryFormatter bf = new BinaryFormatter();		
			
			try
			{
				d = (QueueData)bf.Deserialize(recvStm);
				recvStm.Flush();
			}
			catch(Exception e)
			{
				return new QueueData
					(e.Message + "\r\n전송문제로 데이터를 수신받지 못했습니다");
			}

			return d;
		}
	}


    //여러개의 쓰레드 활동간의 순차적인 처리를 위한 클래스
    // 안전한 쓰레드를 위하여 Queue와 lock을 사용하여 순차적으로 처리하게 된다.
	class MessageQueue
	{
		private Queue<QueueData> que;

		public MessageQueue()
		{	que = new Queue<QueueData>();	}

		public void Enqueue(QueueData item)
		{	lock( que ) que.Enqueue(item);	}

		public QueueData Dequeue()
		{	lock( que )	return que.Count != 0 ? que.Dequeue() : null;	}

		public void Clear()
		{	lock( que ) que.Clear();		}
	}
}