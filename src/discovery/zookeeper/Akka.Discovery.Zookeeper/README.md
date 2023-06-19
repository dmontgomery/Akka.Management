# Zookeeper Based Discovery

This module can be used as a discovery method for any cluster that has access to a Zookeeper service.

## Configuring Using Akka.Hosting

You can programmatically configure `Akka.Discovery.Zookeeper` using `Akka.Hosting`.

```csharp
using var host = new HostBuilder()
    .ConfigureServices((hostContext, services) =>
    {
        services.AddAkka("actorSystem", (builder, provider) =>
        {
            builder.WithZookeeperDiscovery("your-zookeeper-connection-string", serviceName: "defaultService", publicHostName: "localhost", publicPort: 18558);
        });
    })
    .Build();

await host.RunAsync();
```

## Configuring Using HOCON

You will need to include these HOCON settings in your HOCON configuration:
```
akka.discovery {
  method = zookeeper
  zookeeper {
    # The service name assigned to the cluster.
    service-name = "defaultService"
    
    # The connection string used to connect to Zookeeper Table hosting the cluster membership table
    # MANDATORY FIELD: MUST be provided, else the discovery plugin WILL throw an exception.
    connection-string = "<connection-string>"
  }
}
```

__Notes__
* The `akka.discovery.zookeeper.connection-string` setting is mandatory
* For `Akka.Discovery.Zookeeper` to work with multiple clusters, each cluster will have to have different `akka.discovery.zookeeper.service-name` settings.

## Configuring Using ActorSystemSetup

You can programmatically configure `Akka.Discovery.Zookeeper` using the `ZookeeperDiscoverySetup` class.

```C#
var config = ConfigurationFactory.ParseString(File.ReadAllText("app.conf"));

var bootstrap = BootstrapSetup.Create()
    .WithConfig(config) // load HOCON
    .WithActorRefProvider(ProviderSelection.Cluster.Instance); // launch Akka.Cluster
                
var zookeeperSetup = new ZookeeperDiscoverySetup()
    .WithConnectionString(connectionString);

var actorSystemSetup = bootstrap.And(zookeeperSetup);

var system = ActorSystem.Create("my-system", actorSystemSetup);
```

## Using Discovery Together with Akka.Management and Cluster.Bootstrap
All discovery plugins are designed to work with Cluster.Bootstrap to provide an automated way to form a cluster that is not based on hard-wired seeds configuration.

### Configuring using Akka.Hosting

With Akka.Hosting, you can wire them together like this:
```csharp
using var host = new HostBuilder()
    .ConfigureServices((hostContext, services) =>
    {
        services.AddAkka("actorSystem", (builder, provider) =>
        {
            builder
                // Add Akka.Remote support
                .WithRemoting(hostname: "", port: 4053)
                // Add Akka.Cluster support
                .WithClustering()
                // Add Akka.Management.Cluster.Bootstrap support
                .WithClusterBootstrap()
                // Add Akka.Discovery.Zookeeper support
                .WithZookeeperDiscovery("your-zookeeper-conection-string");
        });
    })
    .Build();

await host.RunAsync();
```

### Configuring using HOCON configuration

Some HOCON configuration is needed to make discovery work with Cluster.Bootstrap:

```text
akka.discovery.method = zookeeper
akka.discovery.zookeeper.connection-string = "localhost:2181"
akka.management.http.routes = {
    cluster-bootstrap = "Akka.Management.Cluster.Bootstrap.ClusterBootstrapProvider, Akka.Management.Cluster.Bootstrap"
}
```

You then start the cluster bootstrapping process by calling:
```C#
await AkkaManagement.Get(system).Start();
await ClusterBootstrap.Get(system).Start();
```

A more complete example:
```C#
var config = ConfigurationFactory
    .ParseString(File.ReadAllText("app.conf"))
    .WithFallback(ClusterBootstrap.DefaultConfiguration())
    .WithFallback(AkkaManagementProvider.DefaultConfiguration());

var bootstrap = BootstrapSetup.Create()
    .WithConfig(config) // load HOCON
    .WithActorRefProvider(ProviderSelection.Cluster.Instance); // launch Akka.Cluster

var zookeeperSetup = new ZookeeperDiscoverySetup()
    .WithConnectionString(connectionString);

var actorSystemSetup = bootstrap.And(zookeeperSetup);

var system = ActorSystem.Create("my-system", actorSystemSetup);

var log = Logging.GetLogger(system, this);

await AkkaManagement.Get(system).Start();
await ClusterBootstrap.Get(system).Start();

var cluster = Cluster.Get(system);
cluster.RegisterOnMemberUp(() => {
  var upMembers = cluster.State.Members
      .Where(m => m.Status == MemberStatus.Up)
      .Select(m => m.Address.ToString());

  log.Info($"Current up members: [{string.Join(", ", upMembers)}]")
});
```

Additional helpful documentation about Zookeeper written for C#:

https://jack-vanlightly.com/blog/2019/2/1/building-a-simple-distributed-system-the-implementation

