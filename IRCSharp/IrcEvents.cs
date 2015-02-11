using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IRCSharp.IRC;

namespace IRCSharp
{
	public delegate void ConnectionEstablishedEvent();
	public delegate void DebugLogEvent(object sender, string message);
	public delegate void RawLineReceiveEvent(string line);
	public delegate void FormattedLineReceiveEvent(IrcLine line);
	public delegate void MessageReceiveEvent(IrcMessage message);
	public delegate void NickChangeEvent(IrcUser user, string newNick);
	public delegate void NickChangedEvent(string newNick);
	public delegate void KickEvent(string user, string channel, string reason, IrcUser kicker);
	public delegate void KickedEvent(string channel, string reason, IrcUser kicker);
	public delegate void DisconnectedEvent(DisconnectReason reason, Exception ex);
	public delegate void QuitEvent(IrcUser user, string reason);
	public delegate void JoinedChannelEvent(string channel);
	public delegate void PartedChannelEvent(string channel);
	public delegate void JoinChannelEvent(IrcUser user, string channel);
	public delegate void PartChannelEvent(IrcUser user, string channel);
	public delegate void NoticeReceiveEvent(IrcUser user, string notice);
	public delegate void TopicReceiveEvent(string channel, string topic);
	public delegate void TopicSetEvent(string channel, IrcUser user, DateTime time);
	public delegate void NamesKnownEvent(IrcChannel channel);
	public delegate void ErrorReceivedEvent(string error);
}
