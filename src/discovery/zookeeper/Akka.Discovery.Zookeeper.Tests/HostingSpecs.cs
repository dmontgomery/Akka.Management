﻿// -----------------------------------------------------------------------
//  <copyright file="HostingSpecs.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Cluster.Hosting;
using Akka.Hosting;
using Akka.Management;
using Akka.Management.Cluster.Bootstrap;
using Akka.Remote.Hosting;
using FluentAssertions;
using FluentAssertions.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Discovery.Zookeeper.Tests
{
    public class HostingSpecs
    {
        private const string ConnectionString = "localhost:2181";
        
        private async Task<IHost> StartHost(
            Action<AkkaConfigurationBuilder> testSetup,
            LogLevel minimumLogLevel = LogLevel.Debug)
        {
            var host = new HostBuilder()
                .ConfigureLogging((_, builder) =>
                {
                    builder.ClearProviders();
                    builder.AddFilter(level => level >= minimumLogLevel);
                    builder.AddProvider(new XUnitLoggerProvider(_output, minimumLogLevel));
                })
                .ConfigureServices(services =>
                {
                    services.AddAkka("TestSystem", builder =>
                    {
                        builder.ConfigureLoggers(logger =>
                        {
                            logger.LogLevel = Event.LogLevel.DebugLevel;
                            logger.AddLoggerFactory();
                        });
                        builder.WithRemoting(hostname: "localhost", port: 12552);
                        builder.WithClustering();
                        builder.WithAkkaManagement(config =>
                        {
                            config.Http.HostName = "localhost";
                            config.Http.Port = 18558;
                            config.Http.BindHostName = "localhost";
                            config.Http.BindPort = 18558;
                        });
                        builder.WithClusterBootstrap(setup =>
                        {
                            setup.ContactPointDiscovery.ServiceName = "testService";
                            setup.ContactPointDiscovery.RequiredContactPointsNr = 1;
                        });
                        testSetup(builder);
                    });
                }).Build();
        
            await host.StartAsync();
            return host;
        }

        private readonly ITestOutputHelper _output;
        
        public HostingSpecs(ITestOutputHelper output)
        {
            _output = output;
        }

        [Theory(DisplayName = "WithZookeeperDiscovery should work")]
        [MemberData(nameof(StartupFactory))]
        public async Task WithZookeeperDiscoveryTest(
            Action<AkkaConfigurationBuilder> startupAction)
        {
            var tcs = new TaskCompletionSource<Done>();
            using var host = await StartHost(startupAction);

            var system = host.Services.GetRequiredService<ActorSystem>();
            var cluster = Cluster.Cluster.Get(system);
            cluster.RegisterOnMemberUp(() =>
            {
                tcs.SetResult(Done.Instance);
            });

            tcs.Task.Wait(30.Seconds()).Should().BeTrue();
        }

        public static IEnumerable<object[]> StartupFactory()
        {
            var startups = new Action<AkkaConfigurationBuilder>[]
            {
                builder =>
                {
                    builder.WithZookeeperDiscovery(ConnectionString, "testService", "localhost", 18558);
                },
                builder =>
                {
                    builder.WithZookeeperDiscovery(setup =>
                    {
                        setup.ConnectionString = ConnectionString;
                        setup.ServiceName = "testService";
                        setup.HostName = "localhost";
                        setup.Port = 18558;
                    });
                },
                builder =>
                {
                    var setup = new AkkaDiscoveryOptions
                    {
                        ConnectionString = ConnectionString,
                        ServiceName = "testService",
                        HostName = "localhost",
                        Port = 18558
                    };
                    builder.WithZookeeperDiscovery(setup);
                }
            };
            
            foreach (var startup in startups)
            {
                yield return new object[]
                {
                    startup
                };
            }
        }
    }
}