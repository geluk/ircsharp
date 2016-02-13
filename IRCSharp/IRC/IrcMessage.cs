namespace IRCSharp.IRC
{
	public class IrcMessage
	{
		public IrcUser Sender { get; }
		public string Channel { get; }
		public string Message { get; set; }
		public bool Action { get; }

		public IrcMessage(IrcUser sender, string channel, string message, bool action = false)
		{
			Sender = sender;
			Channel = channel;
			Message = message;
			Action = action;
		}
	}
}
