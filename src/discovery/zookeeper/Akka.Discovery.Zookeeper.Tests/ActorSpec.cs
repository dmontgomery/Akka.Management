// -----------------------------------------------------------------------
//  <copyright file="ActorSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Net;
using System.Threading.Tasks;
using Akka.Configuration;
using Akka.Discovery.Zookeeper.Actors;
using Akka.Discovery.Zookeeper.Model;
using Akka.Event;
using FluentAssertions;
using FluentAssertions.Extensions;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Discovery.Zookeeper.Tests
{
    public class ActorSpec: TestKit.Xunit2.TestKit, IAsyncLifetime
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

        private readonly ClusterMemberZookeeperClient _client;
        private readonly RawZookeeperForTesting _rawClient;

        private int _lastPort = FirstPort;
        
        public ActorSpec(ITestOutputHelper helper)
            : base(Config, nameof(ClusterMemberZookeeperClientSpec), helper)
        {
            var logger = Logging.GetLogger(Sys, nameof(ClusterMemberZookeeperClient));
            var settings = ZookeeperDiscoverySettings.Empty
                .WithServiceName(ServiceName)
                .WithConnectionString(ConnectionString)
                .WithNodeName(NodeName);
            _client = new ClusterMemberZookeeperClient(settings, logger);
            _rawClient = new RawZookeeperForTesting(ConnectionString, settings.OperationTimeout.Milliseconds);
        }
        
        public async Task InitializeAsync()
        {
            // Nothing to do?
        }

        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }

        [Fact(DisplayName = "HeartbeatActor should update ClusterMember entry")]
        public async Task HeartbeatActorShouldUpdate()
        {
            var settings = ZookeeperDiscoverySettings.Empty
                .WithConnectionString(ConnectionString)
                .WithServiceName(ServiceName)
                .WithNodeName(NodeName);

            // Initialize client
            var firstEntry = await _client.GetOrCreateAsync(Host, _address, FirstPort);
            var actor = Sys.ActorOf(HeartbeatActor.Props(settings, _client));

            await WithinAsync(3.Seconds(), async () =>
            {
                await EventFilter.Debug(contains: "Current leader node is")
                    .ExpectOneAsync(() =>
                    {
                        actor.Tell("heartbeat", actor); // Fake a timer message
                        return Task.CompletedTask;
                    });
            });

            _client.GetOrCreateAsync()
            var members = await _rawClient.GetAllChildrenExtendedNodesAsync(_client.FullNodePath);
            members.Count.Should().Be(1);

            var fetched = ClusterMember.FromData(_client.ServiceName, members[0].Name, members[0].Data);
            fetched.LastUpdate.Should().BeAfter(firstEntry.LastUpdate);
        }
    }
}