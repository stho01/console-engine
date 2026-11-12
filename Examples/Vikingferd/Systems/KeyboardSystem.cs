using ConsoleEngine.Ecs;

namespace Vikingferd.Systems;

/// <summary>Reads the keyboard into <see cref="Expedition.Input"/>. First in the simulation, so tests can skip it.</summary>
public sealed class KeyboardSystem(Expedition expedition) : EntitySystem
{
    protected override void OnUpdate(float deltaSeconds) => expedition.Input = PartyInput.FromKeyboard();
}
