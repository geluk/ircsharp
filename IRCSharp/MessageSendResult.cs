using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IRCSharp
{
	public enum MessageSendResult
	{
		Success,
		Failure,
		FloodLimitHit
	}
}
