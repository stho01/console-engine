using System;

namespace ConsoleEngine.Ecs;

// Ordering constraints between systems in the same SystemGroup. Without them a group runs its systems in Add order.
// A constraint naming a type that isn't in the group is ignored, so a system can be reused in groups without it.

/// <summary>Run this system before every system in the group that is a <see cref="SystemType"/>.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class UpdateBeforeAttribute(Type systemType) : Attribute
{
    public Type SystemType { get; } = systemType;
}

/// <summary>Run this system after every system in the group that is a <see cref="SystemType"/>.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class UpdateAfterAttribute(Type systemType) : Attribute
{
    public Type SystemType { get; } = systemType;
}
