using System.Reflection;

namespace Stratosphere.Core;

internal static class AssemblyScan
{
    public static List<Type> TypesWith<TAttribute>(Assembly assembly) where TAttribute : Attribute
    {
        Type[] types;

        try { types = assembly.GetTypes(); }

        catch (ReflectionTypeLoadException e)
        { throw new InvalidOperationException($"Could not load types in {assembly.GetName().Name}: {e.LoaderExceptions.FirstOrDefault()?.Message}"); }

        return types
            .Where(t => t.IsDefined(typeof(TAttribute), inherit: false))
            .OrderBy(t => t.FullName, StringComparer.Ordinal)
            .ToList();
    }
}
