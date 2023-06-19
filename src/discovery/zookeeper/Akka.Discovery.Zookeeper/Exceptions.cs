// -----------------------------------------------------------------------
//  <copyright file="Exceptions.cs" company="Akka.NET Project">
//      Copyright (C) 2009-2022 Lightbend Inc. <http://www.lightbend.com>
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Akka.Actor;

namespace Akka.Discovery.Zookeeper;

/// <summary>
/// Thrown when <see cref="ZkMembershipClient"/> failed to connect to Zookeeper service
/// </summary>
public sealed class InitializationException : AkkaException
{
    public InitializationException()
    {
    }

    public InitializationException(string message, Exception? cause = null) : base(message, cause)
    {
    }

    public InitializationException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }
}

/// <summary>
/// Failed to perform lookup of zookeeper nodes
/// </summary>
public sealed class MemberLookupException : Exception
{
    public MemberLookupException()
    {
    }

    public MemberLookupException(string message) : base(message)
    {
    }

    public MemberLookupException(string message, Exception inner) : base(message, inner)
    {
    }
}