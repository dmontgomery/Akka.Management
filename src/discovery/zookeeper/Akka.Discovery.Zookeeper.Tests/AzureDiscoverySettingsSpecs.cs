// -----------------------------------------------------------------------
//  <copyright file="ZookeeperDiscoverySettingsSpecs.cs" company="Akka.NET Project">
//      Copyright (C) 2009-2022 Lightbend Inc. <http://www.lightbend.com>
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Net;
using FluentAssertions;
using FluentAssertions.Extensions;
using Xunit;
using static FluentAssertions.FluentActions;

namespace Akka.Discovery.Zookeeper.Tests
{
    public class ZookeeperDiscoverySettingsSpecs
    {
        [Fact(DisplayName = "Default settings should contain default values")]
        public void DefaultSettingsTest()
        {
            var settings = ZookeeperDiscoverySettings.Create(ZookeeperServiceDiscovery.DefaultConfig);

            var assemblyName = typeof(ZookeeperServiceDiscovery).Assembly.FullName!.Split(',')[0].Trim();
            var config = ZookeeperServiceDiscovery.DefaultConfig.GetConfig("akka.discovery.zookeeper");
            config.GetString("class").Should().Be($"{typeof(ZookeeperServiceDiscovery).Namespace}.{nameof(ZookeeperServiceDiscovery)}, {assemblyName}");
            
            settings.ServiceName.Should().Be("default");
            settings.HostName.Should().Be(Dns.GetHostName());
            settings.Port.Should().Be(8558);
            settings.ConnectionString.Should().Be("<connection-string>");
            settings.NodeName.Should().Be("group-membership");
            settings.OperationTimeout.Should().Be(10.Seconds());
        }

        [Fact(DisplayName = "Empty settings variable and default settings should match")]
        public void EmptySettingsTest()
        {
            var settings = ZookeeperDiscoverySettings.Create(ZookeeperServiceDiscovery.DefaultConfig);
            var empty = ZookeeperDiscoverySettings.Empty;

            empty.ServiceName.Should().Be(settings.ServiceName);
            empty.HostName.Should().Be(settings.HostName);
            empty.Port.Should().Be(settings.Port);
            empty.ConnectionString.Should().Be(settings.ConnectionString);
            empty.NodeName.Should().Be(settings.NodeName);
            empty.OperationTimeout.Should().Be(settings.OperationTimeout);
        }

        [Fact(DisplayName = "Settings override should work properly")]
        public void SettingsWithOverrideTest()
        {
            var uri = new Uri("https://whatever.com");
            var settings = ZookeeperDiscoverySettings.Empty
                .WithServiceName("a")
                .WithPublicHostName("host")
                .WithPublicPort(1234)
                .WithConnectionString("b")
                .WithNodeName("c")
                .WithOperationTimeout(4.Seconds());

            settings.ServiceName.Should().Be("a");
            settings.HostName.Should().Be("host");
            settings.Port.Should().Be(1234);
            settings.ConnectionString.Should().Be("b");
            settings.NodeName.Should().Be("c");
            settings.OperationTimeout.Should().Be(4.Seconds());
        }

        [Fact(DisplayName = "Setup override should work properly")]
        public void SettingsWithSetupOverrideTest()
        {
            var uri = new Uri("https://whatever.com");
            var setup = new ZookeeperDiscoverySetup()
                .WithServiceName("a")
                .WithPublicHostName("host")
                .WithPublicPort(1234)
                .WithConnectionString("b")
                .WithNodeName("c")
                .WithOperationTimeout(4.Seconds());

            var settings = setup.Apply(ZookeeperDiscoverySettings.Empty);

            settings.ServiceName.Should().Be("a");
            settings.HostName.Should().Be("host");
            settings.Port.Should().Be(1234);
            settings.ConnectionString.Should().Be("b");
            settings.NodeName.Should().Be("c");
            settings.OperationTimeout.Should().Be(4.Seconds());
        }

        [Fact(DisplayName = "Settings constructor should throw on invalid values")]
        public void SettingsInvalidValuesTest()
        {
            var settings = ZookeeperDiscoverySettings.Empty;

            Invoking(() => settings.WithPublicHostName(""))
                .Should().ThrowExactly<ArgumentException>().WithMessage("Must not be empty or whitespace*");
            
            Invoking(() => settings.WithPublicPort(0))
                .Should().ThrowExactly<ArgumentException>().WithMessage("Must be greater than zero and less than or equal to 65535*");
            
            Invoking(() => settings.WithPublicPort(65536))
                .Should().ThrowExactly<ArgumentException>().WithMessage("Must be greater than zero and less than or equal to 65535*");
            
            Invoking(() => settings.WithRetryBackoff(TimeSpan.Zero, TimeSpan.FromSeconds(1)))
                .Should().ThrowExactly<ArgumentException>().WithMessage("Must be greater than zero*");
            
            Invoking(() => settings.WithRetryBackoff(TimeSpan.FromSeconds(1), TimeSpan.Zero))
                .Should().ThrowExactly<ArgumentException>().WithMessage("Must be greater than retryBackoff*");
        }
    }
}