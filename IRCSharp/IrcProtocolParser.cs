using System;
using System.Diagnostics;
using System.Linq;
using IRCSharp.IRC;

namespace IRCSharp
{
	/// <summary>
	/// Parses IRC command strings into objects.
	/// </summary>
	public static class IrcProtocolParser
	{
		/// <summary>
		/// Generates an IrcUser object from an IRC-formatted username,
		/// like user!ident@host.tld 
		/// </summary>
		/// <param name="sender">The username to be parsed.</param>
		/// <returns></returns>
		public static IrcUser GetUserFromSender(string sender)
		{
			if (sender == null)
			{
				return new IrcUser(null, null, null);
			}
			if (!sender.Contains("!"))
			{
				var nick = sender;
				var ident = String.Empty;
				var hostmask = String.Empty;
				return new IrcUser(nick, ident, hostmask);
			}
			else
			{
				var nick = sender.Substring(0, sender.IndexOf('!'));
				var ident = sender.Substring(sender.IndexOf('!') + 1, sender.IndexOf('@') - sender.IndexOf('!') - 1);
				var hostmask = sender.Substring(sender.IndexOf('@') + 1);
				return new IrcUser(nick, ident, hostmask);
			}
		}

		/// <summary>
		/// Checks whether a message uses the CTCP ACTION protocol to signify an action.
		/// </summary>
		/// <param name="message">The message to be checked. If it is an action, it will also be modified to remove the CTCP Action indicators.</param>
		/// <returns>True if the message is an action, false if it is not.</returns>
		public static bool ParseAction(ref string message)
		{
			const string actionSequence = "\u0001ACTION";
			Debug.Assert(actionSequence.Length == 7);

			if (message.StartsWith(actionSequence) && message.EndsWith("\u0001"))
			{
				message = message.Substring(actionSequence.Length + 1, message.Length - (actionSequence.Length - 2));
				return true;
			}
			else
			{
				return false;
			}
		}

		/// <summary>
		/// Generates an IrcLine object from a raw IRC line.
		/// </summary>
		/// <param name="rawLine">The IRC line to be parsed.</param>
		/// <returns></returns>
		public static IrcLine ParseIrcLine(string rawLine)
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
			if (hasSender)
			{
				sender = line.Substring(0, line.IndexOf(' '));
				line = line.Substring(sender.Length + 1);
			}


			// The line without the final argument, which comes after the first colon that isn't at the start of the line.
			string lineWithoutFinalArg;
			// The final argument
			string finalArg;

			if (line.Contains(':'))
			{
				lineWithoutFinalArg = line.Substring(0, line.IndexOf(':'));
				finalArg = line.Substring(line.IndexOf(':') + 1);
			}
			else
			{
				lineWithoutFinalArg = line;
				finalArg = null;
			}
			lineWithoutFinalArg = lineWithoutFinalArg.TrimEnd();

			// Split the line on spaces
			var splitLine = lineWithoutFinalArg.Split(' ').ToList<string>();

			var command = (splitLine.Count >= 1 ? splitLine[0] : null);

			// Contains all arguments for the IRC command, except for the last argument. Usually contains just one argument.
			var args = splitLine.GetRange(1, splitLine.Count - 1).ToArray();

			if (args.Count(string.IsNullOrWhiteSpace) != 0)
			{
				Debugger.Break();
			}

			return new IrcLine(sender, GetUserFromSender(sender), command, args, finalArg, rawLine);
		}
	}
}
