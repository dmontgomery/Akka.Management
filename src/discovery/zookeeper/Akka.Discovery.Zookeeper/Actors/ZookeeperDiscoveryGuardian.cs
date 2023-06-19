// -----------------------------------------------------------------------
//  <copyright file="ZookeeperDiscoveryGuardian.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Immutable;
using System.Diagnostics.Eventing.Reader;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using Akka.Event;
using Akka.Util.Internal;
using Microsoft.Extensions.Hosting;

namespace Akka.Discovery.Zookeeper.Actors
{
    internal sealed class StopDiscovery
    {
        public static readonly StopDiscovery Instance = new StopDiscovery();

        private StopDiscovery()
        {
        }
    }

    internal sealed class DiscoveryStopped
    {
        public DiscoveryStopped(IActorRef replyTo)
        {
            ReplyTo = replyTo;
        }

        public IActorRef ReplyTo { get; }
    }

    internal sealed class DiscoveryStopFailed
    {
        public DiscoveryStopFailed(IActorRef replyTo, Exception cause)
        {
            ReplyTo = replyTo;
            Cause = cause;
        }

        public IActorRef ReplyTo { get; }
        public Exception Cause { get; }
    }

    /// <summary>
    /// The guardian actor that manages the Zookeeper client instance and the management actors.
    /// Instantiated by ZookeeperServiceDiscovery as a system actor and should restart itself on failures.
    /// The actor will only honor a single lookup request at a time, any requests done while it is still processing
    /// a lookup is ignored.
    /// The actor will reply with an empty result if it is still initializing.
    /// </summary>
    internal sealed class ZookeeperDiscoveryGuardian : UntypedActor
    {
        private sealed class Start
        {
            public static readonly Start Instance = new Start();

            private Start()
            {
            }
        }

        public static Props Props(ZookeeperDiscoverySettings settings)
            => Actor.Props.Create(() => new ZookeeperDiscoveryGuardian(settings)).WithDeploy(Deploy.Local);

        private static int startRetryCount;
        private static readonly Status.Failure DefaultFailure = new Status.Failure(null);

        private readonly ILoggingAdapter _log;
        private readonly ZookeeperDiscoverySettings _settings;
        private readonly TimeSpan _timeout;
        private string? _host;
        private IPAddress? _address;
        private readonly int _port;
        private readonly CancellationTokenSource _shutdownCts;
        private readonly TimeSpan _backoff;
        private readonly TimeSpan _maxBackoff;
        private int _retryCount;
        private bool _lookingUp;
        private IActorRef? _requester;
        private IActorRef? _heartbeatActor;

        public ZookeeperDiscoveryGuardian(ZookeeperDiscoverySettings settings)
        {
            _settings = settings;
            _timeout = settings.OperationTimeout;
            _backoff = settings.RetryBackoff;
            _maxBackoff = settings.MaximumRetryBackoff;
            _log = Logging.GetLogger(Context.System, nameof(ZookeeperDiscoveryGuardian));
            _port = settings.Port;
            _shutdownCts = new CancellationTokenSource();
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
                    var parsedHost = ParseAndResolveHostName(_settings.HostName);
                    _address = parsedHost.Address;
                    _host = parsedHost.HostValue;
                    _retryCount = 0;
                    Task.FromResult(Status.Success.Instance).PipeTo(Self);
                    return true;

                case Status.Success _:
                    startRetryCount = 0;
                    _heartbeatActor = Context.System.ActorOf(HeartbeatActor.Props(_settings, _host, _address, _port));

                    Become(Running);

                    if (_log.IsDebugEnabled)
                        _log.Debug("Actor initialized");
                    return true;

                case Status.Failure f:
                    if (_log.IsDebugEnabled)
                        _log.Debug(f.Cause, "Failed to create/retrieve self discovery entry, retrying.");

                    return true;

                case Lookup _:
                    Sender.Tell(ImmutableList<ZkMember>.Empty, Self);
                    return true;

                default:
                    return false;
            }
        }

        public static (string? HostValue, IPAddress? Address) ParseAndResolveHostName(string hostNameSetting)
        {
            if (!IPAddress.TryParse(hostNameSetting, out var address)) return (hostNameSetting, null);
            if (address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any))
                throw new ConfigurationException(
                    $"IPAddress.Any or IPAddress.IPv6Any cannot be used as host address. Was: {hostNameSetting}");

            return (null, address);

        }

        private bool Running(object message)
        {
            switch (message)
            {
                case Lookup lookup:
                    if (_lookingUp)
                    {
                        if (_log.IsDebugEnabled)
                            _log.Debug("Another lookup operation is still underway, ignoring request.");
                        return true;
                    }

                    if (lookup.ServiceName != _settings.ServiceName)
                    {
                        _log.Error(
                            $"Lookup ServiceName mismatch. Expected: {_settings.ServiceName}, received: {lookup.ServiceName}");
                        return true;
                    }

                    _lookingUp = true;
                    _requester = Sender;
                    if (_log.IsDebugEnabled)
                        _log.Debug("Lookup started for service {0}", lookup.ServiceName);

                    Sender.Tell(_heartbeatActor.Ask<ImmutableList<ZkMember>>(lookup, _timeout).Result, Self);
                    
                    _lookingUp = false;
                    return true;

                case StopDiscovery _:
                    foreach (var child in Context.GetChildren())
                        Context.Stop(child);

                    var sender = Sender;

                    Task.FromResult(new DiscoveryStopped(sender)).PipeTo(Self);

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
                case Lookup _:
                    // Ignore lookup messages, we're shutting down
                    Sender.Tell(ImmutableList<ZkMember>.Empty);
                    return true;

                case StopDiscovery _:
                    // Ignore multiple stop messages
                    Sender.Tell(Done.Instance);
                    return true;

                case DiscoveryStopped msg:
                    msg.ReplyTo.Tell(Done.Instance);
                    Context.System.Stop(Self);
                    return true;

                case DiscoveryStopFailed fail:
                    _log.Warning(fail.Cause, "Failed to perform cleanup, node entry has not been removed from storage");
                    fail.ReplyTo.Tell(Done.Instance);
                    Context.System.Stop(Self);
                    return true;

                default:
                    return false;
            }
        }

        protected override void OnReceive(object message)
        {
            throw new NotImplementedException("Should never reach this code");
        }
    }
}