using System;
using IRCSharp.IRC;

namespace IRCSharp
{
	public delegate void PingReceivedEvent(string argument);
	public delegate void ConnectionEstablishedEvent();
	public delegate void DebugLogEvent(object sender, string message);
	public delegate void RawLineReceivedEvent(string line);
	public delegate void FormattedLineReceivedEvent(IrcLine line);
	public delegate void MessageReceivedEvent(IrcMessage message);
	public delegate void NickChangeEvent(IrcUser user, string newNick);
	public delegate void NickChangedEvent(string newNick);
	public delegate void KickEvent(string kickee, string channel, string reason, IrcUser kicker);
	public delegate void KickedEvent(string channel, string reason, IrcUser kicker);
	public delegate void DisconnectedEvent(DisconnectReason reason, Exception ex);
	public delegate void QuitEvent(IrcUser user, string reason);
	public delegate void JoinedChannelEvent(string channel);
	public delegate void PartedChannelEvent(string channel);
	public delegate void JoinChannelEvent(IrcUser user, string channel);
	public delegate void PartChannelEvent(IrcUser user, string channel);
	public delegate void NoticeReceivedEvent(IrcUser user, string notice);
	public delegate void TopicReceivedEvent(string channel, string topic);
	public delegate void TopicSetEvent(string channel, IrcUser user, DateTime time);
	public delegate void NamesKnownEvent(IrcChannel channel);
	public delegate void ErrorReceivedEvent(string error);
	public delegate void WhoisResultReceivedEvent(IrcUser user);
	public delegate void NickservInformationReceivedEvent(NickservInformation information);
}
