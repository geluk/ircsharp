using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IRCSharp
{
	public struct IrcLine
	{
		public IrcUser User;
		public string Sender;
		public string Command;
		public string[] Arguments;
		public string FinalArgument;

		public IrcLine(string sender, IrcUser user, string command, string[] args, string final)
		{
			Sender = sender;
			User = user;
			Command = command;
			Arguments = args;
			FinalArgument = final;
		}

		public string ShortForm()
		{
			StringBuilder sb = new StringBuilder();

			for (int i = 1; i < Arguments.Length; i++) {
				if (Arguments[i] != string.Empty) {
					sb.Append(Arguments[i]);
					sb.Append(" ");
				}
			}
			if (FinalArgument != null) {
				sb.Append(FinalArgument);
			}
			string line = sb.ToString();
			return line;
		}

		public override string ToString()
		{
			StringBuilder sb = new StringBuilder();
			if (Sender != null) {
				sb.Append(":");
				sb.Append(Sender);
				sb.Append(" ");

			}
			if (Command != null) {
				sb.Append(Command);
				sb.Append(" ");
			}
			foreach(string s in Arguments)
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
			string line = sb.ToString();
			return line;
		}
	}
}
