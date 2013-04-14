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
	public delegate void NickChangeEvent(IrcUser user, string newNick);
	public delegate void KickEvent(string user, string channel);
	public delegate void KickedEvent(string channel);
	public delegate void DisconnectedEvent();

    public class IrcClient
    {
		public event ConnectionEstablishedEvent OnConnectionEstablished;
		public event DebugLogEvent OnDebugLog;
		public event RawLineReceiveEvent OnRawLineReceived;
		public event FormattedLineReceiveEvent OnFormattedLineReceived;
		public event MessageReceiveEvent OnMessageReceived;
		public event NickChangeEvent OnNickChanged;
		public event KickEvent OnKick;
		public event KickedEvent OnKicked;
		/// <summary>
		/// Fired whenever the client disconnects, whether intended or not.
		/// </summary>
		public event DisconnectedEvent OnDisconnect;
		/// <summary>
		/// Fired when the client loses connection, when not intended.
		/// </summary>
		public event DisconnectedEvent OnConnectionLost;
		

		// Maps channel names to IrcChannels
		private Dictionary<string, IrcChannel> channels = new Dictionary<string, IrcChannel>();

		private NetClient client;

		private bool QuitRequested = false;
		public bool Connected{ get; private set; }

		private string host, nick, ident, realName;
		private int port;
		private bool invisible;

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
			this.host = host;
			this.port = port;
			this.nick = nick;
			this.ident = ident;
			this.realName = realName;
			this.invisible = invisible;

			client.OnNetworkDataAvailabe += OnReceiveData;
			client.OnDisconnect += () =>
			{
				if (OnDisconnect != null)
					OnDisconnect();
				if (!QuitRequested && OnConnectionLost != null) {
					OnConnectionLost();
				}
			};
			InnerConnect();
		}

		private void InnerConnect()
		{
			client.Connect(host, port);
			SendRaw("NICK " + nick);
			SendRaw("USER " + ident + " " + (invisible ? 8 : 0) + " * :" + realName);
			while (!Connected) {
				Thread.Sleep(20);
			}
			Log("Connection to " + host + " established.");

			if (OnConnectionEstablished != null) OnConnectionEstablished();
		}

		public void Reconnect()
		{
			if (client.Connected) {
				throw new InvalidOperationException("Unable to reconnect: Client is already connected");
			} else {
				InnerConnect();
			}
		}

		public void ChangeNick(string nick)
		{
			SendRaw("NICK :" + nick);
			this.nick = nick;
		}

		public void Quit(string reason = null)
		{
			if (reason == null) {
				SendRaw("QUIT");
			} else {
				SendRaw("QUIT :" + reason);
			}
			QuitRequested = true;
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

		private void OnReceiveData(string line)
		{
			if(line.StartsWith("PING")){
				Connected = true;
				SendRaw("PONG :" + line.Substring("PING :".Length));
				return;
			}
			IrcLine linef = ParseIrcLine(line);

			if (OnRawLineReceived != null) {
				var t = new Thread(() => StartRawLineSend(line));
				t.Start();
			}

			if (OnFormattedLineReceived != null) {
				var t = new Thread(() => StartIrcLineSend(linef));
				t.Start();
			}

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
				case "NICK":
					ProcessNickChange(line);
					break;
				case "KICK":
					ProcessKick(line);
					break;
				default:
					break;
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

		public IrcUser GetUserFromSender(string sender)
		{
			string nick = sender.Substring(0, sender.IndexOf('!'));
			string ident = sender.Substring(sender.IndexOf('!') + 1, sender.IndexOf('@') - sender.IndexOf('!') - 1);
			string hostmask = sender.Substring(sender.IndexOf('@') + 1);
			return new IrcUser(nick, ident, hostmask);
		}

		private void ProcessNickChange(IrcLine line)
		{
			if (OnNickChanged != null) OnNickChanged(GetUserFromSender(line.Sender), line.FinalArgument);
		}

		private void ProcessPm(IrcLine line)
		{
			if (OnMessageReceived != null) {
				IrcMessage msg = new IrcMessage(GetUserFromSender(line.Sender), line.Arguments[0], line.FinalArgument);
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
