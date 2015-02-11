using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using IRCSharp.IRC;

namespace IRCSharp
{
    class InputProcessor
    {
        protected void ProcessIrcLine(IrcLine line)
        {
            switch (line.Command)
            {
                case "PRIVMSG":
                    ProcessPm(line);
                    break;
                case "NOTICE":
                    ProcessNotice(line);
                    break;
                case "NICK":
                    ProcessNickChange(line);
                    break;
                case "KICK":
                    ProcessKick(line);
                    break;
                case "JOIN":
                    ProcessJoin(line);
                    break;
                case "353":
                    ProcessNameReply(line);
                    break;
                case "PART":
                    ProcessPart(line);
                    break;
                case "QUIT":
                    ProcessQuit(line);
                    break;
                case "332":
                    ProcessTopic(line);
                    break;
                case "333":
                    ProcessTopicSet(line);
                    break;
                case "366":
                    ProcessEndOfNames(line);
                    break;
                case "433":
                    Nick = Nick + "_";
                    Authenticate(Nick);
                    break;
                case "ERROR":
                    ProcessError(line);
                    break;
                default:
                    if (OnFormattedLineReceived != null)
                    {
                        Task.Run(() => OnFormattedLineReceived(line));
                    }
                    break;
            }
        }

        private void ProcessError(IrcLine line)
        {
            if (OnErrorReceived != null)
            {
                Task.Run(() => OnErrorReceived(line.FinalArgument));
            }
        }

        private void ProcessEndOfNames(IrcLine line)
        {
            if (OnNamesKnown != null)
            {
                Task.Run(() => OnNamesKnown(channels[line.Arguments[1]]));
            }
        }

        private void ProcessTopicSet(IrcLine line)
        {
            int seconds;
            int.TryParse(line.Arguments[3], out seconds);
            if (OnTopicSet != null)
            {
                Task.Run(() => OnTopicSet(line.Arguments[1], parser.GetUserFromSender(line.Arguments[2]), new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddSeconds(seconds)));
            }
        }

        private void ProcessTopic(IrcLine line)
        {
            if (OnTopicReceived != null)
            {
                Task.Run(() => OnTopicReceived(line.Arguments[1], line.FinalArgument));
            }
        }

        private void ProcessQuit(IrcLine line)
        {
            if (OnQuit != null)
            {
                Task.Run(() => OnQuit(parser.GetUserFromSender(line.Sender), line.FinalArgument));
            }
        }

        private void ProcessNotice(IrcLine line)
        {
            if (OnNoticeReceived != null)
            {
                Task.Run(() => OnNoticeReceived(parser.GetUserFromSender(line.Sender), line.FinalArgument));
            }
        }

        private void ProcessNameReply(IrcLine line)
        {
            var channelName = line.Arguments[2];
            IrcChannel channel;
            try
            {
                channel = channels[channelName];
            }
            catch (KeyNotFoundException)
            {
                Log(this, "Unable to process name reply: channel not found");
                return;
            }
            var users = line.FinalArgument.Split(' ');
            foreach (var user in users)
            {
                if (user.StartsWith("@"))
                {
                    channel.AddUser(user.Substring(1), IrcPermissionLevel.Operator);
                }
                else if (user.StartsWith("+"))
                {
                    channel.AddUser(user.Substring(1), IrcPermissionLevel.Voiced);
                }
                else
                {
                    channel.AddUser(user, IrcPermissionLevel.Default);
                }
            }
        }
        private void ProcessPart(IrcLine line)
        {
            var sender = parser.GetUserFromSender(line.Sender);
            if (sender.Nick == Nick)
            {
                channels.Remove(line.Arguments[0]);
                if (OnPartedChannel != null)
                {
                    Task.Run(() => OnPartedChannel(line.Arguments[0]));
                }
            }
            else
            {
                channels[line.Arguments[0]].RemoveUser(sender.Nick);
                if (OnPartChannel != null)
                {
                    Task.Run(() => OnPartChannel(sender, line.Arguments[0]));
                }
            }
        }
        private void ProcessJoin(IrcLine line)
        {
            var sender = parser.GetUserFromSender(line.Sender);
            if (sender.Nick == Nick)
            {
                if (Ident != sender.Ident)
                {
                    Log(this, string.Format("Warning: Real ident ({0}) differs from requested ident ({1}). Ident field changed according to real ident", sender.Ident, Ident));
                    Ident = sender.Ident;
                }
                if (LocalHost == null)
                {
                    Log(this, "Hostmask detected as " + sender.Hostmask);
                    LocalHost = sender.Hostmask;
                }

                if (channels.ContainsKey(line.Arguments[0]))
                {
                    throw new InvalidOperationException("Received a JOIN for " + line.Arguments[0] + " whil already in this channel.");
                }
                channels.Add(line.Arguments[0], new IrcChannel(line.Arguments[0]));
                if (OnJoinedChannel != null)
                {
                    Task.Run(() => OnJoinedChannel(line.Arguments[0]));
                }
            }
            else
            {
                channels[line.Arguments[0]].AddUser(sender.Nick, IrcPermissionLevel.Default);
                if (OnJoinChannel != null)
                {
                    Task.Run(() => OnJoinChannel(sender, line.Arguments[0]));
                }
            }
        }
        private void ProcessKick(IrcLine line)
        {
            var sender = parser.GetUserFromSender(line.Sender);
            if (line.Arguments[1].Equals(Nick))
            {
                channels.Remove(line.Arguments[0]);
                if (OnKicked != null)
                {
                    Task.Run(() => OnKicked(line.Arguments[0], line.FinalArgument, sender));
                }
            }
            else if (OnKick != null)
            {

                Task.Run(() => OnKick(line.Arguments[1], line.Arguments[0], line.FinalArgument, sender));
            }
        }
        public IrcChannel[] GetChannels()
        {
            return channels.Values.ToArray();
        }
        private void ProcessNickChange(IrcLine line)
        {
            if (line.User.Nick == Nick)
            {
                Nick = line.FinalArgument;
                if (OnNickChanged != null)
                {
                    Task.Run(() => OnNickChanged(line.FinalArgument));
                }
            }
            else
            {
                if (OnNickChange != null)
                {
                    Task.Run(() => OnNickChange(parser.GetUserFromSender(line.Sender), line.FinalArgument));
                }
            }
        }
        private void ProcessPm(IrcLine line)
        {
            const string actionSequence = "\u0001ACTION";

            var action = false;

            if (line.FinalArgument.StartsWith(actionSequence))
            {
                action = true;
                var message = line.FinalArgument;
                message = message.Substring(8, message.Length - 9);
                line.FinalArgument = message;
            }

            if (OnMessageReceived == null) return;

            var sender = parser.GetUserFromSender(line.Sender);
            var channel = line.Arguments[0] == Nick ? sender.Nick : line.Arguments[0];
            var msg = new IrcMessage(sender, channel, line.FinalArgument, action);

            Task.Run(() => OnMessageReceived(msg));
        }
    }
}
