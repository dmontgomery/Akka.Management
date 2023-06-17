// -----------------------------------------------------------------------
//  <copyright file="ClusterMemberSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Net;
using Akka.Discovery.Zookeeper.Model;
using FluentAssertions;
using Xunit;

// If anything throws InvalidOperationException, then the test failed anyway.
// ReSharper disable PossibleInvalidOperationException

namespace Akka.Discovery.Zookeeper.Tests
{
    public class ClusterMemberSpec
    {
        private const string ServiceName = "FakeService";
        private const string Host = "fake.com";
        private readonly IPAddress _address = IPAddress.Loopback;
        private const int Port = 12345;

        [Fact(DisplayName = "Should create and parse RowKey properly")]
        public void ClusterMemberMemberKeyTest()
        {
            var rowKey = ClusterMember.CreateMemberKey(Host, _address, Port);
            var (host, address, port) = ClusterMember.ParseMemberKey(rowKey);

            host.Should().Be(Host);
            address.Should().Be(_address);
            port.Should().Be(Port);
        }
    }
}