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
	public delegate void KickEvent(string user, string channel, string reason, IrcUser kicker);
	public delegate void KickedEvent(string channel, string reason, IrcUser kicker);
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

	[Serializable]
	public struct ClientState
	{
		public string RemoteHost;
		public string LocalHost;
		public string Nick;
		public string Ident;
		public string RealName;
		public Dictionary<string, IrcChannel> Channels;
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
		private IrcProtocolParser parser = new IrcProtocolParser();
		private NetLibClient client;

		public const string Version = "2.1";
		private const int pingTimeout = 200;
		public bool ReplyToPings { get; set; }
		public int ChannelCount { get { return channels.Count; } }
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
		public int MaxJoinAttempts = 8;

		private bool QuitRequested = false;
		private bool _connected;
		public bool Connected
		{
			get
			{
				return _connected;
			}
			private set
			{
				_connected = value;
			}
		}

		public string RemoteHost { get; private set; }
		public string LocalHost { get; private set; }
		public string Nick { get; private set; }
		public string Ident { get; private set; }
		public string RealName { get; private set; }

		private int port;
		private bool invisible;
		private DateTime lastPing;
		private Timer timeoutCheck;
		private bool debug;

		public IrcClient(bool debug)
		{
			this.debug = debug;
			ReplyToPings = true;
			Logger.ClearLog();
			timeoutCheck = new Timer(OnTimeoutCheck, null, 5000, 5000);
		}

		private void OnTimeoutCheck(object state)
		{
			if (Connected && lastPing.Year != 1 && (DateTime.Now - lastPing).TotalSeconds > pingTimeout) {
				if (OnDisconnect != null) {
					DisconnectWithPingTimeout();
				}
			}
		}

		public void DisconnectWithPingTimeout()
		{
			client.DisconnectWithoutEvent();
			Connected = false;
			OnDisconnect(DisconnectReason.PingTimeout);
		}

		private void SetupClient()
		{
			client.OnDataAvailable += OnReceiveData;
			client.OnLogEvent += HandleLogEvent;
			client.OnDisconnect += HandleDisconnect;
		}

		private void HandleLogEvent(string message)
		{
			if (OnNetLibDebugLog != null) {
				OnNetLibDebugLog(message);
			}
		}

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
		/// Connects to an IRC server using the spestring.IsNullOrWhitespace(cified parameters.
		/// </summary>
		/// <param name="invisible">Determines whether the client should connect with mode +i (invisible to all users who aren't in the same channel)</param>
		public void Connect(ConnectionInfo ci)
		{
			Log("Starting IRCSharp version " + Version);
			Log("Using CsNetLib version " + NetLib.Version);

			if (string.IsNullOrWhiteSpace(ci.Host))
				throw new ArgumentException("Settings value is empty or does not exist", "Host");
			if(string.IsNullOrWhiteSpace(ci.Nick))
				throw new ArgumentException("Settings value is empty or does not exist", "Nick");
			if(string.IsNullOrWhiteSpace(ci.RealName)){
				throw new ArgumentException("Settings value is empty or does not exist", "RealName");
			}

			this.RemoteHost = ci.Host;
			this.port = ci.Port;
			this.Nick = ci.Nick;
			this.Ident = ci.Ident == null ? ci.Nick : ci.Ident;
			this.RealName = ci.RealName;
			this.invisible = ci.Invisible;

			InnerConnect();
		}
		private void InnerConnect()
		{
			Log(string.Format("Connecting to {0}:{1}", RemoteHost, port));
			try {
				SetupClient();
				client.Connect(RemoteHost, port);
			} catch (System.Net.Sockets.SocketException e) {
				throw e;
			}
			SendRaw("NICK " + Nick);
			SendRaw("USER " + Ident + " " + (invisible ? 8 : 0) + " * :" + RealName);
			Log("Credentials sent");
			while (!Connected) {
				Thread.Sleep(20);
			}
			Log("Connection to " + RemoteHost + " established.");

			if (OnConnectionEstablished != null)
				Task.Run(() => OnConnectionEstablished());
		}

		public bool InChannel(string channel)
		{
			return channels.ContainsKey(channel);
		}
		public void ChangeNick(string nick)
		{
			SendRaw("NICK :" + nick);
			this.Nick = nick;
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

		public ClientState GetClientState()
		{
			return new ClientState()
			{
				Ident = Ident,
				LocalHost = LocalHost,
				RemoteHost = RemoteHost,
				Nick = Nick,
				RealName = RealName,
				Channels = channels
			};
		}
		private void SetClientState(ClientState state)
		{
			Ident = state.Ident;
			LocalHost = state.LocalHost;
			RemoteHost = state.RemoteHost;
			Nick = state.Nick;
			RealName = state.RealName;
			channels = state.Channels;
		}


		private void ReplyToPing(string line)
		{
			lastPing = DateTime.Now;
			Connected = true;
			if (ReplyToPings) {
				string response = "PONG :" + line.Substring("PING :".Length);
				SendRaw(response);
			}
		}

		private void OnReceiveData(string line, long sender)
		{
			Logger.Log(line, LogLevel.In);

			if (line.StartsWith("PING")) {
				ReplyToPing(line);
				return;
			}
			IrcLine linef = parser.ParseIrcLine(line);
			if (OnRawLineReceived != null) {
				Task.Run(() => OnRawLineReceived(line));
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
						Task.Run(() => OnFormattedLineReceived(line));
					}
					break;
			}
		}

		private void ProcessQuit(IrcLine line)
		{
			if (OnQuit != null) {
				Task.Run(() => OnQuit(parser.GetUserFromSender(line.Sender), line.FinalArgument));
			}
		}
		private void ProcessNotice(IrcLine line)
		{
			if (OnNoticeReceived != null) {
				Task.Run(() => OnNoticeReceived(parser.GetUserFromSender(line.Sender), line.FinalArgument));
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
			IrcUser sender = parser.GetUserFromSender(line.Sender);
			if (sender.Nick == Nick) {
				channels.Remove(line.Arguments[0]);
				if (OnPartedChannel != null) {
					Task.Run(() => OnPartedChannel(line.Arguments[0]));
				}
			} else {
				channels[line.Arguments[0]].RemoveUser(sender.Nick);
				if (OnPartChannel != null) {
					Task.Run(() => OnPartChannel(sender, line.Arguments[0]));
				}
			}
		}
		private void ProcessJoin(IrcLine line)
		{
			IrcUser sender = parser.GetUserFromSender(line.Sender);
			if (sender.Nick == Nick) {
				if (Ident != sender.Ident) {
					Log(string.Format("Warning: Real ident ({0}) differs from requested ident ({1}). Ident field changed according to real ident", sender.Ident, Ident));
					Ident = sender.Ident;
				}
				if (LocalHost == null) {
					Log("Hostmask detected as " + sender.Hostmask);
					LocalHost = sender.Hostmask;
				}

				if (channels.ContainsKey(line.Arguments[0])) {
					throw new InvalidOperationException("Received a JOIN for " + line.Arguments[0] + " whil already in this channel.");
				} else {
					channels.Add(line.Arguments[0], new IrcChannel(line.Arguments[0]));
					if (OnJoinedChannel != null) {
						Task.Run(() => OnJoinedChannel(line.Arguments[0]));
					}
				}
			} else {
				channels[line.Arguments[0]].AddUser(sender.Nick, PermissionLevel.Default);
				if (OnJoinChannel != null) {
					Task.Run(() => OnJoinChannel(sender, line.Arguments[0]));
				}
			}
		}
		private void ProcessKick(IrcLine line)
		{
			IrcUser sender = parser.GetUserFromSender(line.Sender);
			if (line.Arguments[1].Equals(Nick)) {
				channels.Remove(line.Arguments[0]);
				if (OnKicked != null) {
					Task.Run(() => OnKicked(line.Arguments[0], line.FinalArgument, sender));
				}
			} else if (OnKick != null) {

				Task.Run(() => OnKick(line.Arguments[1], line.Arguments[0], line.FinalArgument, sender));
			}
		}
		public IrcChannel[] GetChannels()
		{
			return channels.Values.ToArray();
		}
		private void ProcessNickChange(IrcLine line)
		{
			if (OnNickChanged != null) {
				Task.Run(() => OnNickChanged(parser.GetUserFromSender(line.Sender), line.FinalArgument));
			}
		}
		private void ProcessPm(IrcLine line)
		{
			string actionSequence = "\u0001ACTION";

			bool action = false;

			if (line.FinalArgument.StartsWith(actionSequence)) {
				action = true;
				string message = line.FinalArgument;
				message = message.Substring(8, message.Length - 9);
				line.FinalArgument = message;
			}

			if (OnMessageReceived != null) {
				IrcUser sender = parser.GetUserFromSender(line.Sender);
				string channel;
				if (line.Arguments[0] == Nick) {
					channel = sender.Nick;
				} else {
					channel = line.Arguments[0];
				}
				IrcMessage msg = new IrcMessage(sender, channel, line.FinalArgument, action);

				if (debug) {
					OnMessageReceived(msg);
				} else {
					Task.Run(() => OnMessageReceived(msg));
				}
			}
		}

		/// <summary>
		/// Sends a raw IRC line to the server.
		/// </summary>
		/// <param name="data"></param>
		public bool SendRaw(string data)
		{
			Logger.Log(data, LogLevel.Out);
			var result = client.Send(data, 0);
			if (result) {
				// When the client sends a message to the IRC server, the IRC server will, in addition to processing this message,
				// also regard this as an indication that the client is still alive. Therefore, if it was about to send a ping to the client,
				// it will not do so, since the client has already indicated that it is alive.
				// For this reason, we must inform the client that (provided that the message is successfully sent) it is still connected to the 
				// IRC server. If this value is not set, the client will disconnect itself automatically because it isn't receiving any pings within
				// the spestring.IsNullOrWhitespace(cified ping timeout.
				lastPing = DateTime.Now;
			}
			return result;
		}

		private void Log(string message)
		{
			// We use a blocking call here, because it may be important that debug messages arrive in the right order.
			if (OnDebugLog != null) {
				OnDebugLog(message);
			}
		}
		/// <summary>
		/// Join a channel.
		/// </summary>
		/// <param name="channelName">The channel to join.</param>
		public void JoinChannel(string channelName, bool validate = true, int attemptNumber = 1)
		{
			channelName = channelName.ToLower();

			SendRaw("JOIN :" + channelName);
			if (validate) {
				int sleepTime = 50;
				int totalSleepTime = 0;
				while (!channels.ContainsKey(channelName)) {
					if (totalSleepTime > 1000) {
						if (attemptNumber == MaxJoinAttempts) {
							Log(string.Format("Maximum number of attempts to join {0} reached, channel not joined.", channelName));
							return;
						} else {
							Log(string.Format("Attempt to join {0} failed, retrying.", channelName));
							JoinChannel(channelName, true, ++attemptNumber);
							return;
						}
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
				partMessage += "," + channel;
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
				Host = RemoteHost,
				Port = port,
				Nick = Nick,
				Ident = Ident,
				RealName = RealName,
				Invisible = invisible
			};
		}

		public void Disconnect()
		{
			client.Disconnect();
		}

		public void AddOrCreateClient(NetLibClient client)
		{
			if (client == null) {
				this.client = new NetLibClient(TransferProtocolType.Delimited, Encoding.UTF8);
			} else {
				if (Connected) {
					throw new InvalidOperationException("Changing the network client while already connected is not allowed");
				}
				this.client = client;
			}
		}

		private void HandleDisconnect()
		{
			Connected = false;
			client.DisconnectWithoutEvent();
			Logger.Dispose();
			if (OnDisconnect != null) {
				if (QuitRequested)
					OnDisconnect(DisconnectReason.DisconnectOnRequest);
				else
					OnDisconnect(DisconnectReason.ServerDisconnect);
			}
		}

		public Dictionary<string, IrcChannel> Detach()
		{
			client.OnDataAvailable -= OnReceiveData;
			client.OnDisconnect -= HandleDisconnect;
			client.OnLogEvent -= HandleLogEvent;
			return channels;
		}

		public void Attach(ClientState state)
		{
			client = new NetLibClient(TransferProtocolType.Delimited, Encoding.UTF8);
			SetupClient();
			client.Connect("localhost", 6667);
			Connected = true;

			SetClientState(state);
		}

		public List<string> GetUsers(string channel)
		{
			return channels[channel].Users;
		}
	}
}
