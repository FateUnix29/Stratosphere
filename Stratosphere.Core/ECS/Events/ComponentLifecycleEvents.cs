namespace Stratosphere.Core.ECS.Events;

/* Unlike most events, these are aimed at one specific component, not the entity as a whole.
   Subscribe to ComponentStartup with TComp = TransformComponent and you hear about transforms starting, and only transforms,
   even though the entity has plenty of other components that started too. */

/* Unlike most events, these are aimed at a specific component instead of the entire entity.
   Subscribe to ComponentStartup with TComp = TransformComponent and you hear about transforms starting! */

/// <summary>
/// Raised after a component has been added to an entity and initialized.
/// By this point, every other component the entity spawned with should also be present and initialized, so it is safe to look at them.
/// </summary>
public struct ComponentStartup : IEvent { }

/// <summary>
/// Raised as a component is shutting down, just before it's removed from an entity. Whether by being removed directly, or because said entity is being destroyed.
/// Regardless, the component is about to cease to exist.
/// The component and its siblings are still present while this is handled. The entity itself may, however, already begin to report it no longer exists.
/// </summary>
public struct ComponentShutdown : IEvent { }
