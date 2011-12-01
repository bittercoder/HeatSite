using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace HeatSite
{
    public sealed class Parser
    {
        public const string NewLine = "\r\n";
        const int STD_OUTPUT_HANDLE = -11;
        const int spaceBeforeParam = 2;
        readonly Hashtable argumentMap;
        readonly ArrayList arguments;
        readonly Argument defaultArgument;
        readonly ErrorReporter reporter;

        Parser()
        {
        }

        public Parser(Type argumentSpecification, ErrorReporter reporter)
        {
            this.reporter = reporter;
            arguments = new ArrayList();
            argumentMap = new Hashtable();
            FieldInfo[] fields = argumentSpecification.GetFields();
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                if (!field.IsStatic && !field.IsInitOnly && !field.IsLiteral)
                {
                    ArgumentAttribute attribute = GetAttribute(field);
                    if (attribute is DefaultArgumentAttribute)
                    {
                        Debug.Assert(defaultArgument == null);
                        defaultArgument = new Argument(attribute, field, reporter);
                    }
                    else
                    {
                        arguments.Add(new Argument(attribute, field, reporter));
                    }
                }
            }
            foreach (Argument argument in arguments)
            {
                Debug.Assert(!argumentMap.ContainsKey(argument.LongName));
                argumentMap[argument.LongName] = argument;
                if (argument.ExplicitShortName)
                {
                    if (argument.ShortName != null && argument.ShortName.Length > 0)
                    {
                        Debug.Assert(!argumentMap.ContainsKey(argument.ShortName));
                        argumentMap[argument.ShortName] = argument;
                    }
                    else
                    {
                        argument.ClearShortName();
                    }
                }
            }
            foreach (Argument argument in arguments)
            {
                if (!argument.ExplicitShortName)
                {
                    if (argument.ShortName != null && argument.ShortName.Length > 0 && !argumentMap.ContainsKey(argument.ShortName))
                    {
                        argumentMap[argument.ShortName] = argument;
                    }
                    else
                    {
                        argument.ClearShortName();
                    }
                }
            }
        }

        public bool HasDefaultArgument
        {
            get { return defaultArgument != null; }
        }

        public static bool ParseArgumentsWithUsage(string[] arguments, object destination)
        {
            bool result;
            if (ParseHelp(arguments) || !ParseArguments(arguments, destination))
            {
                Console.Write(ArgumentsUsage(destination.GetType()));
                result = false;
            }
            else
            {
                result = true;
            }
            return result;
        }

        public static bool ParseArguments(string[] arguments, object destination)
        {
            return ParseArguments(arguments, destination, Console.Error.WriteLine);
        }

        public static bool ParseArguments(string[] arguments, object destination, ErrorReporter reporter)
        {
            var parser = new Parser(destination.GetType(), reporter);
            return parser.Parse(arguments, destination);
        }

        static void NullErrorReporter(string message)
        {
        }

        public static bool ParseHelp(string[] args)
        {
            var helpParser = new Parser(typeof (HelpArgument), NullErrorReporter);
            var helpArgument = new HelpArgument();
            helpParser.Parse(args, helpArgument);
            return helpArgument.help;
        }

        public static string ArgumentsUsage(Type argumentType)
        {
            int screenWidth = GetConsoleWindowWidth();
            if (screenWidth == 0)
            {
                screenWidth = 80;
            }
            return ArgumentsUsage(argumentType, screenWidth);
        }

        public static string ArgumentsUsage(Type argumentType, int columns)
        {
            return new Parser(argumentType, null).GetUsageString(columns);
        }

        [DllImport("kernel32.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Auto, SetLastError = true)]
        static extern int GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Auto, SetLastError = true)]
        static extern int GetConsoleScreenBufferInfo(int hConsoleOutput, ref CONSOLE_SCREEN_BUFFER_INFO lpConsoleScreenBufferInfo);

        public static int GetConsoleWindowWidth()
        {
            CONSOLE_SCREEN_BUFFER_INFO csbi = default(CONSOLE_SCREEN_BUFFER_INFO);
            int rc = GetConsoleScreenBufferInfo(GetStdHandle(-11), ref csbi);
            return csbi.dwSize.x;
        }

        public static int IndexOf(StringBuilder text, char value, int startIndex)
        {
            int result;
            for (int index = startIndex; index < text.Length; index++)
            {
                if (text[index] == value)
                {
                    result = index;
                    return result;
                }
            }
            result = -1;
            return result;
        }

        public static int LastIndexOf(StringBuilder text, char value, int startIndex)
        {
            int result;
            for (int index = Math.Min(startIndex, text.Length - 1); index >= 0; index--)
            {
                if (text[index] == value)
                {
                    result = index;
                    return result;
                }
            }
            result = -1;
            return result;
        }

        static ArgumentAttribute GetAttribute(FieldInfo field)
        {
            object[] attributes = field.GetCustomAttributes(typeof (ArgumentAttribute), false);
            ArgumentAttribute result;
            if (attributes.Length == 1)
            {
                result = (ArgumentAttribute) attributes[0];
            }
            else
            {
                Debug.Assert(attributes.Length == 0);
                result = null;
            }
            return result;
        }

        void ReportUnrecognizedArgument(string argument)
        {
            reporter(string.Format("Unrecognized command line argument '{0}'", argument));
        }

        bool ParseArgumentList(string[] args, object destination)
        {
            bool hadError = false;
            if (args != null)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    string argument = args[i];
                    if (argument.Length > 0)
                    {
                        char c = argument[0];
                        switch (c)
                        {
                            case '-':
                            case '/':
                                {
                                    int endIndex = argument.IndexOfAny(new[]
                                        {
                                            ':',
                                            '+',
                                            '-'
                                        }, 1);
                                    string option = argument.Substring(1, (endIndex == -1) ? (argument.Length - 1) : (endIndex - 1));
                                    string optionArgument;
                                    if (option.Length + 1 == argument.Length)
                                    {
                                        optionArgument = null;
                                    }
                                    else
                                    {
                                        if (argument.Length > 1 + option.Length && argument[1 + option.Length] == ':')
                                        {
                                            optionArgument = argument.Substring(option.Length + 2);
                                        }
                                        else
                                        {
                                            optionArgument = argument.Substring(option.Length + 1);
                                        }
                                    }
                                    var arg = (Argument) argumentMap[option];
                                    if (arg == null)
                                    {
                                        ReportUnrecognizedArgument(argument);
                                        hadError = true;
                                    }
                                    else
                                    {
                                        hadError |= !arg.SetValue(optionArgument, destination);
                                    }
                                    break;
                                }
                            case '.':
                                {
                                    goto IL_170;
                                }
                            default:
                                {
                                    if (c != '@')
                                    {
                                        goto IL_170;
                                    }
                                    string[] nestedArguments;
                                    hadError |= LexFileArguments(argument.Substring(1), out nestedArguments);
                                    hadError |= ParseArgumentList(nestedArguments, destination);
                                    break;
                                }
                        }
                        goto IL_1A5;
                        IL_170:
                        if (defaultArgument != null)
                        {
                            hadError |= !defaultArgument.SetValue(argument, destination);
                        }
                        else
                        {
                            ReportUnrecognizedArgument(argument);
                            hadError = true;
                        }
                    }
                    IL_1A5:
                    ;
                }
            }
            return hadError;
        }

        public bool Parse(string[] args, object destination)
        {
            bool hadError = ParseArgumentList(args, destination);
            foreach (Argument arg in arguments)
            {
                hadError |= arg.Finish(destination);
            }
            if (defaultArgument != null)
            {
                hadError |= defaultArgument.Finish(destination);
            }
            return !hadError;
        }

        public string GetUsageString(int screenWidth)
        {
            ArgumentHelpStrings[] strings = GetAllHelpStrings();
            int maxParamLen = 0;
            ArgumentHelpStrings[] array = strings;
            for (int i = 0; i < array.Length; i++)
            {
                ArgumentHelpStrings helpString = array[i];
                maxParamLen = Math.Max(maxParamLen, helpString.syntax.Length);
            }
            int idealMinimumHelpTextColumn = maxParamLen + 2;
            screenWidth = Math.Max(screenWidth, 15);
            int helpTextColumn;
            if (screenWidth < idealMinimumHelpTextColumn + 10)
            {
                helpTextColumn = 5;
            }
            else
            {
                helpTextColumn = idealMinimumHelpTextColumn;
            }
            var builder = new StringBuilder();
            array = strings;
            for (int i = 0; i < array.Length; i++)
            {
                ArgumentHelpStrings helpStrings = array[i];
                int syntaxLength = helpStrings.syntax.Length;
                builder.Append(helpStrings.syntax);
                int currentColumn = syntaxLength;
                if (syntaxLength >= helpTextColumn)
                {
                    builder.Append("\n");
                    currentColumn = 0;
                }
                int charsPerLine = screenWidth - helpTextColumn;
                int index = 0;
                while (index < helpStrings.help.Length)
                {
                    builder.Append(' ', helpTextColumn - currentColumn);
                    currentColumn = helpTextColumn;
                    int endIndex = index + charsPerLine;
                    if (endIndex >= helpStrings.help.Length)
                    {
                        endIndex = helpStrings.help.Length;
                    }
                    else
                    {
                        endIndex = helpStrings.help.LastIndexOf(' ', endIndex - 1, Math.Min(endIndex - index, charsPerLine));
                        if (endIndex <= index)
                        {
                            endIndex = index + charsPerLine;
                        }
                    }
                    builder.Append(helpStrings.help, index, endIndex - index);
                    index = endIndex;
                    AddNewLine("\n", builder, ref currentColumn);
                    while (index < helpStrings.help.Length && helpStrings.help[index] == ' ')
                    {
                        index++;
                    }
                }
                if (helpStrings.help.Length == 0)
                {
                    builder.Append("\n");
                }
            }
            return builder.ToString();
        }

        static void AddNewLine(string newLine, StringBuilder builder, ref int currentColumn)
        {
            builder.Append(newLine);
            currentColumn = 0;
        }

        ArgumentHelpStrings[] GetAllHelpStrings()
        {
            var strings = new ArgumentHelpStrings[NumberOfParametersToDisplay()];
            int index = 0;
            foreach (Argument arg in arguments)
            {
                strings[index] = GetHelpStrings(arg);
                index++;
            }
            strings[index++] = new ArgumentHelpStrings("@<file>", "Read response file for more options");
            if (defaultArgument != null)
            {
                strings[index++] = GetHelpStrings(defaultArgument);
            }
            return strings;
        }

        static ArgumentHelpStrings GetHelpStrings(Argument arg)
        {
            return new ArgumentHelpStrings(arg.SyntaxHelp, arg.FullHelpText);
        }

        int NumberOfParametersToDisplay()
        {
            int numberOfParameters = arguments.Count + 1;
            if (HasDefaultArgument)
            {
                numberOfParameters++;
            }
            return numberOfParameters;
        }

        bool LexFileArguments(string fileName, out string[] arguments)
        {
            string args = null;
            bool result;
            try
            {
                using (var file = new FileStream(fileName, FileMode.Open, FileAccess.Read))
                {
                    args = new StreamReader(file).ReadToEnd();
                }
            }
            catch (Exception e)
            {
                reporter(string.Format("Error: Can't open command line argument file '{0}' : '{1}'", fileName, e.Message));
                arguments = null;
                result = false;
                return result;
            }
            bool hadError = false;
            var argArray = new ArrayList();
            var currentArg = new StringBuilder();
            bool inQuotes = false;
            int index = 0;
            try
            {
                while (true)
                {
                    while (char.IsWhiteSpace(args[index]))
                    {
                        index++;
                    }
                    if (args[index] == '#')
                    {
                        index++;
                        while (args[index] != '\n')
                        {
                            index++;
                        }
                    }
                    else
                    {
                        do
                        {
                            if (args[index] == '\\')
                            {
                                int cSlashes = 1;
                                index++;
                                while (index == args.Length && args[index] == '\\')
                                {
                                    cSlashes++;
                                }
                                if (index == args.Length || args[index] != '"')
                                {
                                    currentArg.Append('\\', cSlashes);
                                }
                                else
                                {
                                    currentArg.Append('\\', cSlashes >> 1);
                                    if (0 != (cSlashes & 1))
                                    {
                                        currentArg.Append('"');
                                    }
                                    else
                                    {
                                        inQuotes = !inQuotes;
                                    }
                                }
                            }
                            else
                            {
                                if (args[index] == '"')
                                {
                                    inQuotes = !inQuotes;
                                    index++;
                                }
                                else
                                {
                                    currentArg.Append(args[index]);
                                    index++;
                                }
                            }
                        } while (!char.IsWhiteSpace(args[index]) || inQuotes);
                        argArray.Add(currentArg.ToString());
                        currentArg.Length = 0;
                    }
                }
            }
            catch (IndexOutOfRangeException)
            {
                if (inQuotes)
                {
                    reporter(string.Format("Error: Unbalanced '\"' in command line argument file '{0}'", fileName));
                    hadError = true;
                }
                else
                {
                    if (currentArg.Length > 0)
                    {
                        argArray.Add(currentArg.ToString());
                    }
                }
            }
            arguments = (string[]) argArray.ToArray(typeof (string));
            result = hadError;
            return result;
        }

        static string LongName(ArgumentAttribute attribute, FieldInfo field)
        {
            return (attribute == null || attribute.DefaultLongName) ? field.Name : attribute.LongName;
        }

        static string ShortName(ArgumentAttribute attribute, FieldInfo field)
        {
            string result;
            if (attribute is DefaultArgumentAttribute)
            {
                result = null;
            }
            else
            {
                if (!ExplicitShortName(attribute))
                {
                    result = LongName(attribute, field).Substring(0, 1);
                }
                else
                {
                    result = attribute.ShortName;
                }
            }
            return result;
        }

        static string HelpText(ArgumentAttribute attribute, FieldInfo field)
        {
            string result;
            if (attribute == null)
            {
                result = null;
            }
            else
            {
                result = attribute.HelpText;
            }
            return result;
        }

        static bool HasHelpText(ArgumentAttribute attribute)
        {
            return attribute != null && attribute.HasHelpText;
        }

        static bool ExplicitShortName(ArgumentAttribute attribute)
        {
            return attribute != null && !attribute.DefaultShortName;
        }

        static object DefaultValue(ArgumentAttribute attribute, FieldInfo field)
        {
            return (attribute == null || !attribute.HasDefaultValue) ? null : attribute.DefaultValue;
        }

        static Type ElementType(FieldInfo field)
        {
            Type result;
            if (IsCollectionType(field.FieldType))
            {
                result = field.FieldType.GetElementType();
            }
            else
            {
                result = null;
            }
            return result;
        }

        static ArgumentType Flags(ArgumentAttribute attribute, FieldInfo field)
        {
            ArgumentType result;
            if (attribute != null)
            {
                result = attribute.Type;
            }
            else
            {
                if (IsCollectionType(field.FieldType))
                {
                    result = ArgumentType.MultipleUnique;
                }
                else
                {
                    result = ArgumentType.AtMostOnce;
                }
            }
            return result;
        }

        static bool IsCollectionType(Type type)
        {
            return type.IsArray;
        }

        static bool IsValidElementType(Type type)
        {
            return type != null && (type == typeof (int) || type == typeof (uint) || type == typeof (string) || type == typeof (bool) || type.IsEnum);
        }

        class Argument
        {
            readonly ArrayList collectionValues;
            readonly object defaultValue;
            readonly Type elementType;
            readonly bool explicitShortName;
            readonly FieldInfo field;
            readonly ArgumentType flags;
            readonly bool hasHelpText;
            readonly string helpText;
            readonly bool isDefault;
            readonly string longName;
            readonly ErrorReporter reporter;
            bool seenValue;
            string shortName;

            public Argument(ArgumentAttribute attribute, FieldInfo field, ErrorReporter reporter)
            {
                longName = Parser.LongName(attribute, field);
                explicitShortName = Parser.ExplicitShortName(attribute);
                shortName = Parser.ShortName(attribute, field);
                hasHelpText = Parser.HasHelpText(attribute);
                helpText = Parser.HelpText(attribute, field);
                defaultValue = Parser.DefaultValue(attribute, field);
                elementType = ElementType(field);
                flags = Flags(attribute, field);
                this.field = field;
                seenValue = false;
                this.reporter = reporter;
                isDefault = (attribute != null && attribute is DefaultArgumentAttribute);
                if (IsCollection)
                {
                    collectionValues = new ArrayList();
                }
                Debug.Assert(longName != null && longName != "");
                Debug.Assert(!isDefault || !ExplicitShortName);
                Debug.Assert(!IsCollection || AllowMultiple, "Collection arguments must have allow multiple");
                Debug.Assert(!Unique || IsCollection, "Unique only applicable to collection arguments");
                Debug.Assert(IsValidElementType(Type) || IsCollectionType(Type));
                Debug.Assert((IsCollection && IsValidElementType(elementType)) || (!IsCollection && elementType == null));
                Debug.Assert(!IsRequired || !HasDefaultValue, "Required arguments cannot have default value");
                Debug.Assert(!HasDefaultValue || defaultValue.GetType() == field.FieldType, "Type of default value must match field type");
            }

            public Type ValueType
            {
                get { return IsCollection ? elementType : Type; }
            }

            public string LongName
            {
                get { return longName; }
            }

            public bool ExplicitShortName
            {
                get { return explicitShortName; }
            }

            public string ShortName
            {
                get { return shortName; }
            }

            public bool HasShortName
            {
                get { return shortName != null; }
            }

            public bool HasHelpText
            {
                get { return hasHelpText; }
            }

            public string HelpText
            {
                get { return helpText; }
            }

            public object DefaultValue
            {
                get { return defaultValue; }
            }

            public bool HasDefaultValue
            {
                get { return null != defaultValue; }
            }

            public string FullHelpText
            {
                get
                {
                    var builder = new StringBuilder();
                    if (HasHelpText)
                    {
                        builder.Append(HelpText);
                    }
                    if (HasDefaultValue)
                    {
                        if (builder.Length > 0)
                        {
                            builder.Append(" ");
                        }
                        builder.Append("Default value:'");
                        AppendValue(builder, DefaultValue);
                        builder.Append('\'');
                    }
                    if (HasShortName)
                    {
                        if (builder.Length > 0)
                        {
                            builder.Append(" ");
                        }
                        builder.Append("(short form /");
                        builder.Append(ShortName);
                        builder.Append(")");
                    }
                    return builder.ToString();
                }
            }

            public string SyntaxHelp
            {
                get
                {
                    var builder = new StringBuilder();
                    if (IsDefault)
                    {
                        builder.Append("<");
                        builder.Append(LongName);
                        builder.Append(">");
                    }
                    else
                    {
                        builder.Append("/");
                        builder.Append(LongName);
                        Type valueType = ValueType;
                        if (valueType == typeof (int))
                        {
                            builder.Append(":<int>");
                        }
                        else
                        {
                            if (valueType == typeof (uint))
                            {
                                builder.Append(":<uint>");
                            }
                            else
                            {
                                if (valueType == typeof (bool))
                                {
                                    builder.Append("[+|-]");
                                }
                                else
                                {
                                    if (valueType == typeof (string))
                                    {
                                        builder.Append(":<string>");
                                    }
                                    else
                                    {
                                        Debug.Assert(valueType.IsEnum);
                                        builder.Append(":{");
                                        bool first = true;
                                        FieldInfo[] fields = valueType.GetFields();
                                        for (int i = 0; i < fields.Length; i++)
                                        {
                                            FieldInfo field = fields[i];
                                            if (field.IsStatic)
                                            {
                                                if (first)
                                                {
                                                    first = false;
                                                }
                                                else
                                                {
                                                    builder.Append('|');
                                                }
                                                builder.Append(field.Name);
                                            }
                                        }
                                        builder.Append('}');
                                    }
                                }
                            }
                        }
                    }
                    return builder.ToString();
                }
            }

            public bool IsRequired
            {
                get { return ArgumentType.AtMostOnce != (flags & ArgumentType.Required); }
            }

            public bool SeenValue
            {
                get { return seenValue; }
            }

            public bool AllowMultiple
            {
                get { return ArgumentType.AtMostOnce != (flags & ArgumentType.Multiple); }
            }

            public bool Unique
            {
                get { return ArgumentType.AtMostOnce != (flags & ArgumentType.Unique); }
            }

            public Type Type
            {
                get { return field.FieldType; }
            }

            public bool IsCollection
            {
                get { return IsCollectionType(Type); }
            }

            public bool IsDefault
            {
                get { return isDefault; }
            }

            public bool Finish(object destination)
            {
                if (!SeenValue && HasDefaultValue)
                {
                    field.SetValue(destination, DefaultValue);
                }
                if (IsCollection)
                {
                    field.SetValue(destination, collectionValues.ToArray(elementType));
                }
                return ReportMissingRequiredArgument();
            }

            bool ReportMissingRequiredArgument()
            {
                bool result;
                if (IsRequired && !SeenValue)
                {
                    if (IsDefault)
                    {
                        reporter(string.Format("Missing required argument '<{0}>'.", LongName));
                    }
                    else
                    {
                        reporter(string.Format("Missing required argument '/{0}'.", LongName));
                    }
                    result = true;
                }
                else
                {
                    result = false;
                }
                return result;
            }

            void ReportDuplicateArgumentValue(string value)
            {
                reporter(string.Format("Duplicate '{0}' argument '{1}'", LongName, value));
            }

            public bool SetValue(string value, object destination)
            {
                bool result;
                if (SeenValue && !AllowMultiple)
                {
                    reporter(string.Format("Duplicate '{0}' argument", LongName));
                    result = false;
                }
                else
                {
                    seenValue = true;
                    object newValue;
                    if (!ParseValue(ValueType, value, out newValue))
                    {
                        result = false;
                    }
                    else
                    {
                        if (IsCollection)
                        {
                            if (Unique && collectionValues.Contains(newValue))
                            {
                                ReportDuplicateArgumentValue(value);
                                result = false;
                                return result;
                            }
                            collectionValues.Add(newValue);
                        }
                        else
                        {
                            field.SetValue(destination, newValue);
                        }
                        result = true;
                    }
                }
                return result;
            }

            void ReportBadArgumentValue(string value)
            {
                reporter(string.Format("'{0}' is not a valid value for the '{1}' command line option", value, LongName));
            }

            bool ParseValue(Type type, string stringData, out object value)
            {
                bool result;
                if ((stringData != null || type == typeof (bool)) && (stringData == null || stringData.Length > 0))
                {
                    try
                    {
                        if (type == typeof (string))
                        {
                            value = stringData;
                            result = true;
                            return result;
                        }
                        if (type == typeof (bool))
                        {
                            if (stringData == null || stringData == "+")
                            {
                                value = true;
                                result = true;
                                return result;
                            }
                            if (stringData == "-")
                            {
                                value = false;
                                result = true;
                                return result;
                            }
                        }
                        else
                        {
                            if (type == typeof (int))
                            {
                                value = int.Parse(stringData);
                                result = true;
                                return result;
                            }
                            if (type == typeof (uint))
                            {
                                value = int.Parse(stringData);
                                result = true;
                                return result;
                            }
                            Debug.Assert(type.IsEnum);
                            value = Enum.Parse(type, stringData, true);
                            result = true;
                            return result;
                        }
                    }
                    catch
                    {
                    }
                }
                ReportBadArgumentValue(stringData);
                value = null;
                result = false;
                return result;
            }

            void AppendValue(StringBuilder builder, object value)
            {
                if (value is string || value is int || value is uint || value.GetType().IsEnum)
                {
                    builder.Append(value.ToString());
                }
                else
                {
                    if (value is bool)
                    {
                        builder.Append(((bool) value) ? "+" : "-");
                    }
                    else
                    {
                        bool first = true;
                        foreach (object o in (Array) value)
                        {
                            if (!first)
                            {
                                builder.Append(", ");
                            }
                            AppendValue(builder, o);
                            first = false;
                        }
                    }
                }
            }

            public void ClearShortName()
            {
                shortName = null;
            }
        }

        struct ArgumentHelpStrings
        {
            public readonly string help;
            public readonly string syntax;

            public ArgumentHelpStrings(string syntax, string help)
            {
                this.syntax = syntax;
                this.help = help;
            }
        }

        struct CONSOLE_SCREEN_BUFFER_INFO
        {
            internal COORD dwCursorPosition;
            internal COORD dwMaximumWindowSize;
            internal COORD dwSize;
            internal SMALL_RECT srWindow;
            internal short wAttributes;
        }

        struct COORD
        {
            internal short x;
            internal short y;
        }

        class HelpArgument
        {
            [Argument(ArgumentType.AtMostOnce, ShortName = "?")] public bool help;
        }

        struct SMALL_RECT
        {
            internal short Bottom;
            internal short Left;
            internal short Right;
            internal short Top;
        }
    }
}