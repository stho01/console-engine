namespace Asteroids.Ecs.Components;

// Tags: no fields, so they cost no memory per entity. They only put entities in their own archetype, which queries
// can select with With<T>() or skip with Without<T>().

public struct Asteroid;

public struct Bullet;
