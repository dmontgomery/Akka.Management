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
            nodeName: "leader-election",
            ttlHeartbeatInterval: TimeSpan.FromMinutes(1),
            staleTtlThreshold: TimeSpan.Zero,
            pruneInterval: TimeSpan.FromHours(1),
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
                ttlHeartbeatInterval: cfg.GetTimeSpan("ttl-heartbeat-interval"),
                staleTtlThreshold: cfg.GetTimeSpan("stale-ttl-threshold"),
                pruneInterval: cfg.GetTimeSpan("prune-interval"),
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
            TimeSpan ttlHeartbeatInterval,
            TimeSpan staleTtlThreshold,
            TimeSpan pruneInterval,
            TimeSpan operationTimeout,
            TimeSpan retryBackoff,
            TimeSpan maximumRetryBackoff)
        {
            if (ttlHeartbeatInterval <= TimeSpan.Zero)
                throw new ArgumentException("Must be greater than zero", nameof(ttlHeartbeatInterval));
            
            if (pruneInterval <= TimeSpan.Zero)
                throw new ArgumentException("Must be greater than zero", nameof(pruneInterval));
            
            if (staleTtlThreshold != TimeSpan.Zero && staleTtlThreshold < ttlHeartbeatInterval)
                throw new ArgumentException(
                    $"Must be greater than {nameof(ttlHeartbeatInterval)} if set to non zero",
                    nameof(staleTtlThreshold));

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
            TtlHeartbeatInterval = ttlHeartbeatInterval;
            StaleTtlThreshold = staleTtlThreshold;
            PruneInterval = pruneInterval;
            OperationTimeout = operationTimeout;
            RetryBackoff = retryBackoff;
            MaximumRetryBackoff = maximumRetryBackoff;
        }

        public string ServiceName { get; }
        public string HostName { get; }
        public int Port { get; }
        public string ConnectionString { get; }
        public string NodeName { get; }
        public TimeSpan TtlHeartbeatInterval { get; }
        public TimeSpan StaleTtlThreshold { get; }
        public TimeSpan PruneInterval { get; }
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
               $"{nameof(TtlHeartbeatInterval)}:{TtlHeartbeatInterval}, " +
               $"{nameof(StaleTtlThreshold)}:{StaleTtlThreshold}, " +
               $"{nameof(PruneInterval)}:{PruneInterval}, " +
               $"{nameof(OperationTimeout)}:{OperationTimeout}, " +
               $"{nameof(RetryBackoff)}:{RetryBackoff}, " +
               $"{nameof(MaximumRetryBackoff)}:{MaximumRetryBackoff})";
        
        public TimeSpan EffectiveStaleTtlThreshold
            => StaleTtlThreshold == TimeSpan.Zero ? new TimeSpan(TtlHeartbeatInterval.Ticks * 5)  : StaleTtlThreshold;
        
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

        public ZookeeperDiscoverySettings WithTtlHeartbeatInterval(TimeSpan ttlHeartbeatInterval)
            => Copy(ttlHeartbeatInterval: ttlHeartbeatInterval);
        
        public ZookeeperDiscoverySettings WithStaleTtlThreshold(TimeSpan staleTtlThreshold)
            => Copy(staleTtlThreshold: staleTtlThreshold);
        
        public ZookeeperDiscoverySettings WithPruneInterval(TimeSpan pruneInterval)
            => Copy(pruneInterval: pruneInterval);

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
            TimeSpan? pruneInterval = null,
            TimeSpan? staleTtlThreshold = null,
            TimeSpan? ttlHeartbeatInterval = null,
            TimeSpan? operationTimeout = null,
            TimeSpan? retryBackoff = null,
            TimeSpan? maximumRetryBackoff = null)
            => new (
                serviceName: serviceName ?? ServiceName,
                hostName: host ?? HostName,
                port: port ?? Port,
                connectionString: connectionString ?? ConnectionString,
                nodeName: nodeName ?? NodeName,
                ttlHeartbeatInterval: ttlHeartbeatInterval ?? TtlHeartbeatInterval,
                staleTtlThreshold: staleTtlThreshold ?? StaleTtlThreshold,
                pruneInterval: pruneInterval ?? PruneInterval,
                operationTimeout: operationTimeout ?? OperationTimeout,
                retryBackoff: retryBackoff ?? RetryBackoff,
                maximumRetryBackoff: maximumRetryBackoff ?? MaximumRetryBackoff);
    }
}