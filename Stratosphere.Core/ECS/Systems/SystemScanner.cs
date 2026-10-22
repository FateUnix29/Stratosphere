using System.Reflection;

namespace Stratosphere.Core.ECS.Systems;

internal static class SystemScanner
{
    /// <summary>
    /// Finds the systems marked with <see cref="RegisterSystemAttribute"/> in an assembly and puts them in update order.
    /// </summary>
    public static List<Type> FindInOrder(Assembly assembly, IReadOnlyCollection<Type> alreadyAdded)
    {
        var found = AssemblyScan.TypesWith<RegisterSystemAttribute>(assembly);

        foreach (var type in found)
        {
            if (!typeof(EntitySystem).IsAssignableFrom(type) || type.IsAbstract)
                throw new InvalidOperationException($"{type.FullName} marked as [RegisterSystem] but isn't a concrete EntitySystem.");
            if (type.GetConstructor(Type.EmptyTypes) == null)
                throw new InvalidOperationException($"{type.FullName} needs a public, blank, empty constructor to be registered automatically.");
        }

        var batch = found.Where(t => !alreadyAdded.Contains(t)).ToList();
        return Order(batch, alreadyAdded);
    }

    /// <summary>
    /// Puts systems in an order that respects their UpdatesAfter/UpdatesBefore attributes.
    /// Systems without constraints between them simply come out sorted by name.
    /// </summary>
    internal static List<Type> Order(List<Type> batch, IReadOnlyCollection<Type> alreadyAdded)
    {
        var inBatch = new HashSet<Type>(batch);
        var mustRunAfter = batch.ToDictionary(t => t, _ => new HashSet<Type>());

        foreach (var type in batch)
        {
            foreach (var attribute in type.GetCustomAttributes<UpdatesAfterAttribute>(inherit: false))
                foreach (var other in attribute.Systems)
                {
                    if (other == type)
                        throw new InvalidOperationException($"{type.Name} can't update after itself.");
                    if (inBatch.Contains(other))
                        mustRunAfter[type].Add(other);
                }

            foreach (var attribute in type.GetCustomAttributes<UpdatesBeforeAttribute>(inherit: false))
                foreach (var other in attribute.Systems)
                {
                    if (other == type)
                        throw new InvalidOperationException($"{type.Name} can't update before itself.");
                    if (inBatch.Contains(other))
                        mustRunAfter[other].Add(type);
                    else if (alreadyAdded.Contains(other))
                        throw new InvalidOperationException(
                        $"{type.Name} must update prior to {other.Name}, but {other.Name} was added to the world in an earlier pass and has already been placed.");
                }
        }

        var ordered = new List<Type>();
        var remaining = new HashSet<Type>(batch);

        while (remaining.Count > 0)
        {
            var next = remaining
                .Where(t => mustRunAfter[t].All(dependency => !remaining.Contains(dependency)))
                .OrderBy(t => t.FullName, StringComparer.Ordinal)
                .FirstOrDefault();

            if (next == null)
                throw new InvalidOperationException("The following systems have update orders that contradict one another: "
                    + string.Join(", ", remaining.Select(t => t.Name).OrderBy(n => n, StringComparer.Ordinal)));

            ordered.Add(next);
            remaining.Remove(next);
        }

        return ordered;
    }
}
