using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CsNetLib2;
using IRCSharp.IRC;
using IRCSharp.IrcCommandProcessors;
using System.Diagnostics;

namespace IRCSharp
{
	public class IrcClient
	{
		public event ConnectionEstablishedEvent OnConnectionEstablished; // The client has established a connection with the IRC server
		public event DebugLogEvent OnDebugLog;
		public event DebugLogEvent OnNetLibDebugLog;
		public event RawLineReceivedEvent OnRawLineReceived; // A raw IRC line is received
		public event FormattedLineReceivedEvent OnFormattedLineReceived; // A formatted IRC line has been created
		public event MessageReceivedEvent OnMessageReceived; // A message is received
		public event NickChangeEvent OnNickChange; // Someone changes their nick
		public event NickChangedEvent OnNickChanged; // The client changes their nick
		public event KickEvent OnKick; // Someone is kicked
		public event KickedEvent OnKicked; // The client is kicked
		public event DisconnectedEvent OnDisconnect; // The client disconnects, whether intended or not
		public event QuitEvent OnQuit; // Someone quits from IRC
		public event JoinChannelEvent OnJoinChannel; // Someone joins a channel
		public event PartChannelEvent OnPartChannel; // Someone parts a channel
		public event JoinedChannelEvent OnJoinedChannel; // The client joins a channel
		public event PartedChannelEvent OnPartedChannel; // The client parts a channel
		public event NoticeReceivedEvent OnNoticeReceived; // Client receives a notice from the IRC server
		public event TopicReceivedEvent OnTopicReceived; // Client receives the topic for a channel
		public event TopicSetEvent OnTopicSet; // Client receives the date and time on which a topic was set, and by whom it was set
		public event NamesKnownEvent OnNamesKnown; // Client has received all the names of the users inside a channel
		public event ErrorReceivedEvent OnErrorReceived; // Client receives an error
		public event WhoisResultReceivedEvent OnWhoisResultReceived // Client receives a WHOIS reply
		{
			add { commandProcessor.OnWhoisResultReceived += value; }
			remove { commandProcessor.OnWhoisResultReceived -= value; }
		}
		public event NickservInformationReceivedEvent OnNickservInformationReceived // Client receives information about a user from NickServ
		{
			add { commandProcessor.OnNickservInformationReceived += value; }
			remove { commandProcessor.OnNickservInformationReceived -= value; }
		}

		public List<DataProcessor> DataProcessors = new List<DataProcessor>();

		internal Dictionary<string, IrcChannel> ChannelDict { get; } = new Dictionary<string, IrcChannel>();

		public IReadOnlyList<IrcChannel> Channels => ChannelDict.Values.ToArray();

		private IrcClientProtocol clientProtocol;
		private CommandProcessor commandProcessor;

		public const string Version = "2.1";
		/// <summary>
		/// Maximum number of times the client should attempt to rejoin a channel if it
		/// doesn't manage to join the channel immediately.
		/// </summary>
		public int MaxJoinAttempts { get; set; } = 8;
		/// <summary>
		/// Maximum number of sub-messages a single message may be broken up into in order
		/// to prevent it from getting cut off because it exceeds the message length limit.
		/// </summary>
		public int FloodLimit { get; set; } = 4;
		/// <summary>
		/// Maximum message length. According to the spec this is 512 bytes.
		/// We subtract two to account for the \r\n terminator, leaving us with 510 bytes.
		/// </summary>
		public int MessageLengthLimit { get; set; } = 510;
		/// <summary>
		/// True if there is an active TCP connection between the IRC server and the 
		/// client AND if there has been network activity less than 200 (default,
		/// see <see cref="PingTimeout"/>) seconds ago.
		/// </summary>
		public bool Connected => clientProtocol.Connected;
		/// <summary>
		/// The hostname of the remote server.
		/// </summary>
		public string RemoteHost { get; private set; }
		/// <summary>
		/// The remote server port the client has connected to.
		/// </summary>
		public int RemotePort { get; private set; }
		/// <summary>
		/// The hostname assigned to this client by the IRC server.
		/// This may or may not match the host's actual hostname.
		/// </summary>
		public string LocalHost { get; internal set; }
		/// <summary>
		/// The nickname assigned to this client by the IRC server and (usually)
		/// as it was requested by the client.
		/// </summary>
		public string Nick { get; internal set; }
		/// <summary>
		/// The ident of this client, as assigned by the IRC server.
		/// This may or may not match the client's requested ident.
		/// </summary>
		public string Ident { get; internal set; }
		/// <summary>
		/// The real name reported by this client.
		/// </summary>
		public string RealName { get; private set; }
		public IrcUser Self => new IrcUser(Nick, Ident, LocalHost);
		/// <summary>
		/// The password that is used to connect to the IRC server.
		/// </summary>
		public string Password { get; private set; }
		public bool QuitRequested { get; private set; }

		/// <summary>
		/// Connects to an IRC server using the specified parameters.
		/// </summary>
		public void Connect(ConnectionInfo ci)
		{
			Log(this, "Starting IRCSharp version " + Version);
			Log(this, "Using CsNetLib version " + NetLib.Version);

			if (string.IsNullOrWhiteSpace(ci.Host))
				throw new ArgumentException("Settings value is empty or does not exist", "ci.Host");
			if (string.IsNullOrWhiteSpace(ci.Nick))
				throw new ArgumentException("Settings value is empty or does not exist", "ci.Nick");
			if (string.IsNullOrWhiteSpace(ci.RealName))
				throw new ArgumentException("Settings value is empty or does not exist", "ci.RealName");

			RemoteHost = ci.Host;
			RemotePort = ci.Port;
			Nick = ci.Nick;
			Ident = ci.Ident ?? ci.Nick;
			RealName = ci.RealName;
			Password = ci.Password;

			clientProtocol = new IrcClientProtocol(this);
			commandProcessor = new CommandProcessor(this);

			// Event plumbing
			clientProtocol.OnDisconnect += OnDisconnect;
			clientProtocol.OnDataAvailable += commandProcessor.OnReceiveData;
			clientProtocol.OnNetLibDebugLog += (msg) => OnNetLibDebugLog?.Invoke(clientProtocol, msg);
			clientProtocol.OnDebugLog += OnDebugLog;
			// FORWARD ALL THE EVENTS
			commandProcessor.OnPingReceived += clientProtocol.ReplyToPing;
			commandProcessor.OnRawLineReceived += OnRawLineReceived;
			commandProcessor.OnFormattedLineReceived += OnFormattedLineReceived;
			commandProcessor.OnMessageReceived += OnMessageReceived;
			commandProcessor.OnNickChange += OnNickChange;
			commandProcessor.OnNickChanged += OnNickChanged;
			commandProcessor.OnKick += OnKick;
			commandProcessor.OnKicked += OnKicked;
			commandProcessor.OnQuit += OnQuit;
			commandProcessor.OnJoinChannel += OnJoinChannel;
			commandProcessor.OnPartChannel += OnPartChannel;
			commandProcessor.OnJoinedChannel += OnJoinedChannel;
			commandProcessor.OnPartedChannel += OnPartedChannel;
			commandProcessor.OnNoticeReceived += OnNoticeReceived;
			commandProcessor.OnTopicReceived += OnTopicReceived;
			commandProcessor.OnTopicSet += OnTopicSet;
			commandProcessor.OnNamesKnown += OnNamesKnown;
			commandProcessor.OnErrorReceived += OnErrorReceived;

			clientProtocol.Connect(ci);

			if (OnConnectionEstablished != null)
				Task.Run(() => OnConnectionEstablished());
		}

		/// <summary>
		/// Changes the nickname of the current user.
		/// </summary>
		/// <param name="nick"></param>
		public void ChangeNick(string nick)
		{
			// TODO: validate the nick change
			clientProtocol.ChangeNick(nick);
			Nick = nick;
		}

		/// <summary>
		/// Quit from the IRC server. Optionally, a reason may be specified.
		/// </summary>
		/// <param name="reason"></param>
		public void Quit(string reason = null)
		{
			clientProtocol.Quit(reason);
			while (Connected)
			{
				Thread.Sleep(10);
			}
		}

		/// <summary>
		/// Leave a channel. Optionally, a reason may be specified.
		/// </summary>
		/// <param name="channel"></param>
		/// <param name="reason"></param>
		public void Part(string channel, string reason = null)
		{
			clientProtocol.Part(channel, reason);
		}

		/// <summary>
		/// Leave multiple channels.
		/// </summary>
		/// <param name="channelList">The channelList to leave.</param>
		public void LeaveChannels(List<string> channelList)
		{
			clientProtocol.Part(channelList);
		}

		private void Log(object sender, string message)
		{
			OnDebugLog?.Invoke(sender, message);
		}

		/// <summary>
		/// Join a channel.
		/// </summary>
		/// <param name="channelName">The channel to join.</param>
		/// <param name="validate">If true, checks whether the channel was actually joined.</param>
		/// <returns>True if <paramref name="validate"/> is set to false. When set to True,
		/// returns True if the channel was successfully joined, False if it was not.</returns>
		public bool JoinChannel(string channelName, bool validate = true)
		{
			channelName = channelName.ToLower();
			Log(this, "Joining " + channelName);
			clientProtocol.Join(channelName);
			if (!validate) return true;

			for (int i = 0; i < MaxJoinAttempts; i++)
			{
				if (ChannelDict.ContainsKey(channelName))
				{
					return true;
				}
				else
				{
					Thread.Sleep(1000);
					Log(this, "Joining " + channelName);
					clientProtocol.Join(channelName);
				}
			}
			return false;
		}

		/// <summary>
		/// Generates the prefix string that any messages coming from this client
		/// will be prefixed with.
		/// </summary>
		/// <param name="target">The target the message should be sent to.</param>
		/// <returns></returns>
		private string GeneratePrefix(string target)
		{
			return $":{Nick}!{Ident}@{LocalHost} PRIVMSG {target} :";
		}

		/// <summary>
		/// Generates the full IRC command that will be sent to other clients when
		/// this client sends <paramref name="message"/> to <paramref name="target"/>.
		/// </summary>
		/// <param name="target">The target the message should be sent to.</param>
		/// <param name="message">The contents of the message.</param>
		/// <returns></returns>
		private string GenerateFullMessage(string target, string message)
		{
			return $":{GeneratePrefix(target)}{message}";
		}

		/// <summary>
		/// Calculates the maximum effective length for an IRC message.
		/// The maximum effective length is considered to be length the actual
		/// message itself must not exceed in order for the full IRC command
		/// that will be received by other clients to not exceed 512 bytes in length.
		/// </summary>
		/// <param name="target">The target the message should be sent to.</param>
		/// <returns>The maximum number of bytes a message may contain in order for it
		/// to not get cut off at the end.</returns>
		private int GetMaxMessageLength(string target)
		{
			return MessageLengthLimit - GeneratePrefix(target).Length;
		}

		/// <summary>
		/// Sends a message to the target.
		/// </summary>
		/// <param name="target">The target the message should be sent to.</param>
		/// <param name="message">The contents of the message.</param>
		public MessageSendResult SendMessage(string target, string message)
		{
			return SendMessageChunk(target, message);
			//return clientProtocol.SendMessage(target, message) ? MessageSendResult.Success : MessageSendResult.Failure;
		}

		/// <summary>
		/// Sends a message to the target. This is a recursive method that, 
		/// if necessary, will break up longer messages, sending them in parts.
		/// </summary>
		/// <param name="target">The target the message should be sent to.</param>
		/// <param name="message">The contents of the message.</param>
		/// <param name="messageNumber">Indicates that the message about to be sent
		/// will be the nth message.  Because this is a numbering scheme (not indexing),
		/// it starts at one. If a message gets chopped up, the second part will
		/// have messageNumber = 2, for the third part it will be 3, and so on.</param>
		/// <returns></returns>
		private MessageSendResult SendMessageChunk(string target, string message, int messageNumber = 1)
		{
			if (messageNumber > FloodLimit)
			{
				return MessageSendResult.FloodLimitHit;
			}
			string cutoff = null;
			// CAVEAT: C# uses UTF-16 strings, but IRC uses UTF-8. Therefore, a charcter with a code point
			// low enough to be a single char in UTF-16 yet high enough to require two chars in UTF-8
			// will be incorrectly reported to have a length of one byte. Unfortunately there is no easy
			// way to fix this because we don't know how to chop up Unicode messages byte-by-byte.
			// At some point this should get looked at, but if we're going to implement support for that,
			// it should be done right.
			if (GenerateFullMessage(target, message).Length > MessageLengthLimit)
			{
				cutoff = message.Substring(GetMaxMessageLength(target));
				message = message.Substring(0, GetMaxMessageLength(target));
			}

			var result = clientProtocol.SendMessage(target, message);
			if (result)
			{
				return cutoff == null ? MessageSendResult.Success : SendMessageChunk(target, cutoff, ++messageNumber);
			}
			else
			{
				return MessageSendResult.Failure;
			}
		}

		/// <summary>
		/// Performs a WHOIS call on an IRC user.
		/// </summary>
		/// <param name="user">The nickname of the user to perform a WHOIS call on.</param>
		/// <returns>An IrcUser object containing information about the requested user.</returns>
		public IrcUser Whois(string user)
		{
			IrcUser whoisResult = null;
			var matchFound = false;
			var whoisHandler = new WhoisResultReceivedEvent(ircUser =>
			{
				if (!matchFound && string.Equals(ircUser.Nick, user, StringComparison.InvariantCultureIgnoreCase))
				{
					matchFound = true;
					whoisResult = ircUser;
				}
			});

			OnWhoisResultReceived += whoisHandler;
			clientProtocol.Whois(user);
			while (!matchFound)
			{
				Thread.Sleep(100);
			}
			// We got the event we're looking for, so we should unsubscribe.
			OnWhoisResultReceived -= whoisHandler;
			return whoisResult;
		}

		/// <summary>
		/// Performs a NickServ lookup on an IRC user, returning their NickServ
		/// username if they have one.
		/// </summary>
		/// <param name="user">The user to perform a NickServ lookup on.</param>
		/// <returns><paramref name="user"/>'s NickServ username if they have 
		/// one, or null if they don't.</returns>
		public NickservInformation NickservLookup(string user)
		{
			NickservInformation lookupResult = null;
			var matchFound = false;
			var nickservHandler = new NickservInformationReceivedEvent(nickservInformation =>
			{
				if (!matchFound && (nickservInformation == null || string.Equals(nickservInformation.Nickname, user, StringComparison.InvariantCultureIgnoreCase)))
				{
					matchFound = true;
					lookupResult = nickservInformation;
				}
			});

			OnNickservInformationReceived += nickservHandler;
			clientProtocol.NickServ(user);
			int i = 0;
			for (; !matchFound && i < 50; i++)
			{
				Thread.Sleep(100);
			}
			if (i >= 50)
			{
				Log(this, $"WARNING: Nickserv lookup for user {user} timed out.");
				Debugger.Break();
			}

			OnNickservInformationReceived -= nickservHandler;
			return lookupResult;
		}

		/// <summary>
		/// Disconnect from the server. This is faster than <see cref="Quit"/>
		/// as it doesn't notify the server and instantly closes the connection.
		/// See also <seealso cref="Quit"/>
		/// </summary>
		public void Disconnect()
		{
			clientProtocol.Disconnect();
		}

		public void HandleLogMessage(object sender, string message)
		{
			Log(sender, message);
		}

		public void SetConnectionState(bool newState)
		{
			clientProtocol.Connected = newState;
		}

		/// <summary>
		/// Join multiple channels.
		/// </summary>
		/// <param name="channels"></param>
		/// <returns></returns>
		public bool JoinChannels(IEnumerable<string> channels)
		{
			bool success = true;
			foreach (var channel in channels)
			{
				success = success && JoinChannel(channel);
			}
			return success;
		}
	}
}
