using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Threading;
using System.Threading.Tasks;
using CsNetLib2;
using System.Diagnostics;

namespace IRCSharp
{
	public delegate void ConnectionEstablishedEvent();
	public delegate void DebugLogEvent(string message);
	public delegate void RawLineReceiveEvent(string line);
	public delegate void FormattedLineReceiveEvent(IrcLine line);
	public delegate void MessageReceiveEvent(IrcMessage message);
	public delegate void NickChangeEvent(IrcUser user, string newNick);
	public delegate void KickEvent(string user, string channel);
	public delegate void KickedEvent(string channel);
	public delegate void DisconnectedEvent(DisconnectReason reason);
	public delegate void QuitEvent(IrcUser user, string reason);
	public delegate void JoinedChannelEvent(string channel);
	public delegate void PartedChannelEvent(string channel);
	public delegate void JoinChannelEvent(IrcUser user, string channel);
	public delegate void PartChannelEvent(IrcUser user, string channel);
	public delegate void NoticeReceiveEvent(IrcUser user, string notice);

	public enum DisconnectReason
	{
		PingTimeout,
		ServerDisconnect,
		DisconnectOnRequest
	}

	public struct ConnectionInfo
	{
		public string Host;
		public int Port;
		public string Nick;
		public string Ident;
		public string RealName;
		public bool Invisible;
	}

    public class IrcClient
    {
		public event ConnectionEstablishedEvent OnConnectionEstablished; // The client has established a connection with the IRC server
		public event DebugLogEvent OnDebugLog;
		public event DebugLogEvent OnNetLibDebugLog;
		public event RawLineReceiveEvent OnRawLineReceived; // A raw IRC line is received
		public event FormattedLineReceiveEvent OnFormattedLineReceived; // A formatted IRC line has been created
		public event MessageReceiveEvent OnMessageReceived; // A message is received
		public event NickChangeEvent OnNickChanged; // Someone changes their nick
		public event KickEvent OnKick; // Someone is kicked
		public event KickedEvent OnKicked; // The client is kicked
		public event DisconnectedEvent OnDisconnect; // The client disconnects, whether intended or not
		public event QuitEvent OnQuit; // Someone quits from IRC
		public event JoinChannelEvent OnJoinChannel; // Someone joins a channel
		public event PartChannelEvent OnPartChannel; // Someone parts a channel
		public event JoinedChannelEvent OnJoinedChannel; // The client joins a channel
		public event PartedChannelEvent OnPartedChannel; // The client parts a channel
		public event NoticeReceiveEvent OnNoticeReceived; // Client receives a notice from the IRC server
		public event LocalPortKnownEvent OnLocalPortKnown
		{
			add
			{
				client.OnLocalPortKnown += value;
			}
			remove
			{
				client.OnLocalPortKnown -= value;
			}
		}
		// Maps channel names to IrcChannels
		private Dictionary<string, IrcChannel> channels = new Dictionary<string, IrcChannel>();
		public const string Version = "1.21";
		private const int pingTimeout = 200;

		public bool ReplyToPings { get; set; }

		public int ChannelCount
		{
			get {
				return channels.Count;
			}
		}
		public int TotalUserCount
		{
			get
			{
				int count = 0;
				foreach (var pair in channels) {
					count += pair.Value.UserCount;
				}
				return count;
			}
		}

		private NetLibClient client;

		private bool QuitRequested = false;
		public bool Connected{ get; private set; }

		private string host, nick, ident, realName;
		private int port;
		private bool invisible;
		private DateTime lastPing;
		private Timer timeoutCheck;

		public IrcClient()
		{
			ReplyToPings = true;
			Logger.ClearLog();
			CreateClient();
			timeoutCheck = new Timer(OnTimeoutCheck, null, 5000, 5000);
		}

		private void OnTimeoutCheck(object state)
		{
			if (Connected && (DateTime.Now - lastPing).TotalSeconds > pingTimeout) {
				if (OnDisconnect != null) {
					GeneratePingTimeout();
				}
			}
		}

		public void GeneratePingTimeout()
		{
			var t = System.Threading.Thread.CurrentThread;
			// Once we disconnect the NetLib client, there will be no alive foreground threads left.
			// For this reason, we promote this thread to a foreground thread, so that the application will not be closed
			// while this thread is reconnecting to the IRC server.
			// Once the connection is opened, a new NetLib client listening thread (which is also a foreground thread)
			// will be started, and this thread, having fulfilled its objective, will die.
			t.IsBackground = false;
			//t.Name = "Bot restart thread";
			client.DisconnectWithoutEvent();
			Connected = false;
			OnDisconnect(DisconnectReason.PingTimeout);
		}

		private void CreateClient()
		{
			client = new NetLibClient(TransferProtocols.Delimited, Encoding.UTF8);
			client.OnDataAvailable += OnReceiveData;
			client.OnLogEvent += (message) =>
			{
				if (OnNetLibDebugLog != null) {
					OnNetLibDebugLog(message);
				}
			};
			client.OnDisconnect += () =>
			{
				Connected = false;
				client.DisconnectWithoutEvent();
				if (OnDisconnect != null) {
					if (QuitRequested)
						OnDisconnect(DisconnectReason.DisconnectOnRequest);
					else
						OnDisconnect(DisconnectReason.ServerDisconnect);
				}
			};
		}

		/*public System.Net.Sockets.Socket GetSocket()
		{
			return client.GetSocket();
		}*/

		public void Connect(string host, int port, string nick, string ident = null, string realName = "IRCSharp", bool invisible = true)
		{
			Connect(
				new ConnectionInfo()
					{
						Host = host,
						Port = port,
						Nick = nick,
						Ident = ident,
						RealName = realName,
						Invisible = invisible
					}
			);
		}

		/// <summary>s
		/// Connects to an IRC server using the specified parameters.
		/// </summary>
		/// <param name="invisible">Determines whether the client should connect with mode +i (invisible to all users who aren't in the same channel)</param>
		public void Connect(ConnectionInfo ci)
		{
			Log("Starting IRCSharp version " + Version);
			Log("Using CsNetLib version " + NetLib.Version);



			// If no ident is specified, try to use the nickname as ident
			if (ci.Ident == null)
				ci.Ident = nick;
			if (ci.Host == null || ci.Nick == null || ci.RealName == null) {
				throw new ArgumentNullException();
			}
			this.host = ci.Host;
			this.port = ci.Port;
			this.nick = ci.Nick;
			this.ident = ci.Ident;
			this.realName = ci.RealName;
			this.invisible = ci.Invisible;
			InnerConnect();
		}
		private void InnerConnect()
		{
			Log("Connecting to the IRC server");
			try {
				client.Connect(host, port);
			} catch (System.Net.Sockets.SocketException e) {
				throw e;
			}
			SendRaw("NICK " + nick);
			SendRaw("USER " + ident + " " + (invisible ? 8 : 0) + " * :" + realName);
			Log("Credentials sent");
			while (!Connected) {
				Thread.Sleep(20);
			}
			Log("Connection to " + host + " established.");

			if (OnConnectionEstablished != null) OnConnectionEstablished();
		}

		public bool InChannel(string channel)
		{
			return channels.ContainsKey(channel);
		}
		public void ChangeNick(string nick)
		{
			SendRaw("NICK :" + nick);
			this.nick = nick;
		}
		public void Part(string channel, string reason = null)
		{
			if (reason == null) {
				SendRaw("PART " + channel);
			} else {
				SendRaw(String.Format("PART {0} :{1}", channel, reason));
			}
			channels.Remove(channel);
		}

		public void Quit(string reason = null)
		{
			if (reason == null) {
				SendRaw("QUIT");
			} else {
				string line = "QUIT :" + reason;
				SendRaw(line);
			}
			QuitRequested = true;
			while (Connected) {
				Thread.Sleep(1);
			}
		}

		/*private void SendRawLine(IrcLine line)
		{
			OnRawLineReceived(line);
		}*/

		private void StartRawLineSend(string line) 
		{
			OnRawLineReceived(line);
		}
		private void StartIrcLineSend(IrcLine line)
		{
			OnFormattedLineReceived(line);
		}
		private void StartPrivMsgSend(IrcMessage msg)
		{
			OnMessageReceived(msg);
		}
		private void OnReceiveData(string line, long sender)
		{
			line = line.TrimEnd('\r', '\n');

			Logger.Log(line, LogLevel.In);

			/*while((byte)line[0] < 32){
				Log(string.Format("Removed leading character {0:X2}", (byte)line[0]));
				line = line.Substring(1);
			}*/

			/*Func<int> lastIndex = () => line.Length -1;

			byte b = (byte)line[lastIndex()];
			while (b < 32 && b != 1) {
				Log(string.Format("Removed trailing character {0:X2} in line \"{1}\"", (byte)line[lastIndex()], line));
				line = line.Substring(0, lastIndex());
				b = (byte)line[lastIndex()];
			}*/

			if (line.StartsWith("PING")) {
				lastPing = DateTime.Now;
				Connected = true;
				if (ReplyToPings) {
					string response = "PONG :" + line.Substring("PING :".Length);
					SendRaw(response);
				}
				return;
			}
			IrcLine linef = ParseIrcLine(line);

			if (OnRawLineReceived != null) {
				var t = new Thread(() => StartRawLineSend(line));
				t.Start();
				
			}

			ProcessIrcLine(linef);
		}
		private void ProcessIrcLine(IrcLine line)
		{
			switch (line.Command) {
				case "PRIVMSG":

					var chars = line.FinalArgument.ToCharArray();

					ProcessPm(line);
					break;
				case "NOTICE":
					ProcessNotice(line);
					break;
				case "NICK":
					ProcessNickChange(line);
					break;
				case "KICK":
					ProcessKick(line);
					break;
				case "JOIN":
					ProcessJoin(line);
					break;
				case "353":
					ProcessNameReply(line);
					break;
				case "PART":
					ProcessPart(line);
					break;
				case "372":
					// Ignore topic page
					break;
				case "QUIT":
					ProcessQuit(line);
					break;
				default:
					if (OnFormattedLineReceived != null) {
						var t = new Thread(() => StartIrcLineSend(line));
						t.Start();
					}
					break;
			}
		}

		private void ProcessQuit(IrcLine line)
		{
			if (OnQuit != null) {
				OnQuit(GetUserFromSender(line.Sender), line.FinalArgument);
			}
		}
		private void ProcessNotice(IrcLine line)
		{
			if (OnNoticeReceived != null) {
				OnNoticeReceived(GetUserFromSender(line.Sender), line.FinalArgument);
			}
		}
		private void ProcessNameReply(IrcLine line)
		{
			string channelName = line.Arguments[2];
			IrcChannel channel;
			try {
				channel = channels[channelName];
			} catch (KeyNotFoundException) {
				Log("Unable to process name reply: channel not found");
				return;
			}
			string[] users = line.FinalArgument.Split(' ');
			foreach (string user in users) {
				if (user.StartsWith("@")) {
					channel.AddUser(user.Substring(1), PermissionLevel.Operator);
				} else if (user.StartsWith("+")) {
					channel.AddUser(user.Substring(1), PermissionLevel.Voiced);
				} else {
					channel.AddUser(user, PermissionLevel.Default);
				}
			}
		}
		private void ProcessPart(IrcLine line)
		{
			IrcUser sender = GetUserFromSender(line.Sender);
			if (sender.Nick == nick) {
				channels.Remove(line.Arguments[0]);
				if (OnPartedChannel != null) OnPartedChannel(line.Arguments[0]);
			} else {
				channels[line.Arguments[0]].RemoveUser(sender.Nick);
				if (OnPartChannel != null) OnPartChannel(sender, line.Arguments[0]);
			}
		}
		private void ProcessJoin(IrcLine line)
		{
			IrcUser sender = GetUserFromSender(line.Sender);
			if (sender.Nick == nick && !channels.ContainsKey(line.Arguments[0])) {
				channels.Add(line.Arguments[0], new IrcChannel(line.Arguments[0]));
				if (OnJoinedChannel != null) OnJoinedChannel(line.Arguments[0]);
			} else {
				channels[line.Arguments[0]].AddUser(sender.Nick, PermissionLevel.Default);
				if (OnJoinChannel != null) OnJoinChannel(sender, line.Arguments[0]);
			}
		}
		private void ProcessKick(IrcLine line)
		{
			if (line.Arguments[1].Equals(nick)) {
				channels.Remove(line.Arguments[0]);
				if (OnKicked != null) {
					OnKicked(line.Arguments[0]);
				}
			} else if (OnKick != null) {
				OnKick(line.Arguments[1], line.Arguments[0]);
			}
		}
		public IrcChannel[] GetChannels()
		{
			return channels.Values.ToArray();
		}
		public IrcUser GetUserFromSender(string sender)
		{
			if (!sender.Contains("!")) {
				string nick = String.Empty;
				string ident = String.Empty;
				string hostmask = sender;
				return new IrcUser(nick, ident, hostmask);
			} else {
				string nick = sender.Substring(0, sender.IndexOf('!'));
				string ident = sender.Substring(sender.IndexOf('!') + 1, sender.IndexOf('@') - sender.IndexOf('!') - 1);
				string hostmask = sender.Substring(sender.IndexOf('@') + 1);
				return new IrcUser(nick, ident, hostmask);
			}
		}
		private void ProcessNickChange(IrcLine line)
		{
			if (OnNickChanged != null) OnNickChanged(GetUserFromSender(line.Sender), line.FinalArgument);
		}
		private void ProcessPm(IrcLine line)
		{
			string actionSequence ="\u0001ACTION";

			bool action = false;

			if (line.FinalArgument.StartsWith(actionSequence)) {
				action = true;
				string message = line.FinalArgument;
				message = message.Substring(8, message.Length - 9);
				line.FinalArgument = message;
				//Console.WriteLine(message);
			}

			if (OnMessageReceived != null) {
				IrcUser sender = GetUserFromSender(line.Sender);
				string channel;
				if (line.Arguments[0] == nick) {
					channel = sender.Nick;
				} else {
					channel = line.Arguments[0];
				}
				IrcMessage msg = new IrcMessage(sender, channel, line.FinalArgument, action);
				var t = new Thread(() => StartPrivMsgSend(msg));
				t.Start();

			}
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

			string sender = null;
			if (hasSender) {
				sender = line.Substring(0, line.IndexOf(' '));
				line = line.Substring(sender.Length + 1);
			}


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

			string command = (splitLine.Count >= 1 ? splitLine[0] : null);
			
			// Contains all arguments for the IRC command, except for the last argument. Usually contains just one argument.
			string[] args = splitLine.GetRange(1, splitLine.Count - 1).ToArray();

			return new IrcLine(sender, command, args, finalArg);
		}

		/// <summary>
		/// Sends a raw IRC line to the server.
		/// </summary>
		/// <param name="data"></param>
		public bool SendRaw(string data)
		{
			Logger.Log(data, LogLevel.Out);
			var result = client.Send(data, 0);
			if(result){
				// When the client sends a message to the IRC server, the IRC server will, in addition to processing this message,
				// also regard this as an indication that the client is still alive. Therefore, if it was about to send a ping to the client,
				// it will not do so, since the client has already indicated that it is alive.
				// For this reason, we must inform the client that (provided that the message is successfully sent) it is still connected to the 
				// IRC server. If this value is not set, the client will disconnect itself automatically because it isn't receiving any pings within
				// the specified ping timeout.
				lastPing = DateTime.Now;
			}
			return result;
		}

		private void Log(string message)
		{
			if (OnDebugLog != null) OnDebugLog(message);
		}
		/// <summary>
		/// Join a channel.
		/// </summary>
		/// <param name="channelName">The channel to join.</param>
		public void JoinChannel(string channelName, bool validate = true)
		{
			channelName = channelName.ToLower();

			SendRaw("JOIN :" + channelName);
			if (validate) {

				int sleepTime = 50;
				int totalSleepTime = 0;
				while (!channels.ContainsKey(channelName)) {
					if (totalSleepTime > 1000) {
						Log(string.Format("Attempt to join {0} failed, retrying.", channelName));
						JoinChannel(channelName, true);
						return;
					}
					Thread.Sleep(sleepTime);
					totalSleepTime += sleepTime;
				}
			}
		}

		/// <summary>
		/// Leave a channel.
		/// </summary>
		/// <param name="channelName">The channel to leave.</param>
		/// <param name="reason">The reason for leaving.</param>
		public void LeaveChannel(string channelName, string reason = null)
		{
			if (reason != null)
				SendRaw("PART " + channelName + " :" + reason);
			else
				SendRaw("PART " + channelName);

			channels.Remove(channelName);
		}
		/// <summary>
		/// Leave multiple channels.
		/// </summary>
		/// <param name="channels">The channels to leave.</param>
		public void LeaveChannel(List<string> channels)
		{
			var partMessage = "";
			foreach (var channel in channels) {
				channels.Remove(channel);
				partMessage += ","+channel;
			}
			SendRaw("PART " + partMessage.Substring(1));
		}
		/// <summary>
		/// Sends a message to the target.
		/// </summary>
		/// <param name="target">The channel or user to receive the message.</param>
		/// <param name="message">The message to be sent.</param>
		public bool SendMessage(string target, string message)
		{
			if (Connected) {
				return SendRaw("PRIVMSG " + target + " :" + message);
			}
			throw new InvalidOperationException("Attempt to send a message while the client is not connected to a server");
		}

		public object GetConnectionInfo()
		{
			return new ConnectionInfo()
			{
				Host = host,
				Port = port,
				Nick = nick,
				Ident = ident,
				RealName = realName,
				Invisible = invisible
			};
		}

		public void Disconnect()
		{
			client.Disconnect();
		}
	}
}
