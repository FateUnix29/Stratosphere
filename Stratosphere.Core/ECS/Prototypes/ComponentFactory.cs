using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Stratosphere.Core.ECS.Prototypes;

public sealed class ComponentFactory
{
    public readonly Dictionary<string, Type> _typesByName = new(StringComparer.Ordinal);
    public readonly Dictionary<Type, string> _namesByType = new();

    public ComponentFactory() { RegisterAssembly(typeof(ComponentFactory).Assembly); }

    /// <summary>
    /// Every registered YAML name, sorted by name.
    /// </summary>
    public IEnumerable<string> Names => _typesByName.Keys.OrderBy(n => n, StringComparer.Ordinal);

    public void RegisterAssembly(Assembly assembly)
    {
        foreach (var type in AssemblyScan.TypesWith<RegisterComponentAttribute>(assembly))
            Register(type, type.GetCustomAttribute<RegisterComponentAttribute>()!.Name);
    }

    public void Register<T>(string? name = null) where T : class, IComponent
        => Register(typeof(T), name);

    public void Register(Type type, string? name = null)
    {
        if (!typeof(IComponent).IsAssignableFrom(type) || !type.IsClass)
            throw new ArgumentException($"{type.FullName} marked as component, but isn't a class implementing IComponent.");
        if (type.IsAbstract)
            throw new ArgumentException($"{type.FullName} is abstract, and cannot be registered as a component.");
        if (type.GetConstructor(Type.EmptyTypes) == null)
            throw new ArgumentException($"{type.FullName} needs a public constructor with no parameters to be registered as a component.");

        name ??= ComponentNames.Of(type);
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException($"{type.FullName} was given an empty component name.");

        if (_typesByName.TryGetValue(name, out var existing))
        {
            if (existing == type) return;
            throw new InvalidOperationException($"Both {existing.FullName} and {type.FullName} want to be the '{name}' component. Naming conflict.");
        }

        if (_namesByType.TryGetValue(type, out var oldName))
            throw new InvalidOperationException($"{type.FullName} is already registered as '{oldName}'. Cannot register as '{name}'.");

        _typesByName.Add(name, type);
        _namesByType.Add(type, name);
    }

    public bool TryGetType(string name, [NotNullWhen(true)] out Type? type) => _typesByName.TryGetValue(name, out type);
    public bool TryGetName(Type type, [NotNullWhen(true)] out string? name) => _namesByType.TryGetValue(type, out name);

    public IComponent Create(string name)
    {
        if (!_typesByName.TryGetValue(name, out var type))
            throw new KeyNotFoundException($"Unknown component '{name}'.");

        return (IComponent)Activator.CreateInstance(type)!;
    }
}
