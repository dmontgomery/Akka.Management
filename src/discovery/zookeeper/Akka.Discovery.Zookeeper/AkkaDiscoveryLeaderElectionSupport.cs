using System.Threading.Tasks;
using Akka.Event;
using org.apache.zookeeper;
using org.apache.zookeeper.recipes.leader;

namespace Akka.Discovery.Zookeeper;

/// <summary>
/// This is a simple wrapper around the <see cref="LeaderElectionSupport"/> class to
/// store the entire cluster member key as the node content instead of only storing
/// the hostname of the cluster member (since the hostname value for the default leader
/// election recipe is just a string we don't really need to do this, but it seems
/// a little more self-explanatory this way, perhaps?)
/// </summary>
public class AkkaDiscoveryLeaderElectionSupport
{
    private readonly LeaderElectionSupport _leaderElectionSupport;
    private readonly ILoggingAdapter _logger;
    
    public AkkaDiscoveryLeaderElectionSupport(ZooKeeper zookeeper, string rootNode, string clusterMemberKey, 
        ILoggingAdapter logger)
    {
        _logger = logger;
        _leaderElectionSupport = new LeaderElectionSupport(zookeeper, rootNode, 
            clusterMemberKey);
    }

    public Task Start()
    {
        _logger.Log(LogLevel.InfoLevel, "Starting leader election");
        return _leaderElectionSupport.start();
    }

    public Task Stop()
    {
        _logger.Log(LogLevel.InfoLevel, "Stopping leader election");
        return _leaderElectionSupport.stop();
    }

    public async Task<string> GetClusterLeaderKey()
    {
        var leaderHostName = await _leaderElectionSupport.getLeaderHostName();
        return leaderHostName;
    }
}