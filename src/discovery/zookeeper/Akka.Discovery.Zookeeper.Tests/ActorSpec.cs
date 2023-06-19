// -----------------------------------------------------------------------
//  <copyright file="ActorSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Immutable;
using System.Net;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using Akka.Discovery.Zookeeper.Actors;
using Akka.Event;
using Akka.TestKit;
using FluentAssertions;
using FluentAssertions.Extensions;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Discovery.Zookeeper.Tests
{
    public class ActorSpec : TestKit.Xunit2.TestKit, IAsyncLifetime
    {
        private static readonly Configuration.Config Config = ConfigurationFactory.ParseString(@"
akka.loglevel = DEBUG
akka.actor.provider = cluster
akka.remote.dot-netty.tcp.port = 0
");

        private const string ConnectionString = "localhost:2181";
        private const string ServiceName = nameof(ServiceName);
        private const string NodeName = "AkkaDiscoveryClusterMembers";
        private const string Host = "fake.com";
        private readonly IPAddress _address = IPAddress.Loopback;
        private const int FirstPort = 12345;
        private readonly RawZookeeperForTesting _rawClient;
        private readonly ILoggingAdapter _logger;

        private int _lastPort = FirstPort;

        public ActorSpec(ITestOutputHelper helper)
            : base(Config, nameof(ActorSpec), helper)
        {
            _logger = Logging.GetLogger(Sys, nameof(ActorSpec));
            var settings = ZookeeperDiscoverySettings.Empty
                .WithServiceName(ServiceName)
                .WithConnectionString(ConnectionString)
                .WithNodeName(NodeName);
            _rawClient = new RawZookeeperForTesting(ConnectionString, settings.OperationTimeout.Milliseconds, _logger);
        }

        public async Task InitializeAsync()
        {
            // Nothing to do?
        }

        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }

        [Fact(DisplayName = "Lookup should return a list of members")]
        public async Task HeartbeatActorShouldReturn()
        {
            var settings = ZookeeperDiscoverySettings.Empty
                .WithPublicHostName("myhost.com")
                .WithConnectionString(ConnectionString)
                .WithServiceName(ServiceName + DateTime.Now.Ticks)
                .WithNodeName(NodeName);
            
            // Initialize client
            var resolvedHost = ZookeeperDiscoveryGuardian.ParseAndResolveHostName(settings.HostName);
            var actor = Sys.ActorOf(HeartbeatActor.Props(settings, resolvedHost.HostValue, resolvedHost.Address,
                settings.Port));

            await WithinAsync(30.Seconds(), async () =>
            {
                while (true)
                {
                    var memberList = await actor.Ask<ImmutableList<ZkMember>>(new Lookup(ServiceName), 
                        settings.OperationTimeout);
                    if (memberList is { Count: > 0 })
                    {
                        break;
                    }
                }
            });
        }
    }
}