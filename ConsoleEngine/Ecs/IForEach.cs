namespace ConsoleEngine.Ecs;

// Per-entity callbacks for Query.ForEach. Implement them on a struct: the JIT then specializes ForEach for the
// struct and inlines Execute, which a delegate can't do.

public interface IForEach<T1>
{
    void Execute(ref T1 c1);
}

public interface IForEach<T1, T2>
{
    void Execute(ref T1 c1, ref T2 c2);
}

public interface IForEach<T1, T2, T3>
{
    void Execute(ref T1 c1, ref T2 c2, ref T3 c3);
}
