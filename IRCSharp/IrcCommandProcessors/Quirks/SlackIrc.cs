using System.Runtime.Remoting.Channels;
using System.Text.RegularExpressions;
using IRCSharp.IRC;

namespace IRCSharp.IrcCommandProcessors.Quirks
{
	public class SlackIrcProcessor : DataProcessor
	{
		/// <summary>
		/// Keeps replacing <paramref name="pattern"/> with <paramref name="replacement"/> in <paramref name="input"/>
		/// until <paramref name="input"/> no longer matches <paramref name="pattern"/>, then returns the result.
		/// </summary>
		private string RecursiveReplace(string input, string pattern, string replacement)
		{
			string currentMessage;
			string newMessage = input;
			do
			{
				currentMessage = newMessage;
				newMessage = Regex.Replace(currentMessage, pattern, replacement);
			} while (currentMessage != newMessage);
			return newMessage;
		}

		public override string PostProcessOutgoingMessage(string message)
		{
			// Simple workaround for turning IRC formatting into Slack-compatible formatting.
			return message.Replace('\u001d', '_').Replace('\u0002', '*');
		}

		public override IrcMessage ProcessMessage(IrcMessage message)
		{
			// Whenever it encounters a URL, the Slack IRC relay does two things:
			// First, it inserts an HTTP URL after everything it thinks might be formatted like a domain name (or an incorrectly formatted URL),
			// With the inserted URL pointing to the previously mentioned domain.
			// Additionally, it adds some spaces around the URL, just for good measure.
			// Second, while it doesn't add a URL after any correctly formatted URLs, it will, in its infinite wisdom,
			// nonetheless decide to add a space after that URL.
			// Guess who's going to have to fix that?

			// When in doubt, regex. This is ugly as hell, but it mostly works.
			// Start by removing all those superflous spaces. Replace HTTP(S):// with slack-workaround:// to mark a URL as handled, so it won't
			// accidentally be matched again in the next recursion.
			var newMessage = RecursiveReplace(message.Message, @"^(.*?)(https?:\/\/)(.*?)( )(.*)$", "$1slack-workaround://$3$5");
			newMessage = newMessage.Replace("slack-workaround://", "http://");

			// Now let's get rid of those extra URLs.
			newMessage = RecursiveReplace(newMessage, @"^(.*? |)(.+)( https?:\/\/\2)(.*)$", "$1slack-workaround://$2$4");
			newMessage = newMessage.Replace("slack-workaround://", "");

			return new IrcMessage(message.Sender, message.Channel, newMessage, message.Action);
		}
	}
}
