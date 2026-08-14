using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Sandplay.Objects
{
    [CreateAssetMenu(fileName = "ObjectCatalog", menuName = "Sandplay/Object Catalog")]
    public class ObjectCatalog : ScriptableObject
    {
        public List<SandplayObject> Objects = new();

        public IEnumerable<SandplayObject> GetByCategory(ObjectCategory category)
        {
            return Objects.Where(o => o != null && o.Category == category);
        }

        public SandplayObject GetById(string objectId)
        {
            return Objects.FirstOrDefault(o => o != null && o.ObjectId == objectId);
        }

        public IEnumerable<ObjectCategory> GetAvailableCategories()
        {
            return Objects.Where(o => o != null).Select(o => o.Category).Distinct().OrderBy(c => c);
        }
    }
}
