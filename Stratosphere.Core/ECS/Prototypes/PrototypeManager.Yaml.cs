using Stratosphere.Core.Serialization;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Stratosphere.Core.ECS.Prototypes;

public sealed partial class PrototypeManager
{
    private readonly ComponentFactory _factory;
    private readonly Dictionary<string, RawPrototype> _pending = new();
    private readonly Dictionary<string, FlatPrototype> _flat = new();

    private static readonly HashSet<string> EntityKeys = new(StringComparer.Ordinal)
    { "type", "id", "name", "description", "parent", "parents", "abstract", "components" };

    public PrototypeManager() : this(new ComponentFactory()) { }
    public PrototypeManager(ComponentFactory factory) { _factory = factory; }

    public ComponentFactory ComponentFactory => _factory;

    public void LoadString(string yaml, string sourceName = "<string>")
    {
        var stream = new YamlStream();
        try { stream.Load(new StringReader(yaml)); }
        catch (Exception e) when (e is YamlException or ArgumentException)
        {
            int line = e is YamlException ye ? (int) ye.Start.Line : 0;
            throw new DataLoadException(sourceName, line, $"YAML couldn't be read: {e.Message}");
        }

        foreach (var document in stream.Documents)
        {
            if (document.RootNode is not YamlSequenceNode list)
                throw NodeReader.Error(sourceName, document.RootNode, "A prototype file must be a list, with each prototype starting with '- type: entity'.");

            foreach (var item in list.Children)
                ReadEntry(item, sourceName);
        }
    }

    public void LoadFile(string path) => LoadString(File.ReadAllText(path), path);

    public void LoadDirectory(string directory)
    {
        var files = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".yml", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f, StringComparer.Ordinal);

        foreach (var file in files) LoadFile(file);
    }

    public void Resolve()
    {
        if (_pending.Count == 0) return;

        var memo = new Dictionary<string, FlatPrototype>();
        var built = new List<(RawPrototype Raw, FlatPrototype Flat, EntityPrototype Prototype)>();

        foreach (var raw in _pending.Values)
        {
            var flat = Flatten(raw, memo, new List<string>());
            built.Add((raw, flat, Build(raw, flat)));
        }

        foreach (var (raw, flat, prototype) in built)
        {
            _entities.Add(raw.Id, prototype);
            _flat.Add(raw.Id, flat);
        }
        _pending.Clear();
    }


    private void ReadEntry(YamlNode item, string source)
    {
        if (item is not YamlMappingNode map)
            throw NodeReader.Error(source, item, "Each prototype must be a group of fields, starting with 'type: entity'.");

        var keys = new Dictionary<string, (YamlNode Key, YamlNode Value)>(StringComparer.Ordinal);
        foreach (var field in NodeReader.FieldsOf(map, source))
        {
            if (!EntityKeys.Contains(field.Name))
                throw NodeReader.Error(source, field.Key,
                $"Unknown prototype field '{field.Name}'. Valid fields: {string.Join(", ", EntityKeys.OrderBy(k => k, StringComparer.Ordinal))}");
            keys[field.Name] = (field.Key, field.Value);
        }

        if (!keys.TryGetValue("type", out var typeEntry))
            throw NodeReader.Error(source, map, "A prototype needs a 'type'. For example 'type: entity'.");

        var type = ScalarText(typeEntry.Value, source, "type");
        if (type != "entity")
            throw NodeReader.Error(source, typeEntry.Value, $"Unknown prototype type '{type}'. The only type so far is 'entity'.");

        if (!keys.TryGetValue("id", out var idEntry))
            throw NodeReader.Error(source, map, "A prototype needs an 'id'.");

        var id = ScalarText(idEntry.Value, source, "id");
        if (id.Length == 0)
            throw NodeReader.Error(source, idEntry.Value, "A prototype's 'id' can't be empty.");

        int line = (int) map.Start.Line;

        if (_pending.TryGetValue(id, out var pendingDuplicate))
            throw new DataLoadException(source, line, $"Duplicate entity prototype ID '{id}'; it's already defined at {pendingDuplicate.Source}:{pendingDuplicate.Line}.");
        if (_flat.TryGetValue(id, out var flatDuplicate))
            throw new DataLoadException(source, line, $"Duplicate entity prototype ID '{id}'; it's already defined at {flatDuplicate.Source}:{flatDuplicate.Line}.");
        if (_entities.ContainsKey(id))
            throw new DataLoadException(source, line, $"Duplicate entity prototype ID '{id}'; it's already registered in code.");

        if (keys.ContainsKey("parent") && keys.ContainsKey("parents"))
            throw NodeReader.Error(source, keys["parents"].Key, "Use either 'parent' or 'parents', not both.");

        var parents = new List<string>();
        foreach (var name in new[] { "parent", "parents" })
        {
            if (!keys.TryGetValue(name, out var parentEntry)) continue;

            if (parentEntry.Value is YamlSequenceNode parentList)
                foreach (var parent in parentList.Children)
                    parents.Add(ScalarText(parent, source, name));
            else
                parents.Add(ScalarText(parentEntry.Value, source, name));
        }

        bool isAbstract = false;
        if (keys.TryGetValue("abstract", out var abstractEntry))
            isAbstract = (bool) NodeReader.Read(typeof(bool), abstractEntry.Value, source)!;

        var raw = new RawPrototype(id, source, line)
        {
            Name = keys.TryGetValue("name", out var nameEntry) ? (string?) NodeReader.Read(typeof(string), nameEntry.Value, source) : null,
            Description = keys.TryGetValue("description", out var descriptionEntry) ? (string?) NodeReader.Read(typeof(string), descriptionEntry.Value, source) : null,
            Abstract = isAbstract,
        };
        raw.Parents.AddRange(parents);

        if (keys.TryGetValue("components", out var componentsEntry))
            ReadComponents(raw, componentsEntry.Value, source);

        _pending.Add(id, raw);
    }

    private static void ReadComponents(RawPrototype raw, YamlNode node, string source)
    {
        if (node is YamlScalarNode { Value: null or "" or "~" }) return;
        if (node is not YamlSequenceNode list)
            throw NodeReader.Error(source, node, "'components' must be a list, with each component starting with '- type: <name>'.");

        foreach (var item in list.Children)
        {
            if (item is not YamlMappingNode map)
                throw NodeReader.Error(source, node, "'components' must be a list, with each component starting with 'type: <name>'.");

            string? name = null;
            var fields = new Dictionary<string, YamlField>(StringComparer.Ordinal);

            foreach (var field in NodeReader.FieldsOf(map, source))
            {
                if (field.Name == "type") name = ScalarText(field.Value, source, "type");
                else
                    fields.Add(field.Name, field);
            }

            if (name == null)
                throw NodeReader.Error(source, map, "A component needs a 'type'. For example, '- type: Health'.");
            if (raw.Components.Any(c => c.Name == name))
                throw NodeReader.Error(source, map, $"Prototype '{raw.Id}' lists the component '{name}' twice.");

            raw.Components.Add(new ComponentData(name, source, (int)map.Start.Line, fields));
        }
    }


    private static string ScalarText(YamlNode node, string source, string what)
        => node is YamlScalarNode scalar
            ? scalar.Value ?? ""
            : throw NodeReader.Error(source, node, $"'{what}' must be plain text.");

    private FlatPrototype Flatten(RawPrototype raw, Dictionary<string, FlatPrototype> memo, List<string> chain)
    {
        if (memo.TryGetValue(raw.Id, out var done)) return done;
        if (chain.Contains(raw.Id))
        {
            var loop = chain.Skip(chain.IndexOf(raw.Id)).Append(raw.Id);
            throw new DataLoadException(raw.Source, raw.Line, $"Prototypes inherit from each other in a circular manner: {string.Join(" -> ", loop)}");
        }

        chain.Add(raw.Id);
        var flat = new FlatPrototype(raw.Source, raw.Line);

        foreach (var parentId in raw.Parents)
        {
            FlatPrototype parent;
            if (_pending.TryGetValue(parentId, out var parentRaw))
                parent = Flatten(parentRaw, memo, chain);
            else if (!_flat.TryGetValue(parentId, out parent!))
            {
                throw new DataLoadException(raw.Source, raw.Line, _entities.ContainsKey(parentId)
                ? $"Prototype '{raw.Id}' inherits from '{parentId}', which is defined in code. Only YAML prototypes can be inherited from."
                : $"Prototype '{raw.Id}' has unknown parent '{parentId}'.");
            }

            flat.Apply(parent.Name, parent.Description, parent.Components);
        }

        flat.Apply(raw.Name, raw.Description, raw.Components);

        chain.RemoveAt(chain.Count - 1);
        memo[raw.Id] = flat;
        return flat;
    }

    private EntityPrototype Build(RawPrototype raw, FlatPrototype flat)
    {
        var prototype = new EntityPrototype(raw.Id, flat.Name, flat.Description,
            raw.Parents.Count > 0 ? raw.Parents.ToArray() : null, raw.Abstract);

        foreach (var data in flat.Components)
        {
            if (!_factory.TryGetType(data.Name, out _))
                throw new DataLoadException(data.Source, data.Line,
                $"Unknown component '{data.Name}'. Known components: {string.Join(", ", _factory.Names)}.");

            var component = _factory.Create(data.Name);
            NodeReader.Populate(component, data.Fields.Values, data.Source, data.Line, $"component '{data.Name}'");
            prototype.Components.Add(data.Name, new ComponentRegiEntry(component));
        }

        return prototype;
    }



    private sealed class ComponentData
    {
        public string Name { get; }
        public string Source { get; }
        public int Line { get; }
        public Dictionary<string, YamlField> Fields { get; }

        public ComponentData(string name, string source, int line, Dictionary<string, YamlField> fields)
        {
            Name = name;
            Source = source;
            Line = line;
            Fields = fields;
        }
    }

    private sealed class RawPrototype
    {
        public string Id { get; }
        public string Source { get; }
        public int Line { get; }
        public string? Name { get; init; }
        public string? Description { get; init; }
        public bool Abstract { get; init; }
        public List<string> Parents { get; } = new();
        public List<ComponentData> Components { get; } = new();

        public RawPrototype(string id, string source, int line)
        {
            Id = id;
            Source = source;
            Line = line;
        }
    }

    private sealed class FlatPrototype
    {
        public string Source { get; }
        public int Line { get; }
        public string? Name { get; private set; }
        public string? Description { get; private set; }
        public List<ComponentData> Components { get; } = new();

        public FlatPrototype(string source, int line)
        {
            Source = source;
            Line = line;
        }

        public void Apply(string? name, string? description, List<ComponentData> components)
        {
            if (name != null) Name = name;
            if (description != null) Description = description;

            foreach (var incoming in components)
            {
                var index = Components.FindIndex(c => c.Name == incoming.Name);
                if (index < 0)
                {
                    /* Copy the field table, so that way merging into this later doesn't change the parent's table. */
                    Components.Add(new ComponentData(incoming.Name, incoming.Source, incoming.Line, new Dictionary<string, YamlField>(incoming.Fields)));
                    continue;
                }

                foreach (var (fieldName, field) in incoming.Fields)
                    Components[index].Fields[fieldName] = field;
            }
        }
    }
}
