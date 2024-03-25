using System;
using System.Collections;
using System.Data;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace ProjectServer
{
	internal class Program
	{
		public static readonly string Time = DateTime.Now.ToString("s").Replace(':', '-');
		public static readonly string IP = "127.0.0.1";
		public static readonly IPAddress Address = IPAddress.Parse(IP);
		public static readonly int Port = 10086;
		public static int Counter = 0;

		public static ServerSocket serverSocket = new();

		public class Client
		{
			// 统一管理客户端
			public Client(Socket ClientSocket, Thread ClientThread)
			{
				this.ClientSocket = ClientSocket;
				this.ClientThread = ClientThread;
			}
			public Socket ClientSocket { get; set; }
			public Thread ClientThread { get; set; }
		}

		public class ServerSocket
		{
			// 定义一个委托，用来将接收数据传出去
			public delegate void SendMsg(string ReceiveMsg);
			public event SendMsg HaveMsg;

			// 存储客户端 Socket
			private readonly Dictionary<string, Client> DicSocket = new();

			// 服务端监听
			public bool Listen(IPAddress ip, int port, int MaxClientNum)
			{
				try
				{
					Socket socketwatch = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
					IPEndPoint endPoint = new(ip, port);
					socketwatch.Bind(endPoint);
					socketwatch.Listen(MaxClientNum);
					HaveMsg.Invoke($"[SERVER]-开始监听于{IP}:{Port}。");
					new Thread(new ThreadStart(() =>
					{
						while (true)
						{
							// 如果有客户端连接，就会返回这个客户端的 Socket 对象
							Socket ClientSocket = socketwatch.Accept();
							HaveMsg.Invoke($"[SERVER]-客户端{ClientSocket.RemoteEndPoint}已接入。");
							Counter++;
							Thread ClientThread = new(new ParameterizedThreadStart(RecMsg))
							{
								// 设置为后台线程
								IsBackground = true
							};
							// 把获取到的客户端 Socket 作为参数传到线程中
							ClientThread.Start(ClientSocket);
							// 把当前客户端的资源放到字典中管理
							DicSocket.Add(ClientSocket.RemoteEndPoint.ToString(), new Client(ClientSocket, ClientThread));
						}
					})).Start();
					return true;
				}
				catch
				{
					return false;
				}
			}
			// 向客户端发消息
			public void Send(List<string> SendTo, string str)
			{
				try
				{
					byte[] arrsendmsg = Encoding.UTF8.GetBytes(str);
					foreach (string client in SendTo)
					{
						if (DicSocket[client].ClientSocket.Send(arrsendmsg) != arrsendmsg.Length)
							HaveMsg.Invoke("[SERVER]-提示信息：向" + client + "发送失败！");
					}
				}
				catch (Exception ex)
				{
					HaveMsg.Invoke("[SERVER]-警告：发送遇到错误," + ex.Message);
				}
			}

			public void RecMsg(object ClientSocket)
			{
				// 给线程传参的一种方式，不可以直接传别的类型
				Socket ThisClientSocket = ClientSocket as Socket;
				while (true)
				{
					try
					{
						byte[] arrserverrecmsg = new byte[1024];
						int length = ThisClientSocket.Receive(arrserverrecmsg);
						if (length <= 0)
							continue;
						string ReceiveStr = Encoding.UTF8.GetString(arrserverrecmsg, 0, length);
						// 将收到的字符串通过委托事件传给窗体，用于窗体显示
						HaveMsg.Invoke(ThisClientSocket.RemoteEndPoint.ToString() + "：" + ReceiveStr);
						// 直接转发给所有客户端
						Send(DicSocket.Keys.ToList(), ThisClientSocket.RemoteEndPoint.ToString() + "：" + ReceiveStr);
					}
					catch
					{
						HaveMsg.Invoke("[SERVER]-提示信息：" + ThisClientSocket.RemoteEndPoint.ToString() + "的连接已断开！");
						// 断开后将字典中维护的客户端移除掉
						DicSocket.Remove(ThisClientSocket.RemoteEndPoint.ToString());
						return;
					}
					Thread.Sleep(200);
				}
			}

			public List<string> GetClients()
			{
				return DicSocket.Keys.ToList();
			}

			public void Close(List<string> CloseWho)
			{
				foreach (string client in CloseWho)
				{
					if (DicSocket[client].ClientThread.ThreadState == ThreadState.Running)
						DicSocket[client].ClientThread.Abort();
					// 如果接收线程正在执行，那么先关闭线程，然后关闭 Socket
					DicSocket[client].ClientSocket.Close();
				}
			}
		}
		
		static void Main()
		{
			serverSocket.HaveMsg += new ServerSocket.SendMsg(PrintDelegate);
			if (!serverSocket.Listen(Address, Port, 4))
			{
				Console.WriteLine("监听失败。即将重启。");
				Application.Restart();
			}
			while (Counter == 4) ;
			Console.WriteLine("4 OK.");

			Console.ReadKey();
		}

		// 消息来到时进行的操作。
		private static void PrintDelegate(string msg)
		{
			string[] msgs = msg.Split('-');
			Console.WriteLine(msgs[0] switch
			{
				"[SERVER]" => $"SERVER: {msgs[1]}",
				"[CLIENT]" => $"CLIENT: {msgs[1]}",
				_ => "msg"
			});
			if (msgs[0] == "[CLIENT]")
			{

			}
		}
	}
}