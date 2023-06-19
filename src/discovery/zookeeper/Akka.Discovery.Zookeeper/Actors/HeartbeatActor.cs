// -----------------------------------------------------------------------
//  <copyright file="HeartbeatActor.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Immutable;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Dispatch.SysMsg;
using Akka.Event;
using Akka.Util.Internal;

namespace Akka.Discovery.Zookeeper.Actors
{
    /// <summary>
    /// Creates and maintains a private connection to the Zookeeper instance.  There is no concept of a ZK heartbeat
    /// (maintaining the connection maintains the group membership automatically) but kept the actor name from other
    /// discovery implementations for consistency.
    /// Instantiated as a child of the ZookeeperDiscoveryGuardian actor, only after it initialized properly.
    /// </summary>
    internal sealed class HeartbeatActor : UntypedActor
    {
        public static Props Props(ZookeeperDiscoverySettings settings, string? host, IPAddress? address, int port)
            => Actor.Props.Create(() => new HeartbeatActor(settings, host, address, port)).WithDeploy(Deploy.Local);

        private static int startRetryCount;
        private static readonly Status.Failure DefaultFailure = new Status.Failure(null);

        private readonly string _serviceName;
        private readonly ILoggingAdapter _log;
        private readonly TimeSpan _timeout;
        private readonly CancellationTokenSource _shutdownCts;
        private readonly TimeSpan _backoff;
        private readonly TimeSpan _maxBackoff;
        private readonly ZkMembershipClient _client;

        public HeartbeatActor(ZookeeperDiscoverySettings settings, string? host, IPAddress? address, int port)
        {
            _serviceName = settings.ServiceName;
            _timeout = settings.OperationTimeout;
            _backoff = settings.RetryBackoff;
            _maxBackoff = settings.MaximumRetryBackoff;
            _log = Context.GetLogger();
            _shutdownCts = new CancellationTokenSource();
            //
            var fullPathToMembershipNode = ZkPathHelper.BuildFullPathFromSettingValues(settings.ServiceName, 
                settings.NodeName);
            var memberKeyValue = ZkMember.CreateMemberKey(host, address, port);
            _client = new ZkMembershipClient(settings.ConnectionString, fullPathToMembershipNode, memberKeyValue, 
                _log, (int)settings.OperationTimeout.TotalMilliseconds);
        }

        private sealed class Start
        {
            public static readonly Start Instance = new Start();

            private Start()
            {
            }
        }

        /// <summary>
        /// The Zookeeper client maintains its own state information for group members, but you can perform a manual
        /// refresh if you want
        /// </summary>
        private sealed class ManualRefresh
        {
            public string ServiceName { get; }

            public ManualRefresh(string serviceName)
            {
                ServiceName = serviceName;
            }
        }


        protected override void PreStart()
        {
            if (_log.IsDebugEnabled)
                _log.Debug("Actor started");

            base.PreStart();
            Become(Initializing);

            // Do an actor start backoff retry
            // Calculate backoff
            var backoff = new TimeSpan(_backoff.Ticks * startRetryCount++);
            // Clamp to maximum backoff time
            backoff = backoff.Min(_maxBackoff);

            // Perform backoff delay
            if (backoff > TimeSpan.Zero)
                Task.Delay(backoff, _shutdownCts.Token).PipeTo(Self, success: () => Start.Instance);
            else
                Self.Tell(Start.Instance);
        }

        protected override void PostStop()
        {
            base.PostStop();
            _shutdownCts.Cancel();
            _shutdownCts.Dispose();

            if (_log.IsDebugEnabled)
                _log.Debug("Actor stopped");
        }

        private bool Initializing(object message)
        {
            switch (message)
            {
                case Start _:
                    if (_log.IsDebugEnabled)
                        _log.Debug("Initializing actor");
                    _client.Start().Wait(_shutdownCts.Token);
                    Task.FromResult(Status.Success.Instance).PipeTo(Self);
                    return true;

                case Status.Success _:

                    Become(Running);

                    if (_log.IsDebugEnabled)
                        _log.Debug("Actor initialized");
                    return true;

                case Status.Failure f:
                    if (_log.IsDebugEnabled)
                        _log.Debug(f.Cause, "Failed to initialize, retrying.");

                    return true;

                case Lookup _:
                case ManualRefresh _:
                    Sender.Tell(ImmutableList<ZkMember>.Empty, Self);
                    return true;

                default:
                    return false;
            }
        }

        private bool Running(object message)
        {
            switch (message)
            {
                case Lookup lookup:
                    if (_log.IsDebugEnabled)
                        _log.Debug("Lookup started for service {0}", lookup.ServiceName);
                    // responding to Ask() from discovery guardian
                    if (_client.Members.Count == 0)
                    {
                        if (_log.IsDebugEnabled)
                            _log.Debug("No members found for service {0}.  Forcing internal refresh",
                                lookup.ServiceName);
                        Self.Tell(new ManualRefresh(lookup.ServiceName));
                    }
                    
                    Sender.Tell(_client.Members.ToImmutableList(), Self);
                    return true;
                
                case ManualRefresh refresh:

                    if (refresh.ServiceName != _serviceName)
                    {
                        _log.Error(
                            $"Lookup ServiceName mismatch. Expected: {_serviceName}, received: {refresh.ServiceName}");
                        return true;
                    }

                    if (_log.IsDebugEnabled)
                        _log.Debug("Refresh started for service {0}", refresh.ServiceName);

                    Sender.Tell(_client.FetchCurrentGroupMembers(), Self);
                    return true;

                case Stop _:

                    Task.FromResult(_client.Stop());
                    Become(Stopping);
                    return true;

                default:
                    return false;
            }
        }

        private bool Stopping(object message)
        {
            switch (message)
            {
                // we probably don't care?
                default:
                    if (_log.IsDebugEnabled)
                        _log.Debug("We received this message while stopping: {0}", message);
                    return true;
            }
        }

        protected override void OnReceive(object message)
        {
            switch (message)
            {
                default:
                    _log.Error("We shouldn't be here, received unknown message: {0}", message);
                    Unhandled(message);
                    break;
            }
        }
    }
}