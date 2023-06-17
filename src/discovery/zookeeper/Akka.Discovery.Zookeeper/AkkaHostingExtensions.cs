// -----------------------------------------------------------------------
//  <copyright file="ZookeeperDiscoveryExtensions.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Net;
using Akka.Actor;
using Akka.Hosting;

namespace Akka.Discovery.Zookeeper
{
    public static class AkkaHostingExtensions
    {
        /// <summary>
        ///     Adds Akka.Discovery.Zookeeper support to the <see cref="ActorSystem"/>.
        ///     Note that this only adds the discovery plugin, you will still need to add ClusterBootstrap for
        ///     a complete solution.
        /// </summary>
        /// <param name="builder">
        ///     The builder instance being configured.
        /// </param>
        /// <param name="connectionString">
        ///     The connection string used to connect to Zookeeper Table hosting the cluster membership table
        /// </param>
        /// <param name="serviceName">
        ///     The service name assigned to the cluster.
        /// </param>
        /// <param name="publicHostname">
        ///     The public IP/host of this node, usually for akka management. It will be used by other nodes to connect
        ///     and query this node. Defaults to <see cref="Dns"/>
        /// </param>
        /// <param name="publicPort">
        ///     The public port of this node, usually for akka management. It will be used by other nodes to connect
        ///     and query this node. Defaults to 8558
        /// </param>
        /// <returns>
        ///     The same <see cref="AkkaConfigurationBuilder"/> instance originally passed in.
        /// </returns>
        /// <example>
        ///   <code>
        ///     services.AddAkka("mySystem", builder => {
        ///         builder.WithClusterBootstrap(options =>
        ///         {
        ///             options.ContactPointDiscovery.ServiceName = "testService";
        ///             options.ContactPointDiscovery.RequiredContactPointsNr = 1;
        ///         }, autoStart: true)
        ///         builder.WithZookeeperDiscovery("localhost:2181");
        ///     }
        ///   </code>
        /// </example>
        public static AkkaConfigurationBuilder WithZookeeperDiscovery(
            this AkkaConfigurationBuilder builder,
            string connectionString,
            string? serviceName = null,
            string? publicHostname = null,
            int? publicPort = null)
        {
            var options = new AkkaDiscoveryOptions
            {
                ConnectionString = connectionString,
                ServiceName = serviceName,
                HostName = publicHostname,
                Port = publicPort
            };
            return builder.WithZookeeperDiscovery(options);
        }

        /// <summary>
        ///     Adds Akka.Discovery.Zookeeper support to the <see cref="ActorSystem"/>.
        ///     Note that this only adds the discovery plugin, you will still need to add ClusterBootstrap for
        ///     a complete solution.
        /// </summary>
        /// <param name="builder">
        ///     The builder instance being configured.
        /// </param>
        /// <param name="serviceName">
        ///     The service name assigned to the cluster.
        /// </param>
        /// <param name="publicHostname">
        ///     The public IP/host of this node, usually for akka management. It will be used by other nodes to connect
        ///     and query this node. Defaults to <see cref="Dns"/>
        /// </param>
        /// <param name="publicPort">
        ///     The public port of this node, usually for akka management. It will be used by other nodes to connect
        ///     and query this node. Defaults to 8558
        /// </param>
        /// <returns>
        ///     The same <see cref="AkkaConfigurationBuilder"/> instance originally passed in.
        /// </returns>
        /// <example>
        ///   <code>
        ///     services.AddAkka("mySystem", builder => {
        ///         builder.WithClusterBootstrap(options =>
        ///         {
        ///             options.ContactPointDiscovery.ServiceName = "testService";
        ///             options.ContactPointDiscovery.RequiredContactPointsNr = 1;
        ///         }, autoStart: true)
        ///         builder.WithZookeeperDiscovery();
        ///     }
        ///   </code>
        /// </example>
        public static AkkaConfigurationBuilder WithZookeeperDiscovery(
            this AkkaConfigurationBuilder builder,
            string? serviceName = null,
            string? publicHostname = null,
            int? publicPort = null)
        {
            var options = new AkkaDiscoveryOptions
            {
                ServiceName = serviceName,
                HostName = publicHostname,
                Port = publicPort
            };
            return builder.WithZookeeperDiscovery(options);
        }

        /// <summary>
        ///     Adds Akka.Discovery.Zookeeper support to the <see cref="ActorSystem"/>.
        ///     Note that this only adds the discovery plugin, you will still need to add ClusterBootstrap for
        ///     a complete solution.
        /// </summary>
        /// <param name="builder">
        ///     The builder instance being configured.
        /// </param>
        /// <param name="configure">
        ///     An action that modifies an <see cref="AkkaDiscoveryOptions"/> instance, used
        ///     to configure Akka.Discovery.Zookeeper.
        /// </param>
        /// <returns>
        ///     The same <see cref="AkkaConfigurationBuilder"/> instance originally passed in.
        /// </returns>
        /// <example>
        ///   <code>
        ///     services.AddAkka("mySystem", builder => {
        ///         builder.WithClusterBootstrap(options =>
        ///         {
        ///             options.ContactPointDiscovery.ServiceName = "testService";
        ///             options.ContactPointDiscovery.RequiredContactPointsNr = 1;
        ///         }, autoStart: true)
        ///         builder.WithZookeeperDiscovery( options => {
        ///             options.ConnectionString = "localhost:2181"
        ///         });
        ///     }
        ///   </code>
        /// </example>
        public static AkkaConfigurationBuilder WithZookeeperDiscovery(
            this AkkaConfigurationBuilder builder,
            Action<AkkaDiscoveryOptions> configure)
        {
            var setup = new AkkaDiscoveryOptions();
            configure(setup);
            return builder.WithZookeeperDiscovery(setup);
        }

        /// <summary>
        ///     Adds Akka.Discovery.Zookeeper support to the <see cref="ActorSystem"/>.
        ///     Note that this only adds the discovery plugin, you will still need to add ClusterBootstrap for
        ///     a complete solution.
        /// </summary>
        /// <param name="builder">
        ///     The builder instance being configured.
        /// </param>
        /// <param name="options">
        ///     The <see cref="AkkaDiscoveryOptions"/> instance used to configure Akka.Discovery.Zookeeper.
        /// </param>
        /// <returns>
        ///     The same <see cref="AkkaConfigurationBuilder"/> instance originally passed in.
        /// </returns>
        /// <example>
        ///   <code>
        ///     services.AddAkka("mySystem", builder => {
        ///         builder.WithClusterBootstrap(options =>
        ///         {
        ///             options.ContactPointDiscovery.ServiceName = "testService";
        ///             options.ContactPointDiscovery.RequiredContactPointsNr = 1;
        ///         }, autoStart: true)
        ///         builder.WithZookeeperDiscovery( new AkkaDiscoveryOptions {
        ///             ConnectionString = "localhost:2181"
        ///         });
        ///     }
        ///   </code>
        /// </example>
        public static AkkaConfigurationBuilder WithZookeeperDiscovery(
            this AkkaConfigurationBuilder builder,
            AkkaDiscoveryOptions options)
        {
            builder.AddHocon(
                ((Configuration.Config)"akka.discovery.method = zookeeper").WithFallback(ZookeeperServiceDiscovery.DefaultConfig), 
                HoconAddMode.Prepend);
            options.Apply(builder);
            builder.AddHocon(ZookeeperServiceDiscovery.DefaultConfig, HoconAddMode.Append);

            return builder;
        }
    }
}