using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CsNetLib2;
using CsNetLib2.Transfer;
using IRCSharp.Annotations;
using IRCSharp.IRC;

namespace IRCSharp
{
    public enum DisconnectReason
    {
        PingTimeout,
        Other,
        DisconnectOnRequest
    }

    public class IrcClient
    {
        public event ConnectionEstablishedEvent OnConnectionEstablished; // The client has established a connection with the IRC server
        public event DebugLogEvent OnDebugLog;
        public event DebugLogEvent OnNetLibDebugLog;
        public event RawLineReceiveEvent OnRawLineReceived; // A raw IRC line is received
        public event FormattedLineReceiveEvent OnFormattedLineReceived; // A formatted IRC line has been created
        public event MessageReceiveEvent OnMessageReceived; // A message is received
        public event NickChangeEvent OnNickChange; // Someone changes their nick
        public event NickChangedEvent OnNickChanged; // The client changes their nick
        public event KickEvent OnKick; // Someone is kicked
        public event KickedEvent OnKicked; // The client is kicked
        public event DisconnectedEvent OnDisconnect; // The client disconnects, whether intended or not
        public event QuitEvent OnQuit; // Someone quits from IRC
        public event JoinChannelEvent OnJoinChannel; // Someone joins a channel
        public event PartChannelEvent OnPartChannel; // Someone parts a channel
        public event JoinedChannelEvent OnJoinedChannel; // The client joins a channel
        public event PartedChannelEvent OnPartedChannel; // The client parts a channel
        public event NoticeReceiveEvent OnNoticeReceived; // Client receives a notice from the IRC server
        public event TopicReceiveEvent OnTopicReceived; // Client receives the topic for a channel
        public event TopicSetEvent OnTopicSet; // Client receives the date and time on which a topic was set, and by whom it was set
        public event NamesKnownEvent OnNamesKnown; // Client has received all the names of the users inside a channel
        public event ErrorReceivedEvent OnErrorReceived; // Client receives an error

        public event LocalPortKnownEvent OnLocalPortKnown
        {
            add
            {
                client.OnLocalPortKnown += value;
            }
            remove
            {
                client.OnLocalPortKnown -= value;
            }
        }
        // Maps channel names to IrcChannels
        protected Dictionary<string, IrcChannel> Channels = new Dictionary<string, IrcChannel>();
        private NetLibClient client;

        public const string Version = "2.1";
        private const int pingTimeout = 200;
        public int MaxJoinAttempts = 8;

        public bool ReplyToPings { get; set; }
        public int ChannelCount { get { return Channels.Count; } }
        public int TotalUserCount
        {
            get
            {
                return Channels.Sum(pair => pair.Value.UserCount);
            }
        }

        public bool Connected { get; private set; }
        public string RemoteHost { get; private set; }
        public int RemotePort { get; private set; }
        public string LocalHost { get; private set; }
        public string Nick { get; private set; }
        public string Ident { get; private set; }
        public string RealName { get; private set; }
        public IrcUser Self { get { return new IrcUser(Nick, Ident, LocalHost); } }
        public string Password { get; private set; }

        private bool quitRequested;
        private bool invisible;
        private DateTime lastPing;
        [UsedImplicitly]
        private Timer timer;

        public IrcClient()
        {
            ReplyToPings = true;
            timer = new Timer(OnTimeoutCheck, null, 5000, 5000);
        }

        private void OnTimeoutCheck(object state)
        {
            if (!Connected || lastPing.Year == 1 || !((DateTime.Now - lastPing).TotalSeconds > pingTimeout)) return;
            if (OnDisconnect != null)
            {
                DisconnectWithPingTimeout();
            }
        }

        public void DisconnectWithPingTimeout()
        {
            client.DisconnectWithoutEvent();
            Connected = false;
            OnDisconnect(DisconnectReason.PingTimeout, null);
        }

        private void HookClientEvents()
        {
            client.OnDataAvailable += OnReceiveData;
            client.OnLogEvent += HandleLogEvent;
            client.OnDisconnect += HandleDisconnect;
        }

        private void HandleLogEvent(string message)
        {
            if (OnNetLibDebugLog != null)
            {
                OnNetLibDebugLog(client, message);
            }
        }

        public void Connect(string host, int port, string nick, string ident = null, string realName = "IRCSharp", bool visible = false)
        {
            Connect(
                new ConnectionInfo
                {
                    Host = host,
                    Port = port,
                    Nick = nick,
                    Ident = ident,
                    RealName = realName,
                    Invisible = !visible
                }
            );
        }

        /// <summary>
        /// Connects to an IRC server using the specified parameters.
        /// </summary>
        public void Connect(ConnectionInfo ci)
        {
            Log(this, "Starting IRCSharp version " + Version);
            Log(this, "Using CsNetLib version " + NetLib.Version);

            if (string.IsNullOrWhiteSpace(ci.Host))
                // ReSharper disable NotResolvedInText
                throw new ArgumentException("Settings value is empty or does not exist", "ci.Host");
            if (string.IsNullOrWhiteSpace(ci.Nick))
                throw new ArgumentException("Settings value is empty or does not exist", "ci.Nick");
            if (string.IsNullOrWhiteSpace(ci.RealName))
            {
                throw new ArgumentException("Settings value is empty or does not exist", "ci.RealName");
                // ReSharper restore NotResolvedInText
            }

            RemoteHost = ci.Host;
            RemotePort = ci.Port;
            Nick = ci.Nick;
            Ident = ci.Ident ?? ci.Nick;
            RealName = ci.RealName;
            invisible = ci.Invisible;
            Password = ci.Password;

            client = new NetLibClient(TransferProtocolType.Delimited, Encoding.UTF8);

            if (ci.useTLS)
            {
                ConnectClientTLS(ci.verifyServerCertificate);
            }
            else
            {
                ConnectClient();
            }
            
        }

        private void ConnectClientTLS(bool verifyServerCertificate)
        {
            Log(this, string.Format("Connecting securely to {0}:{1}", RemoteHost, RemotePort));
            HookClientEvents();
            client.ConnectSecure(RemoteHost, RemotePort, true, verifyServerCertificate);

            Authenticate(Nick);

            if (OnConnectionEstablished != null)
                Task.Run(() => OnConnectionEstablished());
        }

        private void ConnectClient()
        {
            Log(this, string.Format("Connecting to {0}:{1}", RemoteHost, RemotePort));
            HookClientEvents();
            client.Connect(RemoteHost, RemotePort);

            Authenticate(Nick);

            if (OnConnectionEstablished != null)
                Task.Run(() => OnConnectionEstablished());
        }

        private void Authenticate(string username)
        {
            if (!string.IsNullOrEmpty(Password))
            {
                SendRaw("PASS " + Password);
            }
            SendRaw("NICK " + username);
            SendRaw("USER " + Ident + " " + (invisible ? 8 : 0) + " * :" + RealName);
            Log(this, "Credentials sent");
            var waitingTime = 0;
            while (!Connected && waitingTime < 10000)
            {
                Thread.Sleep(20);
                waitingTime += 20;
            }
            if (Connected)
            {
                Log(this, "Connection to " + RemoteHost + " established.");
            }
            else
            {
                throw new Exception("Connection timed out, took longer than 10 seconds to connect");
            }
        }

        public bool InChannel(string channel)
        {
            return Channels.ContainsKey(channel);
        }

        public void ChangeNick(string nick)
        {
            SendRaw("NICK :" + nick);
            Nick = nick;
        }

        public void Part(string channel, string reason = null)
        {
            if (reason == null)
            {
                SendRaw("PART " + channel);
            }
            else
            {
                SendRaw(String.Format("PART {0} :{1}", channel, reason));
            }
            Channels.Remove(channel);
        }

        public void Quit(string reason = null)
        {
            if (reason == null)
            {
                SendRaw("QUIT");
            }
            else
            {
                var line = "QUIT :" + reason;
                SendRaw(line);
            }
            quitRequested = true;
            while (Connected)
            {
                Thread.Sleep(1);
            }
        }

        public ClientState GetClientState()
        {
            return new ClientState
            {
                Ident = Ident,
                LocalHost = LocalHost,
                RemoteHost = RemoteHost,
                RemotePort = RemotePort,
                Nick = Nick,
                RealName = RealName,
                Channels = Channels
            };
        }

        private void SetClientState(ClientState state)
        {
            Ident = state.Ident;
            LocalHost = state.LocalHost;
            RemoteHost = state.RemoteHost;
            Nick = state.Nick;
            RealName = state.RealName;
            Channels = state.Channels;
        }



        private void ReplyToPing(string line)
        {
            lastPing = DateTime.Now;
            Connected = true;
            if (ReplyToPings)
            {
                var response = "PONG :" + line.Substring("PING :".Length);
                SendRaw(response);
            }
        }

        private void OnReceiveData(string line, long sender)
        {
            //Logger.Log(line, LogLevel.In);

            if (line.StartsWith("PING"))
            {
                ReplyToPing(line);
                return;
            }
            var linef = IrcProtocolParser.ParseIrcLine(line);
            if (OnRawLineReceived != null)
            {
                OnRawLineReceived(line);
            }
            ProcessIrcLine(linef);
        }

        private void ProcessIrcLine(IrcLine line)
        {
            switch (line.Command)
            {
                case "001":
                    Connected = true;
                    break;
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
                case "ERROR":
                    ProcessError(line);
                    break;
                default:
                    if (OnFormattedLineReceived != null)
                    {
                        OnFormattedLineReceived(line);
                    }
                    break;
            }
        }

        private void ProcessError(IrcLine line)
        {
            if (OnErrorReceived != null)
            {
                OnErrorReceived(line.FinalArgument);
            }
        }

        private void ProcessEndOfNames(IrcLine line)
        {
            if (OnNamesKnown != null)
            {
                OnNamesKnown(Channels[line.Arguments[1]]);
            }
        }

        private void ProcessTopicSet(IrcLine line)
        {
            int seconds;
            int.TryParse(line.Arguments[3], out seconds);
            if (OnTopicSet != null)
            {
                OnTopicSet(line.Arguments[1], IrcProtocolParser.GetUserFromSender(line.Arguments[2]), new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddSeconds(seconds));
            }
        }

        private void ProcessTopic(IrcLine line)
        {
            if (OnTopicReceived != null)
            {
                OnTopicReceived(line.Arguments[1], line.FinalArgument);
            }
        }

        private void ProcessQuit(IrcLine line)
        {
            if (OnQuit != null)
            {
                OnQuit(IrcProtocolParser.GetUserFromSender(line.Sender), line.FinalArgument);
            }
        }

        private void ProcessNotice(IrcLine line)
        {
            if (OnNoticeReceived != null)
            {
                OnNoticeReceived(IrcProtocolParser.GetUserFromSender(line.Sender), line.FinalArgument);
            }
        }

        private void ProcessNameReply(IrcLine line)
        {
            var channelName = line.Arguments[2];
            IrcChannel channel;
            try
            {
                channel = Channels[channelName];
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
            var sender = IrcProtocolParser.GetUserFromSender(line.Sender);
            if (sender.Nick == Nick)
            {
                Channels.Remove(line.Arguments[0]);
                if (OnPartedChannel != null)
                {
                    OnPartedChannel(line.Arguments[0]);
                }
            }
            else
            {
                Channels[line.Arguments[0]].RemoveUser(sender.Nick);
                if (OnPartChannel != null)
                {
                    OnPartChannel(sender, line.Arguments[0]);
                }
            }
        }
        private void ProcessJoin(IrcLine line)
        {
            var sender = IrcProtocolParser.GetUserFromSender(line.Sender);
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

                if (Channels.ContainsKey(line.Arguments[0]))
                {
                    throw new InvalidOperationException("Received a JOIN for " + line.Arguments[0] + " whil already in this channel.");
                }
                Channels.Add(line.Arguments[0], new IrcChannel(line.Arguments[0]));
                if (OnJoinedChannel != null)
                {
                    OnJoinedChannel(line.Arguments[0]);
                }
            }
            else
            {
                Channels[line.Arguments[0]].AddUser(sender.Nick, IrcPermissionLevel.Default);
                if (OnJoinChannel != null)
                {
                    OnJoinChannel(sender, line.Arguments[0]);
                }
            }
        }
        private void ProcessKick(IrcLine line)
        {
            var sender = IrcProtocolParser.GetUserFromSender(line.Sender);
            if (line.Arguments[1].Equals(Nick))
            {
                Channels.Remove(line.Arguments[0]);
                if (OnKicked != null)
                {
                    OnKicked(line.Arguments[0], line.FinalArgument, sender);
                }
            }
            else if (OnKick != null)
            {

                OnKick(line.Arguments[1], line.Arguments[0], line.FinalArgument, sender);
            }
        }
        public IrcChannel[] GetChannels()
        {
            return Channels.Values.ToArray();
        }
        private void ProcessNickChange(IrcLine line)
        {
            if (line.User.Nick == Nick)
            {
                Nick = line.FinalArgument;
                if (OnNickChanged != null)
                {
                    OnNickChanged(line.FinalArgument);
                }
            }
            else
            {
                if (OnNickChange != null)
                {
                    OnNickChange(IrcProtocolParser.GetUserFromSender(line.Sender), line.FinalArgument);
                }
            }
        }
        private void ProcessPm(IrcLine line)
        {
            var parsedMessage = line.FinalArgument;
            var action = IrcProtocolParser.ParseAction(ref parsedMessage);
            var sender = IrcProtocolParser.GetUserFromSender(line.Sender);
            // If it's a private message, the target field will be the client's nick. Otherwise, it will be a channel name.
            var channel = line.Arguments[0] == Nick ? sender.Nick : line.Arguments[0];
            var msg = new IrcMessage(sender, channel, parsedMessage, action);

            if (OnMessageReceived == null) return;
            OnMessageReceived(msg);
        }

        /// <summary>
        /// Sends a raw IRC line to the server.
        /// </summary>
        /// <param name="data"></param>
        public bool SendRaw(string data)
        {
            //Logger.Log(data, LogLevel.Out);
            var result = client.Send(data, 0);
            if (result)
            {
                // When the client sends a message to the IRC server, the IRC server will, in addition to processing this message,
                // also regard this as an indication that the client is still alive. Therefore, if it was about to send a ping to the client,
                // it will not do so, since the client has already indicated that it is alive.
                // For this reason, we must inform the client that (provided that the message is successfully sent) it is still connected to the 
                // IRC server. If this value is not set, the client will disconnect itself automatically because it isn't receiving any pings within
                // the spestring.IsNullOrWhitespace(cified ping timeout.
                lastPing = DateTime.Now;
            }
            return result;
        }

        private void Log(object sender, string message)
        {
            // We use a blocking call here, because it may be important that debug messages arrive in the right order.
            if (OnDebugLog != null)
            {
                OnDebugLog(sender, message);
            }
        }

        /// <summary>
        /// Join a channel.
        /// </summary>
        /// <param name="channelName">The channel to join.</param>
        /// <param name="validate">Should we check if the channel is actually joined?</param>
        /// <param name="attemptNumber">How many times have we tried to join?</param>
        public bool JoinChannel(string channelName, bool validate = true, int attemptNumber = 1)
        {

            channelName = channelName.ToLower();
            Log(this, "Joining " + channelName);
            SendRaw("JOIN :" + channelName);
            Log(this, "Join sent");
            if (!validate) return true;

            const int sleepTime = 50;
            var totalSleepTime = 0;
            while (!Channels.ContainsKey(channelName))
            {
                if (totalSleepTime > 1000)
                {
                    if (attemptNumber == MaxJoinAttempts)
                    {
                        Log(this, string.Format("Maximum number of attempts to join {0} reached, channel not joined.", channelName));
                        return false;
                    }
                    Log(this, string.Format("Attempt to join {0} failed, retrying.", channelName));
                    return JoinChannel(channelName, true, ++attemptNumber);
                }
                Thread.Sleep(sleepTime);
                totalSleepTime += sleepTime;
            }
            return true;
        }

        /// <summary>
        /// Leave a channel.
        /// </summary>
        /// <param name="channelName">The channel to leave.</param>
        /// <param name="reason">The reason for leaving.</param>
        public void LeaveChannel(string channelName, string reason = null)
        {
            if (reason != null)
                SendRaw("PART " + channelName + " :" + reason);
            else
                SendRaw("PART " + channelName);

            Channels.Remove(channelName);
        }
        /// <summary>
        /// Leave multiple channelList.
        /// </summary>
        /// <param name="channelList">The channelList to leave.</param>
        public void LeaveChannel(List<string> channelList)
        {
            var partMessage = "";
            foreach (var channel in channelList)
            {
                channelList.Remove(channel);
                partMessage += "," + channel;
            }
            SendRaw("PART " + partMessage.Substring(1));
        }
        /// <summary>
        /// Sends a message to the target.
        /// </summary>
        /// <param name="target">The channel or user to receive the message.</param>
        /// <param name="message">The message to be sent.</param>
        public bool SendMessage(string target, string message)
        {
            // Make sure any newline characters are stripped from the message.
            // Replace \n with whitespace, otherwise two words might end up directly next to each other, with no space between them.
            // By replacing only \n and not \r, we cover all lines terminated by \n and \r\n. Good enough for our purposes.
            message = message.Replace('\n', ' ').Replace("\r", "");

            if (Connected)
            {
                return SendRaw("PRIVMSG " + target + " :" + message);
            }
            throw new InvalidOperationException("Attempt to send a message while the client is not connected to a server");
        }

        public object GetConnectionInfo()
        {
            return new ConnectionInfo
            {
                Host = RemoteHost,
                Port = RemotePort,
                Nick = Nick,
                Ident = Ident,
                RealName = RealName,
                Invisible = invisible
            };
        }

        public void Disconnect()
        {
            client.Disconnect();
        }

        private void HandleDisconnect(Exception reason)
        {
            if (!Connected)
            {
                Log(this, "Ignoring disconnect event because the client has already disconnected.");
                Log(this, string.Format("Connection lost ({0}: {1}) Attempting to reconnect...", reason.GetType().Name, reason.Message));
                return;
            }
            Connected = false;
            client.DisconnectWithoutEvent();
            if (OnDisconnect == null) return;

            if (quitRequested)
                OnDisconnect(DisconnectReason.DisconnectOnRequest, null);
            else
                OnDisconnect(DisconnectReason.Other, reason);
        }

        public void Attach(ClientState state)
        {
            client = new NetLibClient(TransferProtocolType.Delimited, Encoding.UTF8);
            HookClientEvents();
            client.Connect("localhost", 6667);
            Connected = true;

            SetClientState(state);
        }

        public List<string> GetUsers(string channel)
        {
            return Channels[channel].Users;
        }
    }
}
