namespace Stratosphere.Core.ECS.Systems;


/// <summary>
/// The interface of an entity system.
/// Systems are behind most of the actual functionality of an entity.
/// </summary>
public interface IEntitySystem
{
    public void Initialize();
    public void Update(float deltaTime);
}
