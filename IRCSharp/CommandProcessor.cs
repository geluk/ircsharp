using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using IRCSharp.IRC;

namespace IRCSharp
{
	public class CommandProcessor
	{
		public event RawLineReceivedEvent OnRawLineReceived; // A raw IRC line is received
		public event FormattedLineReceivedEvent OnFormattedLineReceived; // A formatted IRC line has been created
		public event MessageReceivedEvent OnMessageReceived; // A message is received
		public event NickChangeEvent OnNickChange; // Someone changes their nick
		public event NickChangedEvent OnNickChanged; // The client changes their nick
		public event KickEvent OnKick; // Someone is kicked
		public event KickedEvent OnKicked; // The client is kicked
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
		public event PingReceivedEvent OnPingReceived; // Client receives a ping
		public event WhoisResultReceivedEvent OnWhoisResultReceived; // Client receives a WHOIS result
		public event NickservInformationReceivedEvent OnNickservInformationReceived; // Client receives information about a NickServ account

		private readonly IrcClient client;

		private bool collectingNickservInformation;
		private NickservInformation information;

		public CommandProcessor(IrcClient client)
		{
			this.client = client;
		}

		private void Log(string message)
		{
			client.HandleLogMessage(this, message);
		}

		internal void OnReceiveData(string line, long sender)
		{
			// Before doing anything, we'll give any preprocessors a chance to alter the incoming line
			// if they deem it necessary.
			foreach (var processor in client.DataProcessors)
			{
				line = processor.PreProcessLine(line);
			}

			// There's no need to expose a public API for pings, so we can return immediately after handling them.
			if (line.StartsWith("PING"))
			{
				OnPingReceived?.Invoke(line);
				return;
			}

			var linef = IrcProtocolParser.ParseIrcLine(line);

			// Now we'll give the postprocessors a chance to alter the incoming line.
			foreach (var processor in client.DataProcessors)
			{
				linef = processor.PostProcessLine(linef);
			}

			if (OnRawLineReceived != null)
			{
				OnRawLineReceived(line);
			}
			ProcessIrcLine(linef);
		}

		private void ProcessIrcLine(IrcLine line)
		{
			switch (line.Command)
			{
				case "001":
					client.SetConnectionState(true);
					OnFormattedLineReceived?.Invoke(line);
					break;
				case "PRIVMSG":
					ProcessPm(line);
					break;
				case "NOTICE":
					ProcessNotice(IrcProtocolParser.GetUserFromSender(line.Sender), line.FinalArgument);
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
				case "QUIT":
					OnQuit?.Invoke(IrcProtocolParser.GetUserFromSender(line.Sender), line.FinalArgument);
					break;
				case "311":
					OnWhoisResultReceived?.Invoke(new IrcUser(line.Arguments[1], line.Arguments[2], line.Arguments[3]));
					break;
				case "332":
					OnTopicReceived?.Invoke(line.Arguments[1], line.FinalArgument);
					break;
				case "333":
					ProcessTopicSet(line);
					break;
				case "366":
					OnNamesKnown?.Invoke(client.ChannelDict[line.Arguments[1]]);
					break;
				case "ERROR":
					OnErrorReceived?.Invoke(line.FinalArgument);
					break;
				default:
					OnFormattedLineReceived?.Invoke(line);
					break;
			}
		}

		private void ProcessNotice(IrcUser sender, string notice)
		{
			if (sender.Nick == "NickServ" && sender.Ident == "NickServ")
			{
				ProcessNickservReply(sender, notice);
			}
			else
			{
				OnNoticeReceived?.Invoke(sender, notice);
			}
		}

		private void ProcessTopicSet(IrcLine line)
		{
			int seconds;
			if (!int.TryParse(line.Arguments[3], out seconds))
			{
				Log("Failed to parse topic set time, defaulting to Unix epoch");
			}
			
			OnTopicSet?.Invoke(line.Arguments[1], IrcProtocolParser.GetUserFromSender(line.Arguments[2]), new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddSeconds(seconds));
		}

		private void ProcessNameReply(IrcLine line)
		{
			var channelName = line.Arguments[2];
			IrcChannel channel;
			try
			{
				channel = client.ChannelDict[channelName];
			}
			catch (KeyNotFoundException)
			{
				Log("Unable to process name reply: channel not found");
				return;
			}
			var users = line.FinalArgument.Split(' ');
			foreach (var user in users)
			{
				if (user.StartsWith("@"))
				{
					channel.AddUser(user.Substring(1), IrcPermissionLevel.Operator);
				}
				else if (user.StartsWith("+"))
				{
					channel.AddUser(user.Substring(1), IrcPermissionLevel.Voiced);
				}
				else
				{
					channel.AddUser(user, IrcPermissionLevel.Default);
				}
			}
		}
		private void ProcessPart(IrcLine line)
		{
			var sender = IrcProtocolParser.GetUserFromSender(line.Sender);
			if (sender.Nick != client.Nick)
			{
				client.ChannelDict.Remove(line.Arguments[0]);
				OnPartedChannel?.Invoke(line.Arguments[0]);
			}
			else
			{
				client.ChannelDict[line.Arguments[0]].RemoveUser(sender.Nick);
				OnPartChannel?.Invoke(sender, line.Arguments[0]);
			}
		}
		private void ProcessJoin(IrcLine line)
		{
			var sender = IrcProtocolParser.GetUserFromSender(line.Sender);

			var channel = line.FinalArgument;
			if (sender.Nick == client.Nick)
			{
				if (client.Ident != sender.Ident)
				{
					Log($"Warning: Real ident ({sender.Ident}) differs from requested ident ({client.Ident}). Ident field changed to match ident assigned by the server.");
					client.Ident = sender.Ident;
				}
				if (client.LocalHost == null)
				{
					Log("Hostmask detected as " + sender.Hostmask);
					client.LocalHost = sender.Hostmask;
				}
				if (client.ChannelDict.ContainsKey(channel))
				{
					throw new InvalidOperationException("Received a JOIN for " + channel + " while already in this channel.");
				}
				client.ChannelDict.Add(channel, new IrcChannel(channel));
				OnJoinedChannel?.Invoke(channel);
			}
			else
			{
				client.ChannelDict[channel].AddUser(sender.Nick, IrcPermissionLevel.Default);
				OnJoinChannel?.Invoke(sender, channel);
			}
		}
		private void ProcessKick(IrcLine line)
		{
			var sender = IrcProtocolParser.GetUserFromSender(line.Sender);
			if (line.Arguments[1].Equals(client.Nick))
			{
				client.ChannelDict.Remove(line.Arguments[0]);
				OnKicked?.Invoke(line.Arguments[0], line.FinalArgument, sender);
			}
			else
			{
				OnKick?.Invoke(line.Arguments[1], line.Arguments[0], line.FinalArgument, sender);
			}
		}
		private void ProcessNickChange(IrcLine line)
		{
			if (line.User.Nick == client.Nick)
			{
				client.Nick = line.FinalArgument;
				OnNickChanged?.Invoke(line.FinalArgument);
			}
			else
			{
				OnNickChange?.Invoke(IrcProtocolParser.GetUserFromSender(line.Sender), line.FinalArgument);
			}
		}
		internal void ProcessNickservReply(IrcUser sender, string notice)
		{
			if (notice.StartsWith("Information on "))
			{
				collectingNickservInformation = true;
				var data = notice.Substring("Information on  ".Length);

				var nick = data.Substring(0, data.IndexOf(" ", StringComparison.InvariantCulture) - 1);
				data = data.Substring(nick.Length + 2 + "(account  ".Length);
				var nickserv = data.Substring(0, data.Length - 3);

				information = new NickservInformation();
				information.Nickname = nick;
				information.AccountName = nickserv;
			}
			else if (notice.EndsWith("is not registered."))
			{
				var nick = notice.Substring(1, notice.Length - 2);
				nick = nick.Substring(0, nick.IndexOf(' ') - 1);
				OnNickservInformationReceived?.Invoke(null);
			}else if (collectingNickservInformation)
			{
				if (Regex.IsMatch(notice, @"\*\*\*.?\s+.?[Ee]nd [Oo]f [Ii]nfo.?\s+.?\*\*\*"))
				{
					collectingNickservInformation = false;
					var toSend = information;
					information = null;
					OnNickservInformationReceived?.Invoke(toSend);
					return;
				}
				var match = Regex.Match(notice, @"^[Rr]egistered\s+:\s+(.*) \(.*\)$");
				if (match.Success)
				{
					information.Registered = DateTime.ParseExact(match.Groups[1].Value, "MMM d HH:mm:ss yyyy", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
					return;
				}
				match = Regex.Match(notice, @"^[Ee]ntity [Ii][Dd]\s+:\s+(.*)$");
				if (match.Success)
				{
					information.EntityID = match.Groups[1].Value;
					return;
				}
				match = Regex.Match(notice, @"^[Ll]ast [Aa]ddr\s+:\s+(.*)$");
				if (match.Success)
				{
					information.LastAddress = match.Groups[1].Value;
					return;
				}
				match = Regex.Match(notice, @"^[Ll]ast [Ss]een\s+:\s+(.*) \(.*\)$");
				if (match.Success)
				{
					information.LastSeen = DateTime.ParseExact(match.Groups[1].Value, "MMM d HH:mm:ss yyyy", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
					return;
				}
				match = Regex.Match(notice, @"[Ll]ast [Ss]een\s+:\s+[Nn]ow$");
				if (match.Success)
				{
					information.LastSeen = DateTime.UtcNow;
					return;
				}
				match = Regex.Match(notice, @"^[Ff]lags\s+:\s+(.*)$");
				if (match.Success)
				{
					information.Flags = match.Groups[1].Value;
					return;
				}
				match = Regex.Match(notice, @"^.*\s+has enabled nick protection$");
				if (match.Success)
				{
					return;
				}
				Log("Ignoring unrecognised NickServ reply: " + notice);
			}
		}

		private void ProcessPm(IrcLine line)
		{
			var parsedMessage = line.FinalArgument;
			var action = IrcProtocolParser.ParseAction(ref parsedMessage);
			var sender = IrcProtocolParser.GetUserFromSender(line.Sender);
			// If it's a private message, the target field will be the client's client.Nick. Otherwise, it will be a channel name.
			var channel = line.Arguments[0] == client.Nick ? sender.Nick : line.Arguments[0];
			var message = new IrcMessage(sender, channel, parsedMessage, action);

			foreach (var processor in client.DataProcessors)
			{
				message = processor.ProcessMessage(message);
			}
			OnMessageReceived?.Invoke(message);

		}
	}


}