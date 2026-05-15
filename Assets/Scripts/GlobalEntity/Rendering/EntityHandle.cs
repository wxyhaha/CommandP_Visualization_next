using UnityEngine;

namespace CommandP.GlobalEntity.Rendering
{
    /// <summary>
    /// Attached to EntityRoot so any child transform can resolve back to the entity ID.
    /// Used by raycast selection, debug overlays, etc.
    /// </summary>
    public class EntityHandle : MonoBehaviour
    {
        public string ObjectId;
        public EntityType EntityType;
    }
}
