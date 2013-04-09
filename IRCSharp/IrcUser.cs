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
	}
}
