namespace Stratosphere.Core.ECS.Systems;

public abstract partial class EntitySystem : IEntitySystem
{
    public abstract void Initialize();
    public abstract void Update(float deltaTime);

    /* and any other useful little tidbits that the interface wouldnt touch */
}
