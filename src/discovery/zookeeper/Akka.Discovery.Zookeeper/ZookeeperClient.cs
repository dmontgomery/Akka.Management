using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Akka.Discovery.Zookeeper.Model;
using Akka.Event;
using org.apache.zookeeper;
using LogLevel = Akka.Event.LogLevel;

namespace Akka.Discovery.Zookeeper;

public class ZookeeperClient : IDisposable
{
    private readonly string _serviceName;
    private readonly string _leaderNode;
    private readonly string[] _nodePathParts;
    private readonly string _connectionString;
    private readonly int _sessionTimeout;

    private ZooKeeper? _rawClient;
    private AkkaDiscoveryLeaderElectionSupport? _leaderElection;
    private readonly ILoggingAdapter _logger;

    public string FullPathToLeaderNode => _leaderNode;

    public ZookeeperClient(string serviceName, string connectionString, int sessionTimeout, string[] nodePathParts,
        ILoggingAdapter logger)
    {
        if (sessionTimeout == 0)
            throw new ArgumentException("Indefinite timeout is not supported", nameof(sessionTimeout));
        _nodePathParts = nodePathParts;
        _leaderNode = BuildNodePathInSteps(_nodePathParts.Length);
        _connectionString = connectionString;
        _sessionTimeout = sessionTimeout;
        _logger = logger;
        _serviceName = serviceName;
    }
    
    private string BuildNodePathInSteps(int step)
    {
        if (step <= 0)
            throw new ArgumentException("Step must be greater than 0", nameof(step));
        var sb = new StringBuilder();
        for (var i = 1; i <= step; i++)
        {
            sb.Append('/');
            sb.Append(_nodePathParts[i - 1]);
        }

        return sb.ToString();
    }

    /// <summary>
    /// This is using the built-in LeaderElection recipe for Zookeeper, but with a small wrapper class to provide
    /// better method naming
    /// </summary>
    /// <param name="clusterMemberKey"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public async Task<bool> CheckLeaderAsync(string clusterMemberKey, CancellationToken token)
    {
        if (_leaderElection is null)
        {
            var watcher = new ConnectionWatcher(_logger);
            _rawClient = new ZooKeeper(_connectionString, _sessionTimeout, watcher);

            watcher.WaitForConnectionAsync().Wait(token);

            for (var i = 1; i <= _nodePathParts.Length; i++)
            {
                var pathPart = BuildNodePathInSteps(i);
                if (_rawClient.existsAsync(pathPart).Result is null)
                {
                    _rawClient.createAsync(pathPart, Array.Empty<byte>(), ZooDefs.Ids.OPEN_ACL_UNSAFE,
                        CreateMode.PERSISTENT).Wait(token);
                    if (_logger.IsDebugEnabled)
                    {
                        _logger.Log(LogLevel.DebugLevel, $"Created node {pathPart} for service {_serviceName}");
                    }
                }
            }

            if (_rawClient.existsAsync(_leaderNode).Result is null)
                throw new InitializationException($"Failed to create leader node '{_leaderNode}' from path parts");

            _leaderElection = new AkkaDiscoveryLeaderElectionSupport(_rawClient, _leaderNode, clusterMemberKey, _logger);
            await _leaderElection.Start();
        }

        var clusterLeaderKey = await _leaderElection!.GetClusterLeaderKey();
        if (_logger.IsDebugEnabled)
        {
            _logger.Log(LogLevel.DebugLevel, $"Current leader node is {clusterLeaderKey} for service {_serviceName}");
        }
        return clusterLeaderKey == clusterMemberKey;
    }

    public void Dispose()
    {
        _leaderElection?.Stop().Wait();

        _rawClient?.closeAsync().Wait();
    }

    public async Task<List<ClusterMember>> GetAllGroupMembersAsync(CancellationToken token = default)
    {
        if (_rawClient is null) return new List<ClusterMember>();
        if (await _rawClient.existsAsync(_leaderNode) is null) return new List<ClusterMember>();
        //
        var result = new List<ClusterMember>();
        var children = await _rawClient.getChildrenAsync(_leaderNode);
        token.ThrowIfCancellationRequested();
        foreach (var child in children.Children)
        {
            var childPath = $"{_leaderNode}/{child}";
            var data = await _rawClient.getDataAsync(childPath);
            token.ThrowIfCancellationRequested();
            result.Add( ClusterMember.FromData(_serviceName, child, data));
        }

        return result;
    }

    private sealed class ConnectionWatcher : Watcher
    {
        private readonly ILoggingAdapter _logger;

        public ConnectionWatcher(ILoggingAdapter logger)
        {
            _logger = logger;
        }
        
        private readonly TaskCompletionSource<bool> _connectionEstablished = new();

        public Task WaitForConnectionAsync() => _connectionEstablished.Task;
        
        public override Task process(WatchedEvent @event)
        {
            if(_logger.IsDebugEnabled)
                _logger.Log(LogLevel.DebugLevel, "Zookeeper connection state change:" + @event.ToString());
            if (@event.getState() == Event.KeeperState.SyncConnected)
                _connectionEstablished.TrySetResult(true);
            return Task.CompletedTask;
        }
    }
}