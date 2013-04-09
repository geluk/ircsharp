using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Threading;
using System.Threading.Tasks;

using CSNetLib;

namespace IRCSharp
{
	public delegate void ConnectionEstablishedEvent();
	public delegate void DebugLogEvent(string message);
	public delegate void RawLineReceiveEvent(string line);
	public delegate void FormattedLineReceiveEvent(IrcLine line);
	public delegate void MessageReceiveEvent(IrcMessage message);

    public class IrcClient
    {
		public event ConnectionEstablishedEvent OnConnectionEstablished;
		public event DebugLogEvent OnDebugLog;
		public event RawLineReceiveEvent OnRawLineReceived;
		public event FormattedLineReceiveEvent OnFormattedLineReceived;
		public event MessageReceiveEvent OnMessageReceived;
		

		private Dictionary<string, IrcChannel> channels = new Dictionary<string, IrcChannel>();

		private NetClient client;

		public bool Connected
		{
			get;
			private set;
		}

		/// <summary>
		/// Creates a new IRC Client instance, but does not connect to the server yet.
		/// </summary>
		/// <param name="host">The hostname or IP address of the server to connect to</param>
		/// <param name="port">The port of the server</param>
		/// <param name="nick">The IRC nickname to be used by the client</param>
		/// <param name="ident">The ident/login to be used by the client</param>
		public IrcClient()
		{
			
			client = new NetClient();
		}

		/// <summary>
		/// Connects to an IRC server using the specified parameters.
		/// </summary>
		/// <param name="invisible">Determines whether the client should connect with mode +i (invisible to all users who aren't in the same channel)</param>
		public void Connect(string host, int port, string nick, string ident = null, string realName = "IRCSharp", bool invisible = true)
		{
			// If no ident is specified, try to use the nickname as ident
			if (ident == null)
				ident = nick;
			// Do not accept null references
			if (host == null || nick == null || realName == null) {
				throw new ArgumentNullException();
			}

			client.OnNetworkDataAvailabe += OnReceiveData;
			client.Connect(host, port);
			SendRaw("NICK " + nick);
			SendRaw("USER " + ident + " " + (invisible ? 8 : 0) + " * :" + realName);
			while (!Connected) {
				Thread.Sleep(20);
			}
			Log("Connection to " + host + " established.");
			if (OnConnectionEstablished != null) OnConnectionEstablished();
		}

		private void OnReceiveData(string line)
		{
			if(line.StartsWith("PING")){
				Connected = true;
				SendRaw("PONG :" + line.Substring("PING :".Length));
				return;
			}
			IrcLine linef = ParseIrcLine(line);

			if(OnRawLineReceived != null) OnRawLineReceived(line);
			
			if(OnFormattedLineReceived != null) OnFormattedLineReceived(linef);

			ProcessIrcLine(linef);

			
		}

		private void ProcessIrcLine(IrcLine line)
		{
			switch (line.Command) {
				case "PRIVMSG":
					ProcessPm(line);
					break;
				case "NOTICE":
					break;
				default:
					break;
			}
		}
		private void ProcessPm(IrcLine line)
		{
			string nick = line.Sender.Substring(0, line.Sender.IndexOf('!'));
			string ident = line.Sender.Substring(line.Sender.IndexOf('!')+1,line.Sender.IndexOf('@') - line.Sender.IndexOf('!')-1);
			string hostmask = line.Sender.Substring(line.Sender.IndexOf('@')+1);
			IrcUser sender = new IrcUser(nick,ident,hostmask);
			OnMessageReceived(new IrcMessage(sender, line.Arguments[0], line.FinalArgument));
		}


		private IrcLine ParseIrcLine(string line)
		{
			// If the line starts with a colon, it contains information about the sender.
			bool hasSender = line.StartsWith(":");

			// Clean up the line a bit
			if(hasSender)
				line = line.Substring(1);
			if(line.EndsWith("\r"))
				line = line.Substring(0, line.Length - 1);


			// The line without the final argument, which comes after the first colon that isn't at the start of the line.
			string lineWithoutFinalArg;
			// The final argument
			string finalArg;

			if (line.Contains(':')) {
				lineWithoutFinalArg = line.Substring(0, line.IndexOf(':'));
				finalArg = line.Substring(line.IndexOf(':') + 1);
			} else {
				lineWithoutFinalArg = line;
				finalArg = null;
			}

			// Split the line on spaces
			List<String> splitLine = lineWithoutFinalArg.Split(' ').ToList<string>();

			// First item in the list contains the sender. If there is no sender, use null.
			if(!hasSender)
				splitLine.Insert(0, null);

			string sender = (splitLine.Count >= 0 ? splitLine[0] : null);
			string command = (splitLine.Count >= 1 ? splitLine[1] : null);
			
			// Contains all arguments for the IRC command, except for the last argument. Usually contains just one argument.
			string[] args = splitLine.GetRange(2, splitLine.Count - 2).ToArray();

			return new IrcLine(sender, command, args, finalArg);
		}

		/// <summary>
		/// Sends a raw IRC line to the server.
		/// </summary>
		/// <param name="data"></param>
		public void SendRaw(string data)
		{
			client.SendData(data);
		}

		private void Log(string message)
		{
			if (OnDebugLog != null) OnDebugLog(message);
		}

		public void JoinChannel(string channelName)
		{
			SendRaw("JOIN :" + channelName);
			channels.Add(channelName, new IrcChannel(channelName));

		}

		public void LeaveChannel(string channelName, string reason = null)
		{
			if (reason != null)
				SendRaw("PART " + channelName + " :" + reason);
			else
				SendRaw("PART " + channelName);

			channels.Remove(channelName);
		}

		public void LeaveChannel(List<string> channels)
		{
			var partMessage = "";
			foreach (var channel in channels) {
				channels.Remove(channel);
				partMessage += ","+channel;
			}
			SendRaw("PART " + partMessage.Substring(1));
		}

		public void SendMessage(string target, string message)
		{
			SendRaw("PRIVMSG " + target + " :" + message);
		}
	}
}
