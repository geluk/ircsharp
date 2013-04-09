using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IRCSharp
{
	public class IrcChannel
	{

		private Dictionary<string, PermissionLevel> userPermissions = new Dictionary<string, PermissionLevel>();

		public string Name
		{
			get;
			private set;
		}

		/// <param name="name">The name of the channel.</param>
		/// <param name="users">An array of usernames. Operators and voiced users should be prefixed with @ and + respectively.</param>
		public IrcChannel(string name, string[] users)
		{
			Name = name;
			foreach (string user in users) {
				PermissionLevel pl;
				if(user.StartsWith("@"))
				{
					pl = PermissionLevel.Operator;
				} else if (user.StartsWith("+")) {
					pl = PermissionLevel.Voiced;
				} else {
					pl = PermissionLevel.Default;
				}
				userPermissions.Add(user, pl);
			}
		}
		public IrcChannel(string name)
		: this(name, new string[0]) { }

	}
}
