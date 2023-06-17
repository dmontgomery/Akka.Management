using System.Collections.Generic;
using System.Threading.Tasks;
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
    
    public RawZookeeperForTesting(string connectString, int sessionTimeout) 
        : base(connectString, sessionTimeout, new ConnectionWatcher())
    {
    }
    
    private sealed class ConnectionWatcher : Watcher
    {
        private readonly TaskCompletionSource<UnusedClass> _tcs = new();
        public Task WaitForConnectionAsync() => _tcs.Task;

        public override Task process(WatchedEvent @event)
        {
            if (@event.getState() is Event.KeeperState.SyncConnected)
                _tcs.TrySetResult(new UnusedClass());

            return Task.CompletedTask;
        }
    }
        
    private class UnusedClass
    {
        // this can be removed after NET 6?
    }
}