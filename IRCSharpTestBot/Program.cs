using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IRCSharp;

namespace IRCSharpTestBot
{
	class Program
	{
		private IrcClient client;

		public Program()
		{
			client = new IrcClient();
			client.OnConnectionEstablished += () =>
			{
				Console.WriteLine("Connection Established");
			};

			client.OnRawLineReceived += (line) =>
			{
				Console.ForegroundColor = ConsoleColor.DarkGray;
				Console.WriteLine("RAW: " + line);
				Console.ResetColor();
			};
			client.OnDebugLog += (message) =>
			{
				Console.ForegroundColor = ConsoleColor.Green;
				Console.WriteLine("DEB: " + message);
				Console.ResetColor();
			};
			/*test.OnFormattedLineReceived += (line) =>
			{
				Console.ForegroundColor = ConsoleColor.Blue;
				Console.WriteLine("RAW: " + line.ToString());
				Console.ResetColor();
			};*/
			client.OnMessageReceived += (message) =>
			{
				Console.ForegroundColor = ConsoleColor.Blue;
				Console.WriteLine("{0} ({3}, {4}) in {1}: {2}", message.Sender.Nick, message.Channel, message.Message, message.Sender.Ident, message.Sender.Hostmask);
				Console.ResetColor();
				if (message.Message.StartsWith("-")) {
					ProcessCommand(message);
				}
			};

			client.Connect("irc.esper.net", 6667, "BaggyBetaBot");
			client.JoinChannel("#baggy");
		}
		private void ProcessCommand(IrcMessage message)
		{
			if(!message.Sender.Ident.Equals("~baggerboo"))
			{
				client.SendMessage(message.Channel, "You are not authorized to use commands.");
			}
			string command = message.Message.Substring(1);
			string[] args = command.Substring(command.IndexOf(' ')+1).Split(' ');
			if (command.StartsWith("join")) {
				client.JoinChannel(args[0]);
			} else if (command.Equals("part")) {
				client.LeaveChannel(message.Channel);
			}
		}

		static void Main(string[] args)
		{
			new Program();
		}

	}
}
