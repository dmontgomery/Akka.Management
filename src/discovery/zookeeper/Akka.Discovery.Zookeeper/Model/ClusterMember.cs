using System;
using System.Net;
using System.Text;
using org.apache.zookeeper;

namespace Akka.Discovery.Zookeeper.Model;

/// <summary>
/// A member of the named service group
/// </summary>
public class ClusterMember : IEquatable<ClusterMember>
{
    public string ServiceName { get; }
    public string? Host { get; }
    public IPAddress? Address {get;}
    public int Port {get; }
    public DateTime Created {get; }
    public DateTime LastUpdate { get; }
    public string NodeId { get; }
    public string MemberKey => CreateMemberKey(Host, Address, Port);

    public static ClusterMember FromData(string serviceName, string nodeId, DataResult dataResult)
    {
        var creationTimeUtc = DateTimeOffset.FromUnixTimeMilliseconds(dataResult.Stat.getCtime());
        var modifiedTimeUtc = DateTimeOffset.FromUnixTimeMilliseconds(dataResult.Stat.getMtime());
        var parsedBits= ParseMemberKey(Encoding.UTF8.GetString(dataResult.Data));
        return new ClusterMember(serviceName, parsedBits.Item1, parsedBits.Item2, parsedBits.Item3,
            creationTimeUtc.DateTime, modifiedTimeUtc.DateTime, nodeId);
    }
    
    public ClusterMember(string serviceName, string? host, IPAddress? address, int port, DateTime created, DateTime lastUpdate, 
        string nodeId)
    {
        NodeId = nodeId;
        ServiceName = serviceName;
        Host = host;
        Address = address;
        Port = port;
        Created = created;
        LastUpdate = lastUpdate;
    }
    
    internal static string CreateMemberKey(string? host, IPAddress? address, int port)
        => $"{host}-{address?.MapToIPv4()}-{port}";
    
    internal static (string, IPAddress?, int) ParseMemberKey(string clusterMemberKey)
    {
        var parts = clusterMemberKey.Split('-');
        if (parts.Length != 3)
            throw new InvalidOperationException($"ClusterMemberKey needs to be in [{{Host}}-{{Address}}-{{Port}}] format. was: [{clusterMemberKey}]");
        if (!IPAddress.TryParse(parts[1], out var address))
            address = null;
        return (parts[0], address, int.Parse(parts[2]));
    }
    
    public bool Equals(ClusterMember? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return ServiceName == other.ServiceName && Host == other.Host && Equals(Address, other.Address) && Port == other.Port && Created.Equals(other.Created) && LastUpdate.Equals(other.LastUpdate);
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((ClusterMember)obj);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = ServiceName.GetHashCode();
            hashCode = (hashCode * 397) ^ (Host != null ? Host.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ (Address != null ? Address.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ Port;
            hashCode = (hashCode * 397) ^ Created.GetHashCode();
            hashCode = (hashCode * 397) ^ LastUpdate.GetHashCode();
            return hashCode;
        }
    }
}