namespace ConsoleEngine.Ecs;

/// <summary>
/// Which archetypes a <see cref="Query"/> matches: those with every <c>With</c> component and no <c>Without</c>
/// component. Immutable: each call returns a new description.
/// </summary>
public readonly struct QueryDescription
{
    //**********************************************************
    //** fields
    //**********************************************************

    internal readonly ComponentMask All;
    internal readonly ComponentMask None;

    //**********************************************************
    //** ctor
    //**********************************************************

    private QueryDescription(ComponentMask all, ComponentMask none)
    {
        All = all;
        None = none;
    }

    //**********************************************************
    //** public methods
    //**********************************************************

    public QueryDescription With<T>() where T : unmanaged => new(All.With(ComponentType<T>.Id), None);

    public QueryDescription With<T1, T2>() where T1 : unmanaged where T2 : unmanaged => With<T1>().With<T2>();

    public QueryDescription With<T1, T2, T3>() where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged =>
        With<T1>().With<T2>().With<T3>();

    public QueryDescription Without<T>() where T : unmanaged => new(All, None.With(ComponentType<T>.Id));
}
