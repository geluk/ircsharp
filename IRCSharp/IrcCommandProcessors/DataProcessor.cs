using IRCSharp.IRC;

namespace IRCSharp.IrcCommandProcessors
{
	public abstract class DataProcessor
	{
		public virtual string PreProcessLine(string rawLine)
		{
			return rawLine;
		}

		public virtual IrcLine PostProcessLine(IrcLine line)
		{
			return line;
		}

		public virtual IrcMessage ProcessMessage(IrcMessage message)
		{
			return message;
		}
	}
}
