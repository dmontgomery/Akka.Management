using System.Collections.Generic;
using System.Threading.Tasks;
using Akka.Event;
using org.apache.zookeeper;

namespace Akka.Discovery.Zookeeper.Tests;

public class ExtendedNode
{
    public string Name { get; set; }
    public DataResult Data { get; set; }
}

public class RawZookeeperForTesting : org.apache.zookeeper.ZooKeeper
{
    /// <summary>
    /// This probably performs like garbage, but it's only used in tests.
    /// </summary>
    /// <param name="client"></param>
    /// <param name="path"></param>
    /// <returns></returns>
    public async Task<List<ExtendedNode>> GetAllChildrenExtendedNodesAsync(string path)
    {
        var children = await getChildrenAsync(path);
        var result = new List<ExtendedNode>();
        foreach (var child in children.Children)
        {
            var data = await getDataAsync($"{path}/{child}");
            result.Add(new ExtendedNode
            {
                Name = child,
                Data = data
            });
        }

        return result;
    }
    
    public RawZookeeperForTesting(string connectString, int sessionTimeout, ILoggingAdapter log) 
        : base(connectString, sessionTimeout, new ConnectionWatcher(log))
    {
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
}