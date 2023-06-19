// -----------------------------------------------------------------------
//  <copyright file="ClusterMemberZookeeperClientSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Net;
using System.Threading.Tasks;
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

        private readonly ZkMembershipClient _client;
        private readonly RawZookeeperForTesting _rawClient;
        private readonly ILoggingAdapter _logger;
        private readonly string _fullZkNodePath;

        public ClusterMemberZookeeperClientSpec(ITestOutputHelper helper)
            : base("akka.loglevel = DEBUG", nameof(ClusterMemberZookeeperClientSpec), helper)
        {
            _logger = Logging.GetLogger(Sys, nameof(ClusterMemberZookeeperClientSpec));
            var settings = ZookeeperDiscoverySettings.Empty
                .WithServiceName(ServiceName + DateTime.Now.Ticks)
                .WithConnectionString(ConnectionString)
                .WithNodeName(NodeName);
            _fullZkNodePath = ZkPathHelper.BuildFullPathFromSettingValues(settings.ServiceName, settings.NodeName);
            _client = new ZkMembershipClient(settings.ConnectionString, _fullZkNodePath, 
                ZkMember.CreateMemberKey(Host, _address, FirstPort), _logger,
                (int)settings.OperationTimeout.TotalMilliseconds);
            _rawClient = new RawZookeeperForTesting(ConnectionString, (int)settings.OperationTimeout.TotalMilliseconds, 
                _logger);
        }

        public async Task InitializeAsync()
        {
            await _client.Start();
        }

        public Task DisposeAsync()
        {
            return Task.FromResult(_client.Stop());
        }

        [Fact(DisplayName = "Start/Connect should create an active node")]
        public async Task GetOrCreateInsert()
        {
            // client should know about one child
            _client.Members.Count.Should().Be(1);
            
            // child from list should match self node
            var firstMember = _client.Members[0];
            firstMember.Key.Should().Be(_client.Members[0].Key);
            
            // there should only be one child per raw client
            if (_logger.IsDebugEnabled)
                _logger.Log(LogLevel.DebugLevel, $"Looking for children using raw client");
            var children = await _rawClient.GetAllChildrenExtendedNodesAsync(_fullZkNodePath);
            if (_logger.IsDebugEnabled)
                _logger.Log(LogLevel.DebugLevel, $"There are {children.Count} children");
            children.Count.Should().Be(1);
            
            // fetched raw node should match our 'SelfNode'
            var firstChild = children[0];
            var childPath = $"{_fullZkNodePath}/{firstChild.Name}";
            if (_logger.IsDebugEnabled)
                _logger.Log(LogLevel.DebugLevel, $"Fetched member has key value of {System.Text.Encoding.UTF8.GetString(firstChild.Data.Data)}");
            var member = new ZkMember(firstChild.Name, childPath, firstChild.Data.Data, firstChild.Data.Stat);
            member.Key.Should().Be(_client.SelfNode?.Key);
        }
    }
}