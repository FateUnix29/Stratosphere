using System.Diagnostics.CodeAnalysis;
using Stratosphere.Core.ECS.Prototypes;
namespace Stratosphere.Core.ECS;

/// <summary>
/// Generally something the World can use without knowing the types of what it's working with.
/// </summary>
internal interface IComponentStore
{
    int Count { get; }

    /// <summary>
    /// Adds a component whose type is only known at runtime.
    /// For example, from a prototype.
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="component"></param>
    void AddBoxed(EntityUid entity, IComponent component);

    /// <summary>
    /// Used for retrieving components without knowing the type of the ComponentStore at runtime.
    /// </summary>
    bool TryGetBoxed(EntityUid entity, [NotNullWhen(true)] out IComponent? component);

    /// <summary>
    /// Removes this entity's component from the store, if it has said component.
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="removed"></param>
    bool Remove(EntityUid entity, [NotNullWhen(true)] out IComponent? removed);
}

/// <summary>
/// Holds every component of a type, packed together in memory for efficient access!
/// </summary>
/// <remarks>
/// No longer just a custom dictionary. More like a 'sparse set.'
/// Provides two views of the same data...
/// - Dense: <c>_components</c> and <c>_owners</c> are plain arrays, without gaps. Entry <c>i</c> of one should theoretically belong to the same <c>i</c> of the other.
///   Looping over every component of one type is just iteration over an array.
/// - Sparse: Given an entity, what dense slot does it belong to? Entity UIDs only count up, so it's a table indexed directly by UID.
///   ...In pages, so stale UIDs from long ago don't cost anything.
///   There's no hashing or searching, just two array reads.
/// Removing something will swap the last entry into the hole left, which keeps the dense side gapless, though insertion order is lost. A worthwhile sacrifice.
/// </remarks>
internal sealed class ComponentStore<T> : IComponentStore where T: class, IComponent
{
    private const int PageBits = 12;
    private const int PageSize = 1 << PageBits;
    private const int PageMask = PageSize - 1;
    private const int MaxPages = 1 << 20;

    private T[] _components = new T[8];
    private EntityUid[] _owners = new EntityUid[8];
    private int _count;

    /* Cursed as always.
       Sparse side. _pages[id >> PageBits][id & PageMask] = dense index + 1, default of 0 can just be nothing. */
    private int[]?[] _pages = new int[]?[4];
    private int[] _pageLive = new int[4];

    /// <summary>
    /// Increments every addition or removal.
    /// </summary>
    public int Version { get; private set; }

    public int Count => _count;

    public EntityUid OwnerAt(int index) => _owners[index];
    public T ComponentAt(int index) => _components[index];

    public ReadOnlySpan<T> Components => new(_components, 0, _count);
    public ReadOnlySpan<EntityUid> Owners => new(_owners, 0, _count);

    private bool TrySlot(EntityUid entity, out int[] page, out int offset, out int slot)
    {
        long id = entity.Id;
        long pageIndex = id >> PageBits;

        if (id > 0 && pageIndex < _pages.Length && _pages[pageIndex] is { } found)
        {
            page = found;
            offset = (int)(id & PageMask);
            slot = found[offset];
            return slot != 0;
        }

        page = null!;
        offset = 0;
        slot = 0;
        return false;
    }


    public void Add(EntityUid entity, T component)
    {
        if (!entity.IsValid()) throw new ArgumentException($"{entity} is not a valid entity!", nameof(entity));
        if (TrySlot(entity, out _, out _, out _))
            /* Should we really be throwing here? Well, I guess they can catch it. */
            throw new InvalidOperationException($"Entity {entity} already has a {typeof(T).Name}.");

        long pageIndex = entity.Id >> PageBits;
        if (pageIndex >= MaxPages)
            /* ... dear /god/... */
            throw new InvalidOperationException($"Entity {entity}'s ID is beyond what the component store lookup table is willing to tolerate, which means you have at least four billion of them. Christ almighty. Check that.");

        if (pageIndex >= _pages.Length)
        {
            int length = Math.Max((int)pageIndex+1, _pages.Length*2);
            Array.Resize(ref _pages, length);
            Array.Resize(ref _pageLive, length);
        }

        var page = _pages[pageIndex] ??= new int[PageSize];

        if (_count == _components.Length)
        {
            Array.Resize(ref _components, _count * 2);
            Array.Resize(ref _owners, _count * 2);
            /* Do you ever wonder how Array.Resize actually *works*?
               Is it truly resizing an array in memory, or is the sneaky bastard taking the overhead to allocate a new one? */
        }

        _components[_count] = component;
        _owners[_count] = entity;
        page[entity.Id & PageMask] = ++_count;
        _pageLive[pageIndex]++;
        Version++;
    }

    public void AddBoxed(EntityUid entity, IComponent component) => Add(entity, (T)component);
    public bool TryGetBoxed(EntityUid entity, [NotNullWhen(true)] out IComponent? component)
    {
        var success = TryGet(entity, out var found);
        component = found;
        return success;
    }

    public bool Has(EntityUid entity) => TrySlot(entity, out _, out _, out _);
    public bool TryGet(EntityUid entity, [NotNullWhen(true)] out T? component)
    {
        if (TrySlot(entity, out _, out _, out int slot))
        {
            component = _components[slot-1];
            return true;
        }

        component = null;
        return false;
    }

    public bool Remove(EntityUid entity, [NotNullWhen(true)] out IComponent? removed)
    {
        if (!TrySlot(entity, out var page, out int offset, out int slot))
        {
            removed = null;
            return false;
        }

        int index = slot - 1;
        int last = _count - 1;
        removed = _components[index];

        if (index != last)
        {
            var moved = _owners[last];
            _components[index] = _components[last];
            _owners[index] = moved;
            _pages[moved.Id >> PageBits]![moved.Id & PageMask] = slot;
        }

        _components[last] = null!;
        _owners[last] = default;
        _count = last;

        page[offset] = 0;
        long pageIndex = entity.Id >> PageBits;
        if (--_pageLive[pageIndex] == 0) _pages[pageIndex] = null;

        Version++;
        return true;
    }
}
