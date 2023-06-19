// -----------------------------------------------------------------------
//  <copyright file="ZkMember.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Net;
using org.apache.zookeeper.data;
using Google.Protobuf;

namespace Akka.Discovery.Zookeeper;

public class ZkMember
{
    /// <summary>
    /// The name of the node without path information
    /// </summary>
    /// <example>n_00000101</example>
    public string Name { get; }

    /// <summary>
    /// Fully qualified route to the node
    /// </summary>
    /// <example>/my_company/my_service/my_group/n_00000101</example>
    public string Path { get; }

    /// <summary>
    /// The node's data contents, stored using protobuf
    /// </summary>
    public byte[] Data { get; }

    /// <summary>
    /// The node's metadata
    /// </summary>
    public Stat Stat { get; }
    
    public ZkMemberKey Key { get; }

    public ZkMember(string name, string path, byte[] data, Stat stat)
    {
        Name = name;
        Path = path;
        Data = data;
        Stat = stat;
        Key = new ZkMemberKey(data);
    }

    public static byte[] CreateMemberKey(string? host, IPAddress? address, int port)
    {
        var proto = new ZkMemberProto
        {
            Host = host ?? "",
            Address = address?.MapToIPv4().ToString() ?? "",
            Port = port
        };   
        return proto.ToByteArray();
    }

    internal static (string?, IPAddress?, int) ParseMemberKey(byte[] clusterMemberKey)
    {
        var proto = ZkMemberProto.Parser.ParseFrom(clusterMemberKey);
        var h = !string.IsNullOrWhiteSpace(proto.Host) ? proto.Host : null;
        var a = !string.IsNullOrWhiteSpace(proto.Address) ? IPAddress.Parse(proto.Address) : null;
        var p = proto.Port;
        return (h, a, p);
    }
}

public class ZkMemberKey : IEquatable<ZkMemberKey>
{
    public bool Equals(ZkMemberKey? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return Host == other.Host && Equals(Address, other.Address) && Port == other.Port;
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((ZkMemberKey)obj);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = (Host != null ? Host.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ (Address != null ? Address.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ Port;
            return hashCode;
        }
    }

    /// <summary>
    /// Used by service discovery.  Parsed from the node's data.
    /// </summary>
    public string? Host { get; }

    /// <summary>
    /// /// Used by service discovery.  Parsed from the node's data.
    /// </summary>
    public IPAddress? Address { get; }

    /// <summary>
    /// /// Used by service discovery.  Parsed from the node's data.
    /// </summary>
    public int Port { get; }

    public ZkMemberKey(byte[] data)
    {
        (Host, Address, Port) = ZkMember.ParseMemberKey(data);
    }
}