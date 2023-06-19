// -----------------------------------------------------------------------
//  <copyright file="ZookeeperDiscoverySettings.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Net;
using Akka.Actor;

namespace Akka.Discovery.Zookeeper
{
    public sealed class ZookeeperDiscoverySettings
    {
        public static readonly ZookeeperDiscoverySettings Empty = new ZookeeperDiscoverySettings(
            serviceName: "default",
            hostName: Dns.GetHostName(),
            port: 8558,
            connectionString: "<connection-string>",
            nodeName: "group-membership",
            operationTimeout: TimeSpan.FromSeconds(10),
            retryBackoff: TimeSpan.FromMilliseconds(500),
            maximumRetryBackoff: TimeSpan.FromSeconds(5));
        
        public static ZookeeperDiscoverySettings Create(ActorSystem system)
            => Create(system.Settings.Config);

        public static ZookeeperDiscoverySettings Create(Configuration.Config config)
        {
            var cfg = config.GetConfig("akka.discovery.zookeeper");
            var host = cfg.GetString("public-hostname");
            if (string.IsNullOrWhiteSpace(host))
            {
                host = config.GetString("akka.remote.dot-netty.tcp.public-hostname");
                if (string.IsNullOrWhiteSpace(host))
                    host = Dns.GetHostName();
            }
            
            return new ZookeeperDiscoverySettings(
                serviceName: cfg.GetString("service-name"),
                hostName: host,
                port: cfg.GetInt("public-port"),
                connectionString: cfg.GetString("connection-string"),
                nodeName: cfg.GetString("node-name"),
                operationTimeout: cfg.GetTimeSpan("operation-timeout"),
                retryBackoff: cfg.GetTimeSpan("retry-backoff"),
                maximumRetryBackoff: cfg.GetTimeSpan("max-retry-backoff"));
        }
        
        private ZookeeperDiscoverySettings(
            string serviceName,
            string hostName,
            int port,
            string connectionString,
            string nodeName,
            TimeSpan operationTimeout,
            TimeSpan retryBackoff,
            TimeSpan maximumRetryBackoff)
        {
            if(string.IsNullOrWhiteSpace(hostName))
                throw new ArgumentException(
                    "Must not be empty or whitespace",
                    nameof(hostName));
            
            if(string.IsNullOrWhiteSpace(nodeName))
                throw new ArgumentException(
                    "Must not be empty or whitespace",
                    nameof(nodeName));
            
            if(port < 1 || port > 65535)
                throw new ArgumentException(
                    "Must be greater than zero and less than or equal to 65535",
                    nameof(port));

            if (operationTimeout <= TimeSpan.Zero)
                throw new ArgumentException("Must be greater than zero", nameof(operationTimeout));
            
            if(retryBackoff <= TimeSpan.Zero)
                throw new ArgumentException("Must be greater than zero", nameof(retryBackoff));
            
            if(maximumRetryBackoff < retryBackoff)
                throw new ArgumentException($"Must be greater than {nameof(retryBackoff)}", nameof(maximumRetryBackoff));
            
            ServiceName = serviceName;
            HostName = hostName;
            Port = port;
            ConnectionString = connectionString;
            NodeName = nodeName;
            OperationTimeout = operationTimeout;
            RetryBackoff = retryBackoff;
            MaximumRetryBackoff = maximumRetryBackoff;
        }

        /// <summary>
        /// The service name assigned to the cluster
        /// </summary>
        public string ServiceName { get; }
        /// <summary>
        /// The public facing IP/host of this node
        /// </summary>
        public string HostName { get; }
        /// <summary>
        /// The public open akka management port of this node
        /// </summary>
        public int Port { get; }
        /// <summary>
        /// The connection string to the Zookeeper server or cluster
        /// </summary>
        public string ConnectionString { get; }
        /// <summary>
        /// named Zookeeper node for group membership.  Complete path becomes /Akka.Discovery.Zookeeper/{service-name}/{node-name}
        /// </summary>
        public string NodeName { get; }
        public TimeSpan OperationTimeout { get; }
        public TimeSpan RetryBackoff { get; }
        public TimeSpan MaximumRetryBackoff { get; }

        public override string ToString()
            => "[ZookeeperDiscoverySettings](" +
               $"{nameof(ServiceName)}:{ServiceName}, " +
               $"{nameof(HostName)}:{HostName}, " +
               $"{nameof(Port)}:{Port}, " +
               $"{nameof(ConnectionString)}:{ConnectionString}, " +
               $"{nameof(NodeName)}:{NodeName}, " +
               $"{nameof(OperationTimeout)}:{OperationTimeout}, " +
               $"{nameof(RetryBackoff)}:{RetryBackoff}, " +
               $"{nameof(MaximumRetryBackoff)}:{MaximumRetryBackoff})";
        
        public ZookeeperDiscoverySettings WithServiceName(string serviceName)
            => Copy(serviceName: serviceName);
        
        public ZookeeperDiscoverySettings WithPublicHostName(string hostName)
            => Copy(host: hostName);
        
        public ZookeeperDiscoverySettings WithPublicPort(int port)
            => Copy(port: port);
        
        public ZookeeperDiscoverySettings WithConnectionString(string connectionString)
            => Copy(connectionString: connectionString);
        
        public ZookeeperDiscoverySettings WithNodeName(string nodeName)
            => Copy(nodeName: nodeName);

        public ZookeeperDiscoverySettings WithOperationTimeout(TimeSpan operationTimeout)
            => Copy(operationTimeout: operationTimeout);
        
        public ZookeeperDiscoverySettings WithRetryBackoff(TimeSpan retryBackoff, TimeSpan maximumRetryBackoff)
            => Copy(retryBackoff: retryBackoff, maximumRetryBackoff: maximumRetryBackoff);

        private ZookeeperDiscoverySettings Copy(
            string? serviceName = null,
            string? host = null,
            int? port = null,
            string? connectionString = null,
            string? nodeName = null,
            TimeSpan? operationTimeout = null,
            TimeSpan? retryBackoff = null,
            TimeSpan? maximumRetryBackoff = null)
            => new (
                serviceName: serviceName ?? ServiceName,
                hostName: host ?? HostName,
                port: port ?? Port,
                connectionString: connectionString ?? ConnectionString,
                nodeName: nodeName ?? NodeName,
                operationTimeout: operationTimeout ?? OperationTimeout,
                retryBackoff: retryBackoff ?? RetryBackoff,
                maximumRetryBackoff: maximumRetryBackoff ?? MaximumRetryBackoff);
    }
}