using System.Collections;
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using Stratosphere.Core.ECS.Prototypes;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Stratosphere.Core.Serialization;

internal static class NodeReader
{
    private sealed class Member
    {
        public required string Tag { get; init; }
        public required Type Type { get; init; }
        public required bool Required { get; init; }
        public required Action<object, object?> Set { get; init; }
    }

    private static readonly ConcurrentDictionary<Type, Dictionary<string, Member>> MemberCache = new();

    public static DataLoadException Error(string source, YamlNode node, string message)
        => new(source, (int) node.Start.Line, message);

    public static void Populate(object target, IEnumerable<YamlField> fields, string source, int line, string what)
    {
        var members = MembersOf(target.GetType());
        var seen = new HashSet<string>();

        foreach (var field in fields)
        {
            if (!members.TryGetValue(field.Name, out var member))
                throw Error(field.Source, field.Key,
                    $"Unknown field '{field.Name}' on {what}. "
                    + (members.Count == 0 ? "It has no fields." : $"Valid fields: {string.Join(", ", members.Keys.OrderBy(k => k, StringComparer.Ordinal))}."));

            member.Set(target, Read(member.Type, field.Value, field.Source));
            seen.Add(field.Name);
        }

        foreach (var member in members.Values)
            if (member.Required && !seen.Contains(member.Tag))
                throw new DataLoadException(source, line, $"The {what} is missing required field '{member.Tag}'.");
    }

    public static List<YamlField> FieldsOf(YamlMappingNode mapping, string source)
    {
        var fields = new List<YamlField>();
        foreach (var pair in mapping.Children)
        {
            if (pair.Key is not YamlScalarNode key)
                throw Error(source, pair.Key, "Field names must be plain text.");
            fields.Add(new YamlField(key.Value ?? "", pair.Key, pair.Value, source));
        }
        return fields;
    }

    public static object? Read(Type type, YamlNode node, string source)
    {
        var underlying = Nullable.GetUnderlyingType(type);

        if (IsNull(node))
        {
            if (underlying != null || !type.IsValueType) return null;
            throw Error(source, node, $"A value is needed here (a {Friendly(type)}).");
        }

        if (underlying != null) type = underlying;
        if (type == typeof(object)) return ReadUntyped(node, source);
        if (type == typeof(string)) return Scalar(node, source, type).Value ?? "";
        if (type == typeof(bool))
        {
            var text = Scalar(node, source, type).Value ?? "";
            if (text.Equals("true", StringComparison.OrdinalIgnoreCase)) return true;
            if (text.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;
            throw Error(source, node, $"Expected true or false, got '{text}'.");
        }

        if (type == typeof(char))
        {
            var text = Scalar(node, source, type).Value ?? "";
            if (text.Length == 1) return text[0];
            throw Error(source, node, $"Expected single character, got '{text}'.");
        }

        if (type.IsEnum) return ReadEnum(type, Scalar(node, source, type), source);

        if ((type.IsPrimitive && type != typeof(IntPtr) && type != typeof(UIntPtr)) || type == typeof(decimal))
        {
            var text = (Scalar(node, source, type).Value ?? "").Trim();
            try { return Convert.ChangeType(text, type, CultureInfo.InvariantCulture); }
            catch (Exception e) when (e is FormatException or OverflowException or InvalidCastException)
            { throw Error(source, node, $"Expected a {Friendly(type)}, but found '{text}'."); }
        }

        if (type.IsArray)
        {
            var elementType = type.GetElementType()!;
            var items = Sequence(node, source, type);
            var array = Array.CreateInstance(elementType, items.Children.Count);
            for (int i = 0; i < array.Length; i++)
                array.SetValue(Read(elementType, items.Children[i], source), i);
            return array;
        }

        if (type.IsGenericType)
        {
            var definition = type.GetGenericTypeDefinition();
            var args = type.GetGenericArguments();

            if (definition == typeof(List<>) || definition == typeof(IList<>) || definition == typeof(ICollection<>)
                || definition == typeof(IEnumerable<>) || definition == typeof(IReadOnlyList<>) || definition == typeof(IReadOnlyCollection<>))
            {
                var items = Sequence(node, source, type);
                var list = (IList) Activator.CreateInstance(typeof(List<>).MakeGenericType(args[0]))!;
                foreach (var item in items.Children)
                    list.Add(Read(args[0], item, source));
                return list;
            }

            if (definition == typeof(Dictionary<,>) || definition == typeof(IDictionary<,>) || definition == typeof(IReadOnlyDictionary<,>))
            {
                if (args[0] != typeof(string))
                    throw Error(source, node, $"Only dictionaries with text keys can be read from YAML, not {Friendly(type)}.");

                var mapping = Mapping(node, source, type);
                var dictionary = (IDictionary) Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(args))!;
                foreach (var pair in mapping.Children)
                {
                    if (pair.Key is not YamlScalarNode key)
                        throw Error(source, pair.Key, "Keys must be plain text.");
                    dictionary[key.Value ?? ""] = Read(args[1], pair.Value, source);
                }
                return dictionary;
            }
        }

        var members = MembersOf(type);
        if (members.Count > 0)
        {
            var mapping = Mapping(node, source, type);
            object instance;
            try
            {
                instance = Activator.CreateInstance(type)!;
            }
            catch (MissingMethodException)
            {
                throw Error(source, node, $"{type.Name} can't be created from YAML because it has no constructor without parameters.");
            }
            Populate(instance, FieldsOf(mapping, source), source, (int) node.Start.Line, type.Name);
            return instance;
        }

        throw Error(source, node, $"Don't know how to read a {Friendly(type)} from YAML. (Does it need [DataField] members?)");
    }

    private static object? ReadUntyped(YamlNode node, string source)
    {
        switch (node)
        {
            case YamlScalarNode scalar:
                var text = scalar.Value ?? "";
                if (scalar.Style != ScalarStyle.Plain) return text;
                if (text.Equals("true", StringComparison.OrdinalIgnoreCase)) return true;
                if (text.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;
                if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var whole))
                {
                    /* ?: would quietly cast int to long */
                    if (whole is >= int.MinValue and <= int.MaxValue) return (int) whole;
                    return whole;
                }
                if ((text.Contains('.') || text.Contains('e') || text.Contains('E'))
                    && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                    return number;
                return text;

            case YamlSequenceNode sequence:
                return sequence.Children.Select(child => ReadUntyped(child, source)).ToList();

            case YamlMappingNode mapping:
                var dictionary = new Dictionary<string, object?>();
                foreach (var pair in mapping.Children)
                {
                    if (pair.Key is not YamlScalarNode key)
                        throw Error(source, pair.Key, "Keys must be plain text.");
                    dictionary[key.Value ?? ""] = ReadUntyped(pair.Value, source);
                }
                return dictionary;

            default:
                throw Error(source, node, "Unsupported YAML.");
        }
    }

    private static object ReadEnum(Type type, YamlScalarNode node, string source)
    {
        var text = (node.Value ?? "").Trim();
        var names = Enum.GetNames(type);
        var parts = type.IsDefined(typeof(FlagsAttribute), inherit: false)
            ? text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            : new[] { text };

        if (parts.Length == 0) throw Error(source, node, $"Expected one of: {string.Join(", ", names)}.");

        ulong combined = 0;
        foreach (var part in parts)
        {
            if (Array.IndexOf(names, part) < 0)
                throw Error(source, node, $"'{part}' isn't a {type.Name}. Expected one of: {string.Join(", ", names)}.");
            combined |= Convert.ToUInt64(Enum.Parse(type, part), CultureInfo.InvariantCulture);
        }
        return Enum.ToObject(type, combined);
    }

    private static bool IsNull(YamlNode node)
        => node is YamlScalarNode { Style: ScalarStyle.Plain } scalar
           && (scalar.Value is null or "" or "~" or "null" or "Null" or "NULL");

    private static YamlScalarNode Scalar(YamlNode node, string source, Type wanted)
        => node as YamlScalarNode ?? throw Error(source, node, $"Expected a {Friendly(wanted)}, but found {Describe(node)}.");

    private static YamlSequenceNode Sequence(YamlNode node, string source, Type wanted)
        => node as YamlSequenceNode ?? throw Error(source, node, $"Expected a list for a {Friendly(wanted)}, but found {Describe(node)}.");

    private static YamlMappingNode Mapping(YamlNode node, string source, Type wanted)
        => node as YamlMappingNode ?? throw Error(source, node, $"Expected a group of fields for a {Friendly(wanted)}, but found {Describe(node)}.");


    private static string Describe(YamlNode node) => node switch
    {
        YamlSequenceNode => "a list",
        YamlMappingNode => "a group of fields",
        _ => "a value"
    };

    private static string Friendly(Type type)
    {
        if (!type.IsGenericType) return type.Name;
        var name = type.Name[..type.Name.IndexOf('`')];
        return $"{name}<{string.Join(", ", type.GetGenericArguments().Select(Friendly))}>";
    }


    private static Dictionary<string, Member> MembersOf(Type type) => MemberCache.GetOrAdd(type, BuildMembers);
    private static Dictionary<string, Member> BuildMembers(Type type)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        var members = new Dictionary<string, Member>(StringComparer.Ordinal);

        void Add(string tag, bool required, Type memberType, Action<object, object?> set, string where)
        {
            if (!members.TryAdd(tag, new Member { Tag = tag, Type = memberType, Required = required, Set = set }))
                throw new InvalidOperationException($"{type.FullName} has two [DataField(\"{tag}\")] members ({where}).");
        }

        for (var t = type; t != null && t != typeof(object); t = t.BaseType)
        {
            foreach (var field in t.GetFields(flags))
            {
                var attribute = field.GetCustomAttribute<DataFieldAttribute>();
                if (attribute == null) continue;
                Add(attribute.Tag, attribute.Required, field.FieldType, (target, value) => field.SetValue(target, value), field.Name);
            }

            foreach (var property in t.GetProperties(flags))
            {
                var attribute = property.GetCustomAttribute<DataFieldAttribute>();
                if (attribute == null) continue;
                if (property.GetSetMethod(nonPublic: true) == null)
                    throw new InvalidOperationException($"{type.FullName}.{property.Name} has [DataField] but no setter, YAML can't fill it in.");
                Add(attribute.Tag, attribute.Required, property.PropertyType, (target, value) => property.SetValue(target, value), property.Name);
            }
        }
    
        return members;
    }
}
