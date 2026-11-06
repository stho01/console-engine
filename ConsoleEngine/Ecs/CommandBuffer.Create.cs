namespace ConsoleEngine.Ecs;

// Deferred Create overloads for 1-6 components, mirroring World.Create. Each returns a deferred handle (see the
// CommandBuffer remarks). Keep them identical apart from their arity.
public sealed partial class CommandBuffer
{
    public Entity Create<T1>(in T1 c1) where T1 : unmanaged
    {
        var entity = BeginCreate([ComponentType<T1>.Id]);
        WriteComponent(c1);
        return entity;
    }

    public Entity Create<T1, T2>(in T1 c1, in T2 c2) where T1 : unmanaged where T2 : unmanaged
    {
        var entity = BeginCreate([ComponentType<T1>.Id, ComponentType<T2>.Id]);
        WriteComponent(c1);
        WriteComponent(c2);
        return entity;
    }

    public Entity Create<T1, T2, T3>(in T1 c1, in T2 c2, in T3 c3)
        where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged
    {
        var entity = BeginCreate([ComponentType<T1>.Id, ComponentType<T2>.Id, ComponentType<T3>.Id]);
        WriteComponent(c1);
        WriteComponent(c2);
        WriteComponent(c3);
        return entity;
    }

    public Entity Create<T1, T2, T3, T4>(in T1 c1, in T2 c2, in T3 c3, in T4 c4)
        where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged
    {
        var entity = BeginCreate(
            [ComponentType<T1>.Id, ComponentType<T2>.Id, ComponentType<T3>.Id, ComponentType<T4>.Id]);
        WriteComponent(c1);
        WriteComponent(c2);
        WriteComponent(c3);
        WriteComponent(c4);
        return entity;
    }

    public Entity Create<T1, T2, T3, T4, T5>(in T1 c1, in T2 c2, in T3 c3, in T4 c4, in T5 c5)
        where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged
    {
        var entity = BeginCreate(
        [
            ComponentType<T1>.Id, ComponentType<T2>.Id, ComponentType<T3>.Id, ComponentType<T4>.Id,
            ComponentType<T5>.Id
        ]);
        WriteComponent(c1);
        WriteComponent(c2);
        WriteComponent(c3);
        WriteComponent(c4);
        WriteComponent(c5);
        return entity;
    }

    public Entity Create<T1, T2, T3, T4, T5, T6>(in T1 c1, in T2 c2, in T3 c3, in T4 c4, in T5 c5, in T6 c6)
        where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged
        where T6 : unmanaged
    {
        var entity = BeginCreate(
        [
            ComponentType<T1>.Id, ComponentType<T2>.Id, ComponentType<T3>.Id, ComponentType<T4>.Id,
            ComponentType<T5>.Id, ComponentType<T6>.Id
        ]);
        WriteComponent(c1);
        WriteComponent(c2);
        WriteComponent(c3);
        WriteComponent(c4);
        WriteComponent(c5);
        WriteComponent(c6);
        return entity;
    }
}
