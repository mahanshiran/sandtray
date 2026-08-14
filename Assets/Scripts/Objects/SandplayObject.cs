using UnityEngine;

namespace Sandplay.Objects
{
    public enum ObjectCategory
    {
        People,
        Animals,
        Buildings,
        Nature,
        Vehicles,
        Abstract,
        Custom
    }

    [CreateAssetMenu(fileName = "SandplayObject", menuName = "Sandplay/Sandplay Object")]
    public class SandplayObject : ScriptableObject
    {
        public string DisplayName;
        public ObjectCategory Category;
        public GameObject Prefab;
        public Sprite Thumbnail;
        [TextArea] public string Description;
        public string[] Tags;

        /// <summary>
        /// Unique identifier for serialization. Auto-set from asset name if empty.
        /// </summary>
        public string ObjectId;

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(ObjectId))
                ObjectId = name;
        }
    }
}
