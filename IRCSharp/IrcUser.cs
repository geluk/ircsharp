using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IRCSharp
{
	public struct IrcUser
	{
		public string Nick;
		public string Ident;
		public string Hostmask;
		public IrcUser(string nick, string ident, string hostmask)
		{
			this.Nick = nick;
			this.Ident = ident;
			this.Hostmask = hostmask;
		}
		public override string ToString()
		{
			var sb = new StringBuilder();
			sb.Append(Nick);
			sb.Append(" (");
			sb.Append(Ident);
			sb.Append("@");
			sb.Append(Hostmask);
			sb.Append(")");
			return sb.ToString(); // nickname (ident@hostmask)
		}
	}
}
