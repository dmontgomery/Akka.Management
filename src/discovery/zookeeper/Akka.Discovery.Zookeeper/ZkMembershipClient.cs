// -----------------------------------------------------------------------
//  <copyright file="ZkMembershipClient.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------


using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Akka.Event;
using org.apache.zookeeper;

namespace Akka.Discovery.Zookeeper;

/// <summary>
/// A zookeeper client implementation to monitor group membership in real-time. 
/// </summary>
public class ZkMembershipClient : IDisposable
{
    /// <summary>
    /// This is the root node that will be created in Zookeeper.  All members of the group will be children of this node.
    /// </summary>
    private readonly string _rootNode;

    /// <summary>
    /// This is the identifying content regarding this client member.  Typically this would be the host name of this
    /// client, but it doesn't need to be.  For example, you could pass a string of "host_name:ip_address:port_number"
    /// or even a serialized object or protobuf.  The only requirement is that it can be represented as a byte array.
    /// This value is not used for any sort of lookup
    /// </summary>
    private readonly byte[] _nodeData;

    /// <summary>
    /// connection string to the zookeeper server.  Can be a single host like "localhost:2181" or a comma separated list
    /// of hosts like "localhost:2181,localhost:2182,localhost:2183"
    /// </summary>
    private readonly string _connectionString;

    /// <summary>
    /// connection or operation timeout in milliseconds
    /// </summary>
    private readonly int _sessionTimeoutInMilliseconds;

    private ZooKeeper? _zk;
    private readonly ZkPathHelper _helper;

    /// <summary>
    /// When the zookeeper client is closed, any open watches will be triggered.  Since we have a watch that
    /// will attempt to reload our list of child nodes, we need to know if we are intentionally shutting down
    /// if a connection exception is thrown.
    /// </summary>
    private bool _shutdownStarted;

    private ILoggingAdapter _log;

    /// <summary>
    /// Cached list of members.  This should be updated every time the group changes.
    /// If you want to force a refresh, call <see cref="FetchCurrentGroupMembers"/>
    /// </summary>
    public IList<ZkMember> Members { get; private set; }

    /// <summary>
    /// The node that represents this client in the group
    /// </summary>
    public ZkMember SelfNode
    {
        get
        {
            var myNodeContent = System.Text.Encoding.UTF8.GetString(_nodeData);
            return Members.FirstOrDefault(node => node.DataAsString == myNodeContent)
                   ?? throw new InvalidOperationException();
        }
    }

    public string MembershipNodePath => _rootNode;

    /// <summary>
    /// Create a new membership client using a string as the Zookeeper node content
    /// </summary>
    /// <param name="connectionString">For a single ZK node or a comma-separated list of cluster members
    /// <example>zoo1.corp.netfile.com:2181</example>
    /// <example>zoo1:2181,zoo2:2181,zoo3:2181</example></param>
    /// <param name="fullPathToMembershipNode">Complete route to membership node, in a unix file-path format.
    /// This must be configured the same for every member of the same group
    /// <example>/netfile.com/ring-queue/service-discovery/processor-hosts</example></param>
    /// <param name="hostName">Content stored within the individual node for this client instance.  For group
    /// membership, there is not any sort of a requirement for its contents.  Other recipies may require specific
    /// content type or structure.<example>oakwinq05.corp.netfile.com</example></param>
    /// <param name="log"></param>
    /// <param name="sessionTimeoutInMilliseconds">Defaults to 10 seconds</param>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public ZkMembershipClient(string connectionString, string fullPathToMembershipNode,
        string hostName, ILoggingAdapter log, int sessionTimeoutInMilliseconds = 10000) : this(connectionString,
        fullPathToMembershipNode, System.Text.Encoding.UTF8.GetBytes(hostName), log, sessionTimeoutInMilliseconds)
    {
    }

    /// <summary>
    /// Create a new membership client using arbitrary byte data as the Zookeeper node content
    /// </summary>
    /// <param name="connectionString">For a single ZK node or a comma-separated list of cluster members
    /// <example>zoo1.corp.netfile.com:2181</example>
    /// <example>zoo1:2181,zoo2:2181,zoo3:2181</example></param>
    /// <param name="fullPathToMembershipNode">Complete route to membership node, in a unix file-path format.
    /// This must be configured the same for every member of the same group
    /// <example>/netfile.com/ring-queue/service-discovery/processor-hosts</example></param>
    /// <param name="myHostData">Content stored within the individual node for this client instance.  For group
    /// membership, there is not any sort of a requirement for its contents.  Other recipies may require specific
    /// content type or structure.<example>oakwinq05.corp.netfile.com</example></param>
    /// <param name="log"></param>
    /// <param name="sessionTimeoutInMilliseconds">Defaults to 10 seconds</param>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public ZkMembershipClient(string connectionString, string fullPathToMembershipNode,
        byte[] myHostData, ILoggingAdapter log, int sessionTimeoutInMilliseconds = 10000)
    {
        if (string.IsNullOrEmpty(connectionString))
            throw new ArgumentNullException(nameof(connectionString));
        if (string.IsNullOrEmpty(fullPathToMembershipNode))
            throw new ArgumentNullException(nameof(fullPathToMembershipNode));
        if (myHostData is null || myHostData.Length == 0)
            throw new ArgumentNullException(nameof(myHostData),
                "Some form of content must be provided.  Use the caller's host name if nothing else.");
        if (sessionTimeoutInMilliseconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(sessionTimeoutInMilliseconds),
                "Indefinite timeouts are not supported");
        //
        _nodeData = myHostData;
        _log = log;
        _rootNode = fullPathToMembershipNode;
        _helper = new ZkPathHelper(_rootNode);
        this._connectionString = connectionString;
        this._sessionTimeoutInMilliseconds = sessionTimeoutInMilliseconds;
        // empty list to start
        this.Members = new List<ZkMember>();
        _shutdownStarted = false;
    }

    /// <summary>
    /// Refresh the list of group members, and resets the watch.
    /// </summary>
    /// <returns>Current list of members</returns>
    public async Task<List<ZkMember>> FetchCurrentGroupMembers()
    {
        var result = new List<ZkMember>();
        // group watchers are one-time use, so after every event we set a new one
        // it only *looks* recursive
        var watcher = new GroupMembershipChangedWatcher(_log);
        watcher.MembershipChanged += async (sender, args) => { await FetchCurrentGroupMembers(); };
        try
        {
            if (_zk != null)
            {
                var children = await _zk.getChildrenAsync(_rootNode, watcher);
                foreach (var child in children.Children)
                {
                    var childPath = $"{_rootNode}/{child}";
                    var stat = await _zk.existsAsync(childPath);
                    if (stat is null) continue;
                    var data = await _zk.getDataAsync(childPath);
                    var member = new ZkMember(child, childPath, data.Data, stat);
                    result.Add(member);
                }
            }

            // update our current list
            Members = result;
        }
        catch (KeeperException.ConnectionLossException e)
        {
            if (!_shutdownStarted)
            {
                _log.Error(e, "Connection lost, but not in shutdown");
                throw;
            }
        }
        catch (KeeperException.SessionExpiredException e)
        {
            if (!_shutdownStarted)
            {
                _log.Error(e, "Session expired, but not in shutdown");
                throw;
            }
        }

        return result;
    }

    private async Task InternalStop(bool stoppedFromDisposeMethod)
    {
        _shutdownStarted = true;
        if (!stoppedFromDisposeMethod && _log.IsDebugEnabled)
            _log.Debug("Stop called directly");
        if (_zk != null)
        {
            if (_log.IsDebugEnabled)
                _log.Debug("Shutdown started");
            await _zk.closeAsync();
            if (_log.IsDebugEnabled)
                _log.Debug("Zookeeper connection closed");
        }
    }

    /// <summary>
    /// Creates our persistent root node if it does not yet exist and sets a watch on the connection.
    /// Then creates the ephemeral member node for this instance and sets a watch on the group node.
    /// When members join or leave the group, an event is triggered.
    /// </summary>
    public async Task Start()
    {
        var watcher = new ConnectionWatcher(_log);
        _zk = new ZooKeeper(_connectionString, _sessionTimeoutInMilliseconds, watcher);

        await watcher.WaitForConnectionAsync();

        // create persistent nodes if they don't exist
        foreach (var node in _helper.Nodes)
        {
            if (await _zk.existsAsync(node) is null)
            {
                await _zk.createAsync(node, Array.Empty<byte>(), ZooDefs.Ids.OPEN_ACL_UNSAFE,
                    CreateMode.PERSISTENT);
                if (_log.IsDebugEnabled)
                    _log.Debug("Created persistent node: " + node);
            }
        }

        // if the root node does not exist, something bad has happened and we can't continue
        if (await _zk.existsAsync(_rootNode) is null)
            throw new InitializationException("Root node does not exist.  Aborting startup.");

        var createdPath = await _zk.createAsync(_helper.Child("n_"),
            _nodeData,
            ZooDefs.Ids.OPEN_ACL_UNSAFE,
            CreateMode.EPHEMERAL_SEQUENTIAL);
        if (_log.IsDebugEnabled)
            _log.Debug("Created ephemeral node: " + createdPath);

        await FetchCurrentGroupMembers();
    }

    /// <summary>
    /// Closes the Zookeeper connection and terminates watching events
    /// </summary>
    public async Task Stop()
    {
        await InternalStop(false);
    }

    private sealed class ConnectionWatcher : Watcher
    {
        private readonly ILoggingAdapter _log;

        public ConnectionWatcher(ILoggingAdapter log)
        {
            _log = log;
        }

        private readonly TaskCompletionSource<bool> _connectionEstablished = new TaskCompletionSource<bool>();

        public Task WaitForConnectionAsync() => _connectionEstablished.Task;

        public override Task process(WatchedEvent @event)
        {
            if (_log.IsDebugEnabled)
                _log.Debug("Zookeeper connection state change:" + @event.ToString());
            if (@event.getState() == Event.KeeperState.SyncConnected)
                _connectionEstablished.TrySetResult(true);
            return Task.CompletedTask;
        }
    }

    private sealed class GroupMembershipChangedWatcher : Watcher
    {
        private readonly ILoggingAdapter _log;

        public GroupMembershipChangedWatcher(ILoggingAdapter log)
        {
            _log = log;
        }

        private readonly TaskCompletionSource<bool> _membershipChanged = new TaskCompletionSource<bool>();

        public Task WaitForMembershipChangeAsync() => _membershipChanged.Task;

        public override Task process(WatchedEvent @event)
        {
            if (@event.get_Type() == Event.EventType.NodeChildrenChanged
                && @event.getState() == Event.KeeperState.SyncConnected)
            {
                if (_log.IsDebugEnabled)
                    _log.Debug("Zookeeper group membership changed:" + @event);
                // fire event
                var args = new MembershipChangedEventArgs(@event.getPath(), DateTimeOffset.Now);
                OnMembershipChanged(args);
                //
                _membershipChanged.TrySetResult(true);
            }
            else
            {
                if (_log.IsDebugEnabled)
                    _log.Debug("Unhandled event in membership changed:" + @event);
            }

            return Task.CompletedTask;
        }

        private void OnMembershipChanged(MembershipChangedEventArgs e)
        {
            var handler = MembershipChanged;
            handler?.Invoke(this, e);
        }

        public event EventHandler<MembershipChangedEventArgs>? MembershipChanged;
    }

    private class MembershipChangedEventArgs : EventArgs
    {
        public MembershipChangedEventArgs(string path, DateTimeOffset eventTime)
        {
            Path = path;
            EventTime = eventTime;
        }

        public string Path { get; }
        public DateTimeOffset EventTime { get; }
    }

    public void Dispose()
    {
        InternalStop(true).Wait();
    }
}