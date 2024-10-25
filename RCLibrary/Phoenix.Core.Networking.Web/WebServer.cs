using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Phoenix.Core.Networking.Web;

public abstract class WebServer
{
	private TcpListener mylistener;

	private int port = 25580;

	private Thread mthread;

	private string webDir;

	public WebServer(int bindport)
	{
		mylistener = new TcpListener(IPAddress.Loopback, bindport);
	}

	public WebServer(IPAddress bindip, int bindport)
	{
		mylistener = new TcpListener(bindip, bindport);
	}

	public void Start()
	{
		if (mylistener == null)
		{
			throw new Exception("Listener cant be null when starting server");
		}
		try
		{
			mylistener.Start();
			DebugSystem.Write("Web Server Started on " + mylistener.LocalEndpoint.ToString());
			Thread src = new Thread(ListenThread);
			src.Init();
		}
		catch (Exception ex)
		{
			DebugSystem.Write(new ExceptionData(ex, ExceptionSeverity.Error, "Start", "C:\\Users\\Rommel JR\\Dropbox\\Wonderland Online Dev Group\\Pserver Core\\CServer\\RCLibrary\\Networking\\Web\\WebServer.cs", 45));
		}
	}

	public void Stop()
	{
		mthread.Kill();
	}

	private void ListenThread()
	{
		DebugSystem.Write("Started Web Server Listening Thread", DebugItemType.Info_Heavy);
		int num = 0;
		string text = "C:\\MyWebServerRoot\\";
		string text2 = "";
		string data = "";
		string text3 = "";
		while (true)
		{
			Socket socket = mylistener.AcceptSocket();
			DebugSystem.Write(DebugItemType.Info_Heavy, "Socket Type " + socket.SocketType);
			if (socket.Connected)
			{
				DebugSystem.Write(DebugItemType.Info_Heavy, $"\nClient Connected by Web Server!!\n==================\nCLient IP {socket.RemoteEndPoint}\n");
				byte[] array = new byte[1024];
				int num2 = socket.Receive(array, array.Length, SocketFlags.None);
				string @string = Encoding.ASCII.GetString(array);
				if (@string.Substring(0, 3) != "GET")
				{
					DebugSystem.Write(DebugItemType.Info_Heavy, "Only Get Method is supported..");
					socket.Close();
				}
				else
				{
					num = @string.IndexOf("HTTP", 1);
					string text4 = @string.Substring(num, 8);
					string text5 = @string.Substring(0, num - 1);
					text5.Replace("\\", "/");
					if (text5.Contains("api?"))
					{
						HandleAPIRequests(socket, text4, text5);
						socket.Close();
					}
					else
					{
						if (text5.IndexOf(".") < 1 && !text5.EndsWith("/"))
						{
							text5 += "/";
						}
						num = text5.LastIndexOf("/") + 1;
						string text6 = text5.Substring(num);
						string text7 = text5.Substring(text5.IndexOf("/"), text5.LastIndexOf("/") - 3);
						string text8 = ((!(text7 == "/")) ? GetLocalPath(text, text7) : text);
						Console.WriteLine("Directory Requested : " + text8);
						if (text8.Length == 0)
						{
							string text9 = "<H2>Error!! Requested Directory does not exists</H2><Br>";
							SendHeader(text4, "", text9.Length, " 404 Not Found", socket);
							SendToBrowser(text9, socket);
							socket.Close();
						}
						else
						{
							if (text6.Length == 0)
							{
								text6 = GetTheDefaultFileName(text8);
								if (text6 == "")
								{
									string text9 = "<H2>Error!! No Default File Name Specified</H2>";
									SendHeader(text4, "", text9.Length, " 404 Not Found", socket);
									SendToBrowser(text9, socket);
									socket.Close();
									goto IL_03c7;
								}
							}
							string mimeType = GetMimeType(text6);
							text2 = text8 + text6;
							DebugSystem.Write(DebugItemType.Info_Heavy, "File Requested : " + text2);
							if (!File.Exists(text2))
							{
								string text9 = "<H2>404 Error! File Does Not Exists...</H2>";
								SendHeader(text4, "", text9.Length, " 404 Not Found", socket);
								SendToBrowser(text9, socket);
								DebugSystem.Write(DebugItemType.Info_Heavy, data);
							}
							else
							{
								int num3 = 0;
								text3 = "";
								FileStream fileStream = new FileStream(text2, FileMode.Open, FileAccess.Read, FileShare.Read);
								BinaryReader binaryReader = new BinaryReader(fileStream);
								byte[] array2 = new byte[fileStream.Length];
								int num4;
								while ((num4 = binaryReader.Read(array2, 0, array2.Length)) != 0)
								{
									text3 += Encoding.ASCII.GetString(array2, 0, num4);
									num3 += num4;
								}
								binaryReader.Close();
								fileStream.Close();
								SendHeader(text4, mimeType, num3, " 200 OK", socket);
								SendToBrowser(array2, socket);
							}
							socket.Close();
						}
					}
				}
			}
			goto IL_03c7;
			IL_03c7:
			bool flag = true;
		}
	}

	public abstract void HandleAPIRequests(Socket src, string httpver, string req);

	public string GetTheDefaultFileName(string sLocalDirectory)
	{
		string text = "";
		try
		{
			StreamReader streamReader = new StreamReader("data\\Default.Dat");
			while ((text = streamReader.ReadLine()) != null && !File.Exists(sLocalDirectory + text))
			{
			}
		}
		catch (Exception ex)
		{
			DebugSystem.Write("An Exception Occurred : " + ex.ToString());
		}
		if (File.Exists(sLocalDirectory + text))
		{
			return text;
		}
		return "";
	}

	public string GetLocalPath(string sMyWebServerRoot, string sDirName)
	{
		string text = "";
		string text2 = "";
		string result = "";
		int num = 0;
		sDirName.Trim();
		sMyWebServerRoot = sMyWebServerRoot.ToLower();
		sDirName = sDirName.ToLower();
		try
		{
			StreamReader streamReader = new StreamReader("data\\VDirs.Dat");
			while ((text = streamReader.ReadLine()) != null)
			{
				text.Trim();
				if (text.Length > 0)
				{
					num = text.IndexOf(";");
					text = text.ToLower();
					text2 = text.Substring(0, num);
					result = text.Substring(num + 1);
					if (text2 == sDirName)
					{
						break;
					}
				}
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine("An Exception Occurred : " + ex.ToString());
		}
		if (text2 == sDirName)
		{
			return result;
		}
		return "";
	}

	public string GetMimeType(string sRequestedFile)
	{
		string text = "";
		string result = "";
		string text2 = "";
		string text3 = "";
		sRequestedFile = sRequestedFile.ToLower();
		int startIndex = sRequestedFile.IndexOf(".");
		text2 = sRequestedFile.Substring(startIndex);
		try
		{
			StreamReader streamReader = new StreamReader("data\\Mime.Dat");
			while ((text = streamReader.ReadLine()) != null)
			{
				text.Trim();
				if (text.Length > 0)
				{
					startIndex = text.IndexOf(";");
					text = text.ToLower();
					text3 = text.Substring(0, startIndex);
					result = text.Substring(startIndex + 1);
					if (text3 == text2)
					{
						break;
					}
				}
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine("An Exception Occurred : " + ex.ToString());
		}
		if (text3 == text2)
		{
			return result;
		}
		return "";
	}

	public void SendToBrowser(string sData, Socket mySocket)
	{
		SendToBrowser(Encoding.ASCII.GetBytes(sData), mySocket);
	}

	public void SendHeader(string sHttpVersion, string sMIMEHeader, int iTotBytes, string sStatusCode, Socket mySocket)
	{
		string text = "";
		if (sMIMEHeader.Length == 0)
		{
			sMIMEHeader = "text/html";
		}
		text = text + sHttpVersion + sStatusCode + "\r\n";
		text += "Server: cx1193719-b\r\n";
		text = text + "Content-Type: " + sMIMEHeader + "\r\n";
		text += "Accept-Ranges: bytes\r\n";
		text = text + "Content-Length: " + iTotBytes + "\r\n\r\n";
		byte[] bytes = Encoding.ASCII.GetBytes(text);
		SendToBrowser(bytes, mySocket);
		Console.WriteLine("Total Bytes : " + iTotBytes);
	}

	public void SendToBrowser(byte[] bSendData, Socket mySocket)
	{
		int num = 0;
		try
		{
			if (mySocket.Connected)
			{
				if ((num = mySocket.Send(bSendData, bSendData.Length, SocketFlags.None)) == -1)
				{
					Console.WriteLine("Socket Error cannot Send Packet");
				}
				else
				{
					Console.WriteLine("No. of bytes send {0}", num);
				}
			}
			else
			{
				Console.WriteLine("Connection Dropped....");
			}
		}
		catch (Exception arg)
		{
			Console.WriteLine("Error Occurred : {0} ", arg);
		}
	}
}
