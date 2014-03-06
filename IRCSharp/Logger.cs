using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO;
namespace IRCSharp
{
	public enum LogLevel
	{
		In,
		Out
	}

	public delegate void LogEvent(string message, LogLevel level);

	public static class Logger
	{
		private static string filename = "ircsharp.log";
		private static bool disposed = false;

		static Logger()
		{
			LoadLogFile();
		}
		private static void LoadLogFile()
		{
			textWriter = new StreamWriter(filename, true);
		}

		private static TextWriter textWriter;

		public static void Log(string message, LogLevel level, params object[] format)
		{
			if (format.Length != 0) {
				message = String.Format(message, format);
			}
			StringBuilder lineBuilder = new StringBuilder();
			lineBuilder.Append(DateTime.Now.ToString("[MMM dd - HH:mm:ss.fff]\t"));

			switch (level) {
				case LogLevel.In:
					lineBuilder.Append("+++\t");
					break;
				case LogLevel.Out:
					lineBuilder.Append("---\t");
					break;
			}
			lineBuilder.Append(message);
			WriteToLogFile(lineBuilder);
		}
		private static void WriteToLogFile(StringBuilder lineBuilder)
		{
			if (!disposed) {
				textWriter.WriteLine(lineBuilder.ToString());
				textWriter.Flush();
			}
		}
		public static void ClearLog()
		{
			textWriter.Close();
			File.Delete(filename);
			LoadLogFile();
		}
		public static void Dispose()
		{
			textWriter.Close();
			textWriter.Dispose();
			disposed = true;
		}
	}
}
