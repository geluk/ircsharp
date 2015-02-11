using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IRCSharp
{
	public struct ConnectionInfo
	{
		public string Host;
		public int Port;
		public string Nick;
		public string Ident;
		public string RealName;
		public bool Invisible;
	}
}
