using System;

namespace HeatSite
{
    [AttributeUsage(AttributeTargets.Field)]
    public class DefaultArgumentAttribute : ArgumentAttribute
    {
        public DefaultArgumentAttribute(ArgumentType type)
            : base(type)
        {
        }
    }
}