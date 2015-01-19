using System;
using System.Collections.Generic;
using System.Linq;

namespace IRCSharp.IRC
{
	[Serializable]
	public class IrcChannel
	{
		private readonly Dictionary<string, IrcPermissionLevel> userPermissions = new Dictionary<string, IrcPermissionLevel>();

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

		private string GetPrefix(IrcPermissionLevel pl)
		{
			switch (pl) {
				case IrcPermissionLevel.Default:
					return "";
				case IrcPermissionLevel.Voiced:
					return "+";
				case IrcPermissionLevel.Operator:
					return "@";
			}
			throw new ArgumentException("IrcPermissionLevel must be Default, Voiced or Operator");
		}

		/// <param name="name">The name of the channel.</param>
		/// <param name="users">An array of usernames. Operators and voiced users should be prefixed with @ and + respectively.</param>
		public IrcChannel(string name, IEnumerable<string> users)
		{
			Name = name;
			foreach (var user in users) {
				IrcPermissionLevel pl;
				if(user.StartsWith("@"))
				{
					pl = IrcPermissionLevel.Operator;
				} else if (user.StartsWith("+")) {
					pl = IrcPermissionLevel.Voiced;
				} else {
					pl = IrcPermissionLevel.Default;
				}
				userPermissions.Add(user, pl);
			}
		}
		public IrcChannel(string name)
		: this(name, new string[0]) { }

		internal void AddUser(string name, IrcPermissionLevel pl)
		{
			if (!userPermissions.ContainsKey(name)) {
				userPermissions.Add(name, pl);
			}
		}

		internal void RemoveUser(string name)
		{
			userPermissions.Remove(name);
		}

		internal void SetPermission(string user, IrcPermissionLevel pl)
		{
			if (userPermissions.ContainsKey(user)) {
				userPermissions[user] = pl;
			}
		}
	}
}
