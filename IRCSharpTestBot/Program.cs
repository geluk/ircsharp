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
			client.OnNickChanged += OnNickChanged;

			client.OnMessageReceived += (message) =>
			{
				Console.ForegroundColor = ConsoleColor.Blue;
				Console.WriteLine("{0} ({3}, {4}) in {1}: {2}", message.Sender.Nick, message.Channel, message.Message, message.Sender.Ident, message.Sender.Hostmask);
				Console.ResetColor();
				if (message.Message.StartsWith("-")) {
					ProcessCommand(message);
				}
			};

			string channel = "#baggy";
			client.Connect("irc.esper.net", 6667, "Baggybot_test");
			
			client.JoinChannel(channel);

			StringBuilder sb = new StringBuilder();
			for (int i = 0; i < 16; i++) {
				sb.Append(GetColor(i % 99));
				sb.Append("=");
			}
			client.SendMessage(channel, GetColor(12) + sb.ToString());

			sb = new StringBuilder();
			sb.Append("test ");
			sb.Append(GetItalic());
			sb.Append("test ");
			sb.Append(GetUnderlined());
			sb.Append("test ");
			sb.Append(GetBold());
			sb.Append("test ");


			client.SendMessage(channel, GetColor(12) + sb.ToString());
			}

		string GetBold()
		{
			return "\x02";
		}

		string GetUnderlined()
		{
			return "\x1F";
		}
		string GetItalic()
		{
			return "\x09";
		}

		string GetColor(int color)
		{
			return ((char)((byte)3)).ToString() + color;
		}

		void OnNickChanged(IrcUser user, string newNick)
		{
			Console.ForegroundColor = ConsoleColor.Red;
			Console.WriteLine(user.Nick + " changed nick to " + newNick);
			Console.ResetColor();
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
			} else if (command.StartsWith("part")) {
				client.LeaveChannel(message.Channel);
			} else if (command.StartsWith("nick")) {
				client.ChangeNick(args[0]);
			}
			Console.WriteLine("Invalid Command");
		}

		static void Main(string[] args)
		{
			new Program();
		}

	}
}
