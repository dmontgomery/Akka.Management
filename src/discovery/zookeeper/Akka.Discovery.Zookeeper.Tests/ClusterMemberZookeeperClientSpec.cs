// -----------------------------------------------------------------------
//  <copyright file="ClusterMemberZookeeperClientSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Net;
using System.Threading.Tasks;
using Akka.Discovery.Zookeeper.Model;
using Akka.Event;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Discovery.Zookeeper.Tests
{
    public class ClusterMemberZookeeperClientSpec: TestKit.Xunit2.TestKit, IAsyncLifetime
    {
        private const string ConnectionString = "localhost:2181";
        private const string ServiceName = nameof(ServiceName);
        private const string WrongService = nameof(WrongService);
        private const string NodeName = "AkkaDiscoveryClusterMembers";
        private const string Host = "fake.com";
        private readonly IPAddress _address = IPAddress.Loopback;
        private const int FirstPort = 12345;

        private readonly ClusterMemberZookeeperClient _client;
        private readonly RawZookeeperForTesting _rawClient;
        private readonly ILoggingAdapter _logger;

        private int _lastPort = FirstPort;

        public ClusterMemberZookeeperClientSpec(ITestOutputHelper helper)
            : base("akka.loglevel = DEBUG", nameof(ClusterMemberZookeeperClientSpec), helper)
        {
            _logger = Logging.GetLogger(Sys, nameof(ClusterMemberZookeeperClient));
            var settings = ZookeeperDiscoverySettings.Empty
                .WithServiceName(ServiceName)
                .WithConnectionString(ConnectionString)
                .WithNodeName(NodeName);
            _client = new ClusterMemberZookeeperClient(settings, _logger);
            _rawClient = new RawZookeeperForTesting(ConnectionString, (int)settings.OperationTimeout.TotalMilliseconds);
        }

        public async Task InitializeAsync()
        {
            // nothing to init?
        }

        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }

        [Fact(DisplayName = "GetOrCreateAsync should create an active node")]
        public async Task GetOrCreateInsert()
        {
            // Test will fail here if the client did not create the appropriate node
            var entity = await _client.GetOrCreateAsync(Host, _address, FirstPort);

            // there should only be one child
            if (_logger.IsDebugEnabled)
                _logger.Log(LogLevel.DebugLevel, $"Looking for children");
            var children = await _rawClient.GetAllChildrenExtendedNodesAsync(_client.FullNodePath);
            if (_logger.IsDebugEnabled)
                _logger.Log(LogLevel.DebugLevel, $"There are {children.Count} children");
            children.Count.Should().Be(1);
            var firstChild = children[0];

            // node should match
            var fetchedMember = ClusterMember.FromData(_client.ServiceName, firstChild.Name, firstChild.Data);
            entity.Should().Be(fetchedMember);
            if (_logger.IsDebugEnabled)
                _logger.Log(LogLevel.DebugLevel, $"Fetched member has key value of {fetchedMember.MemberKey}");
        }
    }
}