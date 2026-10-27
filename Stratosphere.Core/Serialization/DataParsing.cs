using System.Collections.Concurrent;
using System.Globalization;
using System.Numerics;
using System.Reflection;
using Stratosphere.Core.Maths;

namespace Stratosphere.Core.Serialization;

public interface IDataParsable<TSelf> where TSelf : IDataParsable<TSelf>
{
    static abstract TSelf ParseData(object raw);
}

public sealed class DataFormatException : Exception
{
    public DataFormatException(string message) : base(message) { }
}

public static class DataParse
{
    public static double[] Numbers(object raw, int min, int max, string what, string example)
    {
        if (raw is not List<object?> list) throw new DataFormatException($"Expected {what} as a list of numbers like {example}.");
        if (list.Count < min || list.Count > max)
            throw new DataFormatException($"Expected {what} with {(min == max ? min.ToString() : $"{min}-{max}")} numbers like {example}, got {list.Count}");

        var numbers = new double[list.Count];
        for (int i = 0; i < numbers.Length; i++) numbers[i] = Number(list[i], what);
        return numbers;
    }

    public static double Number(object? raw, string what) => raw switch
    {
        int i => i,
        long l => l,
        double d => d,
        _ => throw new DataFormatException($"Expected number in {what}, found '{raw}'.")
    };

    public static bool IsNumber(object? raw) => raw is int or long or double;
}

internal static class DataParsers
{
    private static readonly Dictionary<Type, Func<object, object?>> BuiltIn = new()
    {
        [typeof(Vector2)]    = raw => { var n = DataParse.Numbers(raw, 2, 2, "a 2D vector", "[1, 2]"); return new Vector2((float) n[0], (float) n[1]); },
        [typeof(Vector3)]    = raw => { var n = DataParse.Numbers(raw, 2, 3, "a vector", "[1, 2, 3]"); return new Vector3((float) n[0], (float) n[1], n.Length > 2 ? (float) n[2] : 0f); },
        [typeof(Vector3d)]   = raw => { var n = DataParse.Numbers(raw, 2, 3, "a position", "[1, 2, 3]"); return new Vector3d(n[0], n[1], n.Length > 2 ? n[2] : 0); },
        [typeof(Quaternion)] = ParseQuaternion,
    };

    private static readonly ConcurrentDictionary<Type, Func<object, object?>?> Discovered = new();

    public static bool TryGet(Type type, out Func<object, object?> parser)
    {
        if (BuiltIn.TryGetValue(type, out parser!)) return true;
        parser = Discovered.GetOrAdd(type, Discover)!;
        return parser != null;
    }

    private static Func<object, object?>? Discover(Type type)
    {
        var implementsInterface = type.GetInterfaces().Any(i => i.IsGenericType
            && i.GetGenericTypeDefinition() == typeof(IDataParsable<>)
            && i.GetGenericArguments()[0] == type);
        if (!implementsInterface) return null;

        var method = type.GetMethod(nameof(IDataParsable<>.ParseData), BindingFlags.Public | BindingFlags.Static, new[] { typeof(object) })
            ?? throw new InvalidOperationException($"{type.FullName} implements IDataParsable but its ParseData isn't public and static.");

        return raw =>
        {
            try { return method.Invoke(null, new[] { raw }); }
            catch (TargetInvocationException e) when (e.InnerException != null)
            { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.InnerException).Throw(); throw; }
        };
    }

    private static object? ParseQuaternion(object raw)
    {
        if (DataParse.IsNumber(raw))
        {
            double degrees = DataParse.Number(raw, "a rotation");
            return Quaternion.CreateFromAxisAngle(Vector3.UnitZ, (float)(degrees * Math.PI / 180.0));
        }

        var n = DataParse.Numbers(raw, 4, 4, "a rotation, degrees/quaternion", "[0, 0, 0.7071, 0.7071]");
        var q = new Quaternion((float) n[0], (float) n[1], (float) n[2], (float) n[3]);
        if (q.LengthSquared() < 1e-12f) throw new DataFormatException("A quaternion of all zeroes isn't a rotation.");
        return Quaternion.Normalize(q);
    }
}
