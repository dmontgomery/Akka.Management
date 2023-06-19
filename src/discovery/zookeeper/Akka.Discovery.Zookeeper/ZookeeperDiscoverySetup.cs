// -----------------------------------------------------------------------
//  <copyright file="ZookeeperDiscoverySettings.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using Akka.Actor.Setup;

namespace Akka.Discovery.Zookeeper
{
    public sealed class ZookeeperDiscoverySetup: Setup
    {
        public string? ServiceName { get; set; }
        public string? HostName { get; set; }
        public int? Port { get; set; }
        public string? ConnectionString { get; set; }
        public string? NodeName { get; set; }
        public TimeSpan? OperationTimeout { get; set; }
        public TimeSpan? RetryBackoff { get; set; }
        public TimeSpan? MaximumRetryBackoff { get; set; }
        
        public ZookeeperDiscoverySetup WithServiceName(string serviceName)
        {
            ServiceName = serviceName;
            return this;
        }
        
        public ZookeeperDiscoverySetup WithPublicHostName(string hostName)
        {
            HostName = hostName;
            return this;
        }
        
        public ZookeeperDiscoverySetup WithPublicPort(int port)
        {
            Port = port;
            return this;
        }
        
        public ZookeeperDiscoverySetup WithConnectionString(string connectionString)
        {
            ConnectionString = connectionString;
            return this;
        }
        
        public ZookeeperDiscoverySetup WithNodeName(string nodeName)
        {
            NodeName = nodeName;
            return this;
        }

        public ZookeeperDiscoverySetup WithOperationTimeout(TimeSpan operationTimeout)
        {
            OperationTimeout = operationTimeout;
            return this;
        }
        
        public ZookeeperDiscoverySetup WithRetryBackoff(TimeSpan retryBackoff, TimeSpan maximumRetryBackoff)
        {
            RetryBackoff = retryBackoff;
            MaximumRetryBackoff = maximumRetryBackoff;
            return this;
        }
        
        public override string ToString()
        {
            var props = new List<string>();
            if(ServiceName != null)
                props.Add($"{nameof(ServiceName)}:{ServiceName}");
            if(HostName != null)
                props.Add($"{nameof(HostName)}:{HostName}");
            if(NodeName != null)
                props.Add($"{nameof(NodeName)}:{NodeName}");
            if(Port != null)
                props.Add($"{nameof(Port)}:{Port}");
            if(ConnectionString != null)
                props.Add($"{nameof(ConnectionString)}:{ConnectionString}");
            if(OperationTimeout != null)
                props.Add($"{nameof(OperationTimeout)}:{OperationTimeout}");
            if(RetryBackoff != null)
                props.Add($"{nameof(RetryBackoff)}:{RetryBackoff}");
            if(MaximumRetryBackoff != null)
                props.Add($"{nameof(MaximumRetryBackoff)}:{MaximumRetryBackoff}");

            return $"[ZookeeperDiscoverySetup]({string.Join(", ", props)})";
        }
        
        public ZookeeperDiscoverySettings Apply(ZookeeperDiscoverySettings setting)
        {
            if (ServiceName != null)
                setting = setting.WithServiceName(ServiceName);
            if (HostName != null)
                setting = setting.WithPublicHostName(HostName);
            if (NodeName != null)
                setting = setting.WithNodeName(NodeName);
            if (Port != null)
                setting = setting.WithPublicPort(Port.Value);
            if (ConnectionString != null)
                setting = setting.WithConnectionString(ConnectionString);
            if (OperationTimeout != null)
                setting = setting.WithOperationTimeout(OperationTimeout.Value);
            if (RetryBackoff != null && MaximumRetryBackoff != null)
                setting = setting.WithRetryBackoff(RetryBackoff.Value, MaximumRetryBackoff.Value);

            return setting;
        }
    }
}