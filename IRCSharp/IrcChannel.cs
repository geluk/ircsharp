using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IRCSharp
{
	[Serializable]
	public class IrcChannel
	{
		private Dictionary<string, PermissionLevel> userPermissions = new Dictionary<string, PermissionLevel>();

		public string Name
		{
			get;
			private set;
		}

		public int UserCount
		{
			get
			{
				return userPermissions.Count;
			}
		}

		public List<string> Users {
			get
			{
				return userPermissions.Select(pair => GetPrefix(pair.Value) + pair.Key).ToList();
			}
		}

		private string GetPrefix(PermissionLevel pl)
		{
			switch (pl) {
				case PermissionLevel.Default:
					return "";
				case PermissionLevel.Voiced:
					return "+";
				case PermissionLevel.Operator:
					return "@";
			}
			throw new ArgumentException("PermissionLevel must be Default, Voiced or Operator");
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

		internal void AddUser(string name, PermissionLevel pl)
		{
			if (!userPermissions.ContainsKey(name)) {
				userPermissions.Add(name, pl);
			}
		}

		internal void RemoveUser(string name)
		{
			userPermissions.Remove(name);
		}

		internal void SetPermission(string user, PermissionLevel pl)
		{
			if (userPermissions.ContainsKey(user)) {
				userPermissions[user] = pl;
			}
		}
	}
}
