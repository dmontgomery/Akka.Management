using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using Akka.Discovery.Zookeeper.Actors;
using Akka.Discovery.Zookeeper.Model;
using Akka.Event;

namespace Akka.Discovery.Zookeeper;

public class ZookeeperServiceDiscovery: ServiceDiscovery
{
    public static readonly Configuration.Config DefaultConfig = 
        ConfigurationFactory.FromResource<ZookeeperServiceDiscovery>("Akka.Discovery.Zookeeper.reference.conf");

    private readonly ILoggingAdapter _log;
    private readonly ExtendedActorSystem _system;
    private readonly ZookeeperDiscoverySettings _settings;
    private readonly IActorRef _guardianActor;

    public ZookeeperServiceDiscovery(ExtendedActorSystem system)
    {
        _system = system;
        _log = Logging.GetLogger(system, typeof(ZookeeperServiceDiscovery));
            
        _system.Settings.InjectTopLevelFallback(DefaultConfig);
        _settings = ZookeeperDiscoverySettings.Create(system);
            
        var setup = _system.Settings.Setup.Get<ZookeeperDiscoverySetup>();
        if (setup.HasValue)
            _settings = setup.Value.Apply(_settings);

        _guardianActor = system.SystemActorOf(ZookeeperDiscoveryGuardian.Props(_settings), "zookeeper-discovery-guardian");

        var shutdown = CoordinatedShutdown.Get(system);
        shutdown.AddTask(CoordinatedShutdown.PhaseClusterExiting, "stop-zookeeper-discovery", async () =>
        {
            try
            {
                await _guardianActor.Ask<Done>(StopDiscovery.Instance);
            }
            catch
            {
                _guardianActor.Tell(PoisonPill.Instance);
                // Just ignore any timeout exceptions, if we failed to remove ourself from the member entry list,
                // the entry will be removed in future entry pruning.
            }
                
            if(_log.IsDebugEnabled)
                _log.Debug("Service stopped");
                
            return Done.Instance;
        });
            
        if(_log.IsDebugEnabled)
            _log.Debug("Service started");
    }

    public override async Task<Resolved> Lookup(Lookup lookup, TimeSpan resolveTimeout)
    {
        if(_log.IsDebugEnabled)
            _log.Debug("Starting lookup for service {0}", lookup.ServiceName);

        try
        {
            var members = await _guardianActor.Ask<ImmutableList<ClusterMember>>(lookup, resolveTimeout);

            return new Resolved(
                lookup.ServiceName,
                members.Select(m => new ResolvedTarget(m.Host, m.Port, m.Address)).ToImmutableList());
        }
        catch (Exception e)
        {
            _log.Warning(e, "Failed to perform contact point lookup");
            return new Resolved(lookup.ServiceName);
        }
    }
}