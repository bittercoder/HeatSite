using System;

namespace HeatSite
{
    [Flags]
    public enum ArgumentType
    {
        Required = 1,
        Unique = 2,
        Multiple = 4,
        AtMostOnce = 0,
        LastOccurenceWins = 4,
        MultipleUnique = 6
    }
}