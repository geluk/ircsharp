using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IRCSharp
{
	class IrcProtocolParser
	{
		public IrcUser GetUserFromSender(string sender)
		{
			if (sender == null) {
				return new IrcUser(null, null, null);
			}
			if (!sender.Contains("!")) {
				string nick = String.Empty;
				string ident = String.Empty;
				string hostmask = sender;
				return new IrcUser(nick, ident, hostmask);
			} else {
				string nick = sender.Substring(0, sender.IndexOf('!'));
				string ident = sender.Substring(sender.IndexOf('!') + 1, sender.IndexOf('@') - sender.IndexOf('!') - 1);
				string hostmask = sender.Substring(sender.IndexOf('@') + 1);
				return new IrcUser(nick, ident, hostmask);
			}
		}

		public IrcLine ParseIrcLine(string line)
		{
			// If the line starts with a colon, it contains information about the sender.
			bool hasSender = line.StartsWith(":");

			// Clean up the line a bit
			if (hasSender)
				line = line.Substring(1);
			if (line.EndsWith("\r"))
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

			return new IrcLine(sender, GetUserFromSender(sender), command, args, finalArg);
		}
	}
}
