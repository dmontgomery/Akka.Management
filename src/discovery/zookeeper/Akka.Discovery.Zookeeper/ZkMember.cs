// -----------------------------------------------------------------------
//  <copyright file="ZkMember.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Net;
using org.apache.zookeeper.data;

namespace Akka.Discovery.Zookeeper;

public class ZkMember
{
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
    /// The node's data contents.  Most of the time this is just a UTF8 string value with the host name.
    /// </summary>
    public byte[] Data { get; }

    /// <summary>
    /// The node's data contents.  Most of the time this is just a UTF8 string value with the host name.
    /// </summary>
    public string DataAsString => System.Text.Encoding.UTF8.GetString(Data);

    /// <summary>
    /// The node's metadata
    /// </summary>
    public Stat Stat { get; }

    public ZkMember(string name, string path, byte[] data, Stat stat)
    {
        Name = name;
        Path = path;
        Data = data;
        Stat = stat;
        (Host, Address, Port) = ParseMemberKey(DataAsString);
    }
    
    public static string CreateMemberKey(string? host, IPAddress? address, int port)
        => $"{host}-{address?.MapToIPv4()}-{port}";
    
    internal static (string, IPAddress?, int) ParseMemberKey(string clusterMemberKey)
    {
        var parts = clusterMemberKey.Split('-');
        if (parts.Length != 3)
            throw new InvalidOperationException($"Node data needs to be in [{{Host}}-{{Address}}-{{Port}}] format. was: [{clusterMemberKey}]");
        if (!IPAddress.TryParse(parts[1], out var address))
            address = null;
        return (parts[0], address, int.Parse(parts[2]));
    }

}