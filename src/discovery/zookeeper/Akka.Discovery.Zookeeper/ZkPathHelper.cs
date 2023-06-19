// -----------------------------------------------------------------------
//  <copyright file="ZkPathHelper.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Akka.Discovery.Zookeeper;

public class ZkPathHelper
{
    private readonly string _rootNode;
    private readonly string[] _pathParts;

    public string Child(string childName)
    {
        return _rootNode + "/" + childName;
    }
        
    public IEnumerable<string> Nodes
    {
        get
        {
            var sb = new StringBuilder();
            foreach (var p in _pathParts)
            {
                sb.Append("/");
                sb.Append(p);
                yield return sb.ToString();
            }
        }
    }

    /// <summary>
    /// returns a full path from setting values: /Akka.Discovery.Zookeeper/{service-name}/{node-name}
    /// </summary>
    /// <returns></returns>
    public static string BuildFullPathFromSettingValues(string serviceName, string nodeName)
    {
        return $"/Akka.Discovery.Zookeeper/{serviceName}/{nodeName}";
    }

    public ZkPathHelper(string rootNode)
    {
        if (string.IsNullOrEmpty(rootNode))
            throw new ArgumentNullException(nameof(rootNode));
        this._rootNode = rootNode;
        this._pathParts = FromPathToParts(this._rootNode);
    }
        
    /// <summary>
    /// Creating nodes must be done individually, so we need to split a path into its parts 
    /// </summary>
    /// <param name="fullPath"></param>
    /// <returns></returns>
    private string[] FromPathToParts(string fullPath)
    {
        // for a value of '/my_company/my_service/my_group/n_00000101' we want to return ['my_company', 'my_service', 'my_group', 'n_00000101']
        return fullPath.Split('/').Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
    }
}