using System;
using System.Reflection;

namespace CasualtiesTogetherAddons.Networking;

// Check only the API shapes we use, without executing upstream code or pinning a version.
internal static class RejoinCompatibility
{
    internal static MethodInfo RequireStaticMethod(Type type, string name, Type result, params Type[] parameters)
    {
        var method = type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static
            | BindingFlags.DeclaredOnly, null, parameters, null);
        if (method == null || method.ReturnType != result || method.ContainsGenericParameters)
            throw new NotSupportedException($"Rejoin requires {type.FullName}.{name} with a compatible signature.");
        return method;
    }

    internal static void RequireStaticField(Type type, string name, Type fieldType)
    {
        var field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static
            | BindingFlags.DeclaredOnly);
        if (field == null || field.FieldType != fieldType || field.IsInitOnly || field.IsLiteral)
            throw new NotSupportedException($"Rejoin requires writable {type.FullName}.{name} of type {fieldType.Name}.");
    }
}
