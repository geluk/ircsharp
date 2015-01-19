using System;
using System.Linq;
using IRCSharp.IRC;

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
				var nick = String.Empty;
				var ident = String.Empty;
				var hostmask = sender;
				return new IrcUser(nick, ident, hostmask);
			} else {
				var nick = sender.Substring(0, sender.IndexOf('!'));
				var ident = sender.Substring(sender.IndexOf('!') + 1, sender.IndexOf('@') - sender.IndexOf('!') - 1);
				var hostmask = sender.Substring(sender.IndexOf('@') + 1);
				return new IrcUser(nick, ident, hostmask);
			}
		}

		public IrcLine ParseIrcLine(string rawLine)
		{
			var line = rawLine;

			// If the line starts with a colon, it contains information about the sender.
			var hasSender = line.StartsWith(":");

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
			var splitLine = lineWithoutFinalArg.Split(' ').ToList<string>();

			var command = (splitLine.Count >= 1 ? splitLine[0] : null);

			// Contains all arguments for the IRC command, except for the last argument. Usually contains just one argument.
			var args = splitLine.GetRange(1, splitLine.Count - 1).ToArray();

			return new IrcLine(sender, GetUserFromSender(sender), command, args, finalArg, rawLine);
		}
	}
}
