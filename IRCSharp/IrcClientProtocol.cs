using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CsNetLib2;
using CsNetLib2.Transfer;

namespace IRCSharp
{
	public enum DisconnectReason
	{
		PingTimeout,
		Other,
		DisconnectOnRequest
	}

	/// <summary>
	/// Internal class for handling communication with the IRC server.
	/// A <see cref="CommandProcessor"/> should be used to handle incoming data, which
	/// can be done by connecting it to the <see cref="OnDataAvailable"/> event.
	/// This class takes care of formatting outgoing commands according to the IRC
	/// client protocol as specified by RFC1459 and updated by RFC2812 while also
	/// including support for several other popular extensions and services, such as
	/// CTCP and NickServ.
	/// </summary>
	internal class IrcClientProtocol
	{
		internal event DisconnectedEvent OnDisconnect;
		internal event LogEvent OnNetLibDebugLog;
		internal event DataAvailabeEvent OnDataAvailable;
		internal event DebugLogEvent OnDebugLog;

		private const int pingTimeout = 200;

		private readonly NetLibClient client;
		private readonly IrcClient ircClient;

		private DateTime lastPing;
		private Timer timer;

		internal bool Connected { get; set; }
		internal bool ReplyToPings { get; set; } = true;

		internal IrcClientProtocol(IrcClient ircClient)
		{
			this.ircClient = ircClient;
			this.client = new NetLibClient(TransferProtocolType.Delimited, Encoding.UTF8);
			client.OnDisconnect += HandleDisconnect;
			client.OnDataAvailable += (data, id) => OnDataAvailable?.Invoke(data, id);
			client.OnLogEvent += (data) => OnNetLibDebugLog?.Invoke(data);

			timer = new Timer(OnTimeoutCheck, null, 5000, 5000);
		}

		private void Log(string message)
		{
			OnDebugLog?.Invoke(this, message);
		}

		private void HandleDisconnect(Exception reason)
		{
			if (!Connected)
			{
				Log("Ignoring disconnect event because the client has already disconnected.");
				Log($"Connection lost ({reason.GetType().Name}: {reason.Message}) Attempting to reconnect...");
				return;
			}
			DisconnectWithoutEvent();
			if (OnDisconnect == null) return;

			if (ircClient.QuitRequested)
				OnDisconnect(DisconnectReason.DisconnectOnRequest, null);
			else
				OnDisconnect(DisconnectReason.Other, reason);
		}

		private void OnTimeoutCheck(object state)
		{
			if (!Connected || lastPing.Year == 1 || !((DateTime.Now - lastPing).TotalSeconds > pingTimeout)) return;
			DisconnectWithPingTimeout();
		}

		/// <summary>
		/// Disconnect from the server by simulating a ping timeout.
		/// </summary>
		private void DisconnectWithPingTimeout()
		{
			DisconnectWithoutEvent();
			OnDisconnect(DisconnectReason.PingTimeout, null);
		}

		internal void Join(string channelName)
		{
			SendRaw("JOIN :" + channelName);
		}

		internal void Authenticate(string username, string password, string ident, string realName, bool invisible)
		{
			if (!string.IsNullOrEmpty(password))
			{
				SendRaw("PASS " + password);
			}
			SendRaw("NICK " + username);
			SendRaw("USER " + ident + " " + (invisible ? 8 : 0) + " * :" + realName);
			Log("Credentials sent");
		}

		internal void Part(string channel, string reason = null)
		{
			if (reason == null)
			{
				SendRaw("PART " + channel);
			}
			else
			{
				SendRaw($"PART {channel} :{reason}");
			}
		}

		internal void Part(IEnumerable<string> channels)
		{
			var partMessage = channels.Aggregate("", (current, channel) => current + "," + channel);
			SendRaw("PART " + partMessage.Substring(1));
		}

		internal void ReplyToPing(string line)
		{
			lastPing = DateTime.Now;
			Connected = true;
			if (ReplyToPings)
			{
				var response = "PONG :" + line.Substring("PING :".Length);
				SendRaw(response);
			}
		}

		/// <summary>
		/// Sends a raw IRC line to the server.
		/// </summary>
		/// <param name="data"></param>
		internal bool SendRaw(string data)
		{
			var result = client.Send(data, 0);
			if (result)
			{
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

		internal void ChangeNick(string nick)
		{
			SendRaw("NICK :" + nick);
		}

		internal void Quit(string reason)
		{
			if (reason == null)
			{
				SendRaw("QUIT");
			}
			else
			{
				var line = "QUIT :" + reason;
				SendRaw(line);
			}
		}

		internal void DisconnectWithoutEvent()
		{
			client.DisconnectWithoutEvent();
			Connected = false;
		}
		
		internal void Connect(ConnectionInfo ci)
		{
			if (ci.useTLS)
			{
				client.ConnectSecure(ci.Host, ci.Port, true, ci.verifyServerCertificate);
			}
			else
			{
				client.Connect(ci.Host, ci.Port);
			}
			Authenticate(ci.Nick, ci.Password, ci.Ident, ci.RealName, ci.Invisible);

			var waitingTime = 0;
			while (!Connected && waitingTime < 10000)
			{
				Thread.Sleep(20);
				waitingTime += 20;
			}
			if (Connected)
			{
				Log($"Connection to {ci.Host} established.");
			}
			else
			{
				throw new Exception("Connection timed out, took longer than 10 seconds to connect");
			}
		}

		internal bool SendMessage(string target, string message)
		{
			// Make sure any newline characters are stripped from the message.
			// Replace \n with whitespace, otherwise two words might end up directly next to each other, with no space between them.
			// By replacing only \n and not \r, we cover all lines terminated by \n and \r\n. Good enough for our purposes.
			message = message.Replace('\n', ' ').Replace("\r", "");

			foreach (var processor in ircClient.DataProcessors)
			{
				message = processor.PostProcessOutgoingMessage(message);
			}

			if (Connected)
			{
				return SendRaw("PRIVMSG " + target + " :" + message);
			}
			throw new InvalidOperationException("Attempt to send a message while the client is not connected to a server");
		}

		internal void Disconnect()
		{
			client.Disconnect();
		}

		internal void Whois(string user)
		{
			SendRaw("WHOIS " + user);
		}

		internal void NickServ(string user)
		{
			SendMessage("NickServ", "INFO " + user);
		}
	}
}
