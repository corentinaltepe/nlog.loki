using System;
using System.Collections.Generic;

namespace NLog.Loki.Model;

internal class LokiLabels : IEquatable<LokiLabels>
{
    private readonly int _hashCode;

    public HashSet<LokiLabel> Labels { get; }

    public LokiLabels(HashSet<LokiLabel> labels)
    {
        Labels = labels;
        unchecked
        {
            var hash = 0;
            foreach(var label in labels)
                hash = (hash * 397) ^ label.GetHashCode();
            _hashCode = hash;
        }
    }

    public bool Equals(LokiLabels other)
    {
        if(ReferenceEquals(null, other))
            return false;
        if(ReferenceEquals(this, other))
            return true;
        return Labels.SetEquals(other.Labels);
    }

    public override bool Equals(object other)
    {
        if(ReferenceEquals(null, other))
            return false;
        if(ReferenceEquals(this, other))
            return true;
        return Equals(other as LokiLabels);
    }

    public override int GetHashCode()
    {
        return _hashCode;
    }

    public static bool operator ==(LokiLabels left, LokiLabels right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(LokiLabels left, LokiLabels right)
    {
        return !Equals(left, right);
    }
}
