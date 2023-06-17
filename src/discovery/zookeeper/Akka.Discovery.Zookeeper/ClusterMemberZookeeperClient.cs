// -----------------------------------------------------------------------
//  <copyright file="ClusterMemberZookeeperClient.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

#nullable enable
using System.Collections.Immutable;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Akka.Discovery.Zookeeper.Model;
using Akka.Event;
using org.apache.zookeeper;

namespace Akka.Discovery.Zookeeper
{
    internal class ClusterMemberZookeeperClient
    {
        private readonly ILoggingAdapter _log;
        private readonly ZookeeperClient _client;
        private readonly string _serviceName;
        private ClusterMember? _entity;
        public string FullNodePath { get; }
        public string ServiceName => _serviceName;
        public ClusterMemberZookeeperClient(
            ZookeeperDiscoverySettings settings,
            ILoggingAdapter log)
        {
            _log = log;
            _serviceName = settings.ServiceName;
            var nodePathParts = new string[] { "Akka.Discovery.Zookeeper", settings.ServiceName, settings.NodeName };
            _client = new ZookeeperClient(_serviceName, settings.ConnectionString, 
                (int)settings.OperationTimeout.TotalMilliseconds, nodePathParts, _log);
            FullNodePath = _client.FullPathToLeaderNode;
        }

        /// <summary>
        /// Initialize our connection to Zookeeper
        /// </summary>
        /// <param name="host">The public Akka.Management host name of this node</param>
        /// <param name="address">The public Akka.Management IP address of this node</param>
        /// <param name="port">the public Akka.Management port of this node</param>
        /// <param name="token">CancellationToken to cancel this operation</param>
        /// <returns>The immutable Zookeeper <see cref="ClusterMember"/> entity entry of this node</returns>
        /// <exception cref="KeeperException">The Zookeeper server returned an error</exception>
        /// <exception cref="CreateEntityFailedException">Client failed to insert new entity row, Zookeeper service responded with an error</exception>
        public async ValueTask<ClusterMember> GetOrCreateAsync(
            string? host,
            IPAddress? address,
            int port,
            CancellationToken token = default)
        {
            if (_entity != null)
                return _entity;

            var memberKey = ClusterMember.CreateMemberKey(host, address, port);
            var entry = await GetEntityAsync(memberKey, token);
            if (entry != null)
            {
                _entity = entry;
                if(_log.IsDebugEnabled)
                    _log.Debug($"[{_serviceName}@{_entity.Address}:{_entity.Port}] Found cluster member entry. " +
                               $"Created: [{_entity.Created}], last update: [{_entity.LastUpdate}]");
                return _entity;
            }

            if (_entity is null)
            {
                throw new CreateEntityFailedException(
                    $"[{_serviceName}@{address}:{port}] Failed to create cluster member node");
            }

            return _entity;
        }

        /// <summary>
        /// Query the Zookeeper node for all current cluster members
        /// </summary>
        /// <param name="token">CancellationToken to cancel this operation</param>
        /// <returns>
        /// All currently connected cluster members
        /// </returns>
        /// <exception cref="InitializationException">Failed to initialize the client, could not connect to Zookeeper services</exception>
        /// <exception cref="RequestFailedException">The Zookeeper server returned an error</exception>
        public async Task<ImmutableList<ClusterMember>> GetAllAsync(CancellationToken token = default)
        {
            var queryResult = await _client.GetAllGroupMembersAsync(token);
            
            if(_log.IsDebugEnabled)
                _log.Debug($"[{_entity}] Retrieved {queryResult.Count} entry rows.");
            return queryResult.ToImmutableList();
        }

        /// <summary>
        /// Query the Zookeeper node for the current cluster leader
        /// </summary>
        /// <param name="clusterMemberKey"></param>
        /// <param name="token">CancellationToken to cancel this operation</param>
        /// <returns>
        /// True if this cluster member is the current cluster leader
        /// </returns>
        /// <exception cref="InitializationException">Failed to initialize the client, could not connect to Zookeeper services</exception>
        /// <exception cref="KeeperException">The Zookeeper server returned an error</exception>
        public async Task<bool> CheckLeaderAsync(CancellationToken token = default)
        {
            if (_entity is not null)
                return await _client.CheckLeaderAsync(_entity.MemberKey, token);
            return false;
        }
        
        #region Helper methods

        /// <summary>
        /// Retrieve a single cluster member with key value matching <paramref name="clusterMemberKey"/>
        /// </summary>
        /// <param name="clusterMemberKey">The key</param>
        /// <param name="token"></param>
        /// <returns><see cref="ClusterMember"/> retrieved</returns>
        /// <exception cref="KeeperException">The Zookeeper server returned an error or failed to connect</exception>
        public async Task<ClusterMember?> GetEntityAsync(string clusterMemberKey, CancellationToken token)
        {
            // we don't care if the current node is the leader or not,
            // we're making certain the LeaderElection recipe has been launched
            await _client.CheckLeaderAsync(clusterMemberKey, token);
            
            var queryResult = await _client.GetAllGroupMembersAsync(token);

            // this is similar to ".FirstOrDefault()" Linq function. We're deliberately NOT using Linq because the
            // bcl NuGet package has a very severe backward target framework compatibility problem.
            foreach (var entry in queryResult)
            {
                if (entry.MemberKey == clusterMemberKey)
                {
                    return entry;
                }
            }

            return default;
        }

        #endregion
    }
}