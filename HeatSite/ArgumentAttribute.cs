using System;
using System.Diagnostics;

namespace HeatSite
{
    [AttributeUsage(AttributeTargets.Field)]
    public class ArgumentAttribute : Attribute
    {
        readonly ArgumentType type;
        object defaultValue;
        string helpText;
        string longName;
        string shortName;

        public ArgumentAttribute(ArgumentType type)
        {
            this.type = type;
        }

        public ArgumentType Type
        {
            get { return type; }
        }

        public bool DefaultShortName
        {
            get { return null == shortName; }
        }

        public string ShortName
        {
            get { return shortName; }
            set
            {
                Debug.Assert(value == null || !(this is DefaultArgumentAttribute));
                shortName = value;
            }
        }

        public bool DefaultLongName
        {
            get { return null == longName; }
        }

        public string LongName
        {
            get
            {
                Debug.Assert(!DefaultLongName);
                return longName;
            }
            set
            {
                Debug.Assert(value != "");
                longName = value;
            }
        }

        public object DefaultValue
        {
            get { return defaultValue; }
            set { defaultValue = value; }
        }

        public bool HasDefaultValue
        {
            get { return null != defaultValue; }
        }

        public bool HasHelpText
        {
            get { return null != helpText; }
        }

        public string HelpText
        {
            get { return helpText; }
            set { helpText = value; }
        }
    }
}