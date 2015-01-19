using System.Text;

namespace IRCSharp.IRC
{
	public struct IrcLine
	{
		public IrcUser User;
		public string Sender;
		public string Command;
		public string[] Arguments;
		public string FinalArgument;
		public string RawLine;

		public IrcLine(string sender, IrcUser user, string command, string[] args, string final, string raw)
		{
			Sender = sender;
			User = user;
			Command = command;
			Arguments = args;
			FinalArgument = final;
			RawLine = raw;
		}

		public string ShortForm()
		{
			var sb = new StringBuilder();

			for (var i = 1; i < Arguments.Length; i++) {
				if (Arguments[i] != string.Empty) {
					sb.Append(Arguments[i]);
					sb.Append(" ");
				}
			}
			if (FinalArgument != null) {
				sb.Append(FinalArgument);
			}
			var line = sb.ToString();
			return line;
		}

		public override string ToString()
		{
			var sb = new StringBuilder();
			if (Sender != null) {
				sb.Append(":");
				sb.Append(Sender);
				sb.Append(" ");

			}
			if (Command != null) {
				sb.Append(Command);
				sb.Append(" ");
			}
			foreach(var s in Arguments)
			{
				if (s != string.Empty) {
					sb.Append(s);
					sb.Append(" ");
				}
			}
			if (FinalArgument != null) {
				sb.Append(":");
				sb.Append(FinalArgument);
			}
			var line = sb.ToString();
			return line;
		}
	}
}
