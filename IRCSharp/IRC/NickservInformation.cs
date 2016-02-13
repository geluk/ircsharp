using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IRCSharp.IRC
{
	public class NickservInformation
	{
		public string Nickname { get; set; }
		public string AccountName { get; set; }
		public DateTime Registered { get; set; }
		public string EntityID { get; set; }
		public string LastAddress { get; set; }
		public DateTime LastSeen { get; set; }
		public string Flags { get; set; }

		public NickservInformation() { }

		public NickservInformation(string nickname, string accoutnName, DateTime registered, string entityId, string lastAddress, DateTime lastSeen, string flags)
		{
			Nickname = nickname;
			AccountName = accoutnName;
			Registered = registered;
			EntityID = entityId;
			LastAddress = lastAddress;
			LastSeen = lastSeen;
			Flags = flags;
		}
	}
}
