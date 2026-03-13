// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Reflection;

namespace SixLabors.ImageSharp.Tests;

/// <summary>
/// xUnit v3 compatible replacement for <c>Microsoft.DotNet.XUnitExtensions.ConditionalFactAttribute</c>.
/// Skips the test when the specified static member on the given type evaluates to <see langword="false"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class ConditionalFactAttribute : FactAttribute
{
    public ConditionalFactAttribute(Type conditionType, string conditionMember)
    {
        if (!EvaluateCondition(conditionType, conditionMember))
        {
            this.Skip = $"Condition '{conditionType.Name}.{conditionMember}' is false";
        }
    }

    private static bool EvaluateCondition(Type type, string memberName)
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;

        PropertyInfo prop = type.GetProperty(memberName, flags);
        if (prop != null)
        {
            return (bool?)prop.GetValue(null) ?? false;
        }

        FieldInfo field = type.GetField(memberName, flags);
        if (field != null)
        {
            return (bool?)field.GetValue(null) ?? false;
        }

        MethodInfo method = type.GetMethod(memberName, flags, null, Type.EmptyTypes, null);
        if (method != null)
        {
            return (bool?)method.Invoke(null, null) ?? false;
        }

        return false;
    }
}
