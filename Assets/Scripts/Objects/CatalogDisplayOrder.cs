using System;
using System.Collections.Generic;

namespace Sandplay.Objects
{
    /// <summary>Default browsing order for broad scene-building usefulness, not clinical significance.</summary>
    public static class CatalogDisplayOrder
    {
        private static readonly string[] Categories =
            { "people", "buildings", "nature", "animals", "abstract", "vehicles", "mythological", "religious", "custom" };

        private static readonly Dictionary<string, Dictionary<string, int>> Priorities =
            new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase)
            {
                ["people"] = Rank(
                    "Man Casual 1|Woman in Long Sleeves|Man Casual 2|Woman in Dress|Man in Long Sleeves|Man in Suit",
                    "King|Archer|Astronaut|SWAT|Witch|Scarecrow|Alien|Zombie"),
                ["buildings"] = Rank(
                    "House|Wall|Fence|Road|Farm House|Apartment|Schoolhouse|Hospital|Bridge1|Door and frame|Gate|Tent|Treehouse|Barn|Well|Fountain|Castle|Lighthouse|Hotel|Hostel|Fireplace|Staircase|Archway|Gazebo|Bank|Windmill",
                    "Radio tower|Water tower|Silo|Billboard|Factory|Club|Bar|Coliseum|Pyramid"),
                ["nature"] = Rank(
                    "Tree|Apple tree|Evergreen Tree|Willow tree|River|Pond|Mountain|Rock|Stone|Sun|Moon|Cloud|Rainbow|Ocean|Waterfall|Island|Rose|Sunflower|Tuft of grass|Bamboo|Star|Water lilies|Seashell|Birds nest|Tree roots|Tree stump|Dead tree",
                    "Kelp|Icicle|Haystack|Cattail|Coconut"),
                ["animals"] = Rank(
                    "Dog|Cat|Rabbit|Horse|Cow|Sheep|Pig|Goat|Hen|Duck|Butterfly|Sparrow|Turtle|Goldfish|Deer|Bear|Lion|Tiger|Elephant|Wolf|Snake|Fox|Owl|Eagle|Dolphin|Whale|Panda|Monkey|Giraffe|Zebra|Crocodile|Shark|Dinosaur|T-Rex",
                    "Grasshopper|Armadillo|Bison|Llama|Seahorse|Jellyfish"),
                ["abstract"] = Rank(
                    "Heart|Chair|Table|Bed|Crib|Baby bottle|Couch|Blankets|Pillow|Book|Key|Mirror|Candle|Campfire|Cup of tea|Phone|Picture frame|Basket|Crayons|Easel|Palette|Swing set|Slide|Soccer ball|Balloon|Cake|Ring|Crown|Ladder|Shovel|Bucket|Map|Bridge|Bird Cage|Life preserver|Crutches|Walking stick|Coffin|Gun|Bomb",
                    "Satellite|Oil pump|Road Lane Markings|Divided Highway|Telephone pole|Printer|Cash register|Sewing machine|Bowling Pins|Concert Stage|Microphone|Trumpet|Trident"),
                ["vehicles"] = Rank(
                    "Bicycle|Bus|Van|Taxi|Ambulance|Police Car|Wheelchair|Boat|Airplane|Truck|Tractor|Tricycle|Scooter|Motorcycle|Caravan|Bullet train|Wagon|Kayak|Helicopter",
                    "Jetpack|Space rover|Space Shuttle|Rocket ship|UFO|Jet|Airship"),
                ["mythological"] = Rank("Dragon|Unicorn|Magic wand|Robot|Beanstalk", ""),
                ["religious"] = Rank("Church|Temple|Pagoda|Cemetery Scene", ""),
            };

        private static Dictionary<string, int> Rank(string common, string specialized)
        {
            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            Add(common, 0);
            Add(specialized, 2000);
            return result;
            void Add(string names, int start)
            {
                foreach (var name in names.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries))
                    result[name] = start++;
            }
        }

        private static string Category(string value) => (value ?? "").Trim().ToLowerInvariant();

        public static int CompareCategories(string a, string b)
        {
            a = Category(a); b = Category(b);
            int aRank = Array.IndexOf(Categories, a), bRank = Array.IndexOf(Categories, b);
            if (aRank < 0) aRank = Categories.Length;
            if (bRank < 0) bRank = Categories.Length;
            int rank = aRank.CompareTo(bRank);
            return rank != 0 ? rank : StringComparer.Ordinal.Compare(a, b);
        }

        private static int ItemRank(NetworkCatalogItem item) =>
            Priorities.TryGetValue(Category(item.category), out var names) &&
            names.TryGetValue(item.display_name ?? "", out int rank) ? rank : 1000;

        private static int SmartRank(NetworkCatalogItem item)
        {
            if (item?.id != null && item.id.StartsWith("smart.", StringComparison.Ordinal) &&
                LinearObjectState.TryParse(item.id, out var state))
                return Array.IndexOf(SmartLinearCatalog.DefinitionIds, state.DefinitionId);
            return int.MaxValue;
        }

        // Specific category tabs retain alphabetical browsing after the pinned
        // local structures. All retains the broader curated usefulness order.
        public static int CompareWithinCategory(NetworkCatalogItem a, NetworkCatalogItem b)
        {
            if (ReferenceEquals(a, b)) return 0;
            if (a == null) return 1;
            if (b == null) return -1;
            int rank = SmartRank(a).CompareTo(SmartRank(b));
            if (rank == 0) rank = StringComparer.OrdinalIgnoreCase.Compare(a.display_name, b.display_name);
            return rank != 0 ? rank : StringComparer.Ordinal.Compare(a.id, b.id);
        }

        public static int CompareItems(NetworkCatalogItem a, NetworkCatalogItem b)
        {
            if (ReferenceEquals(a, b)) return 0;
            if (a == null) return 1;
            if (b == null) return -1;
            int rank = CompareCategories(a.category, b.category);
            if (rank == 0) rank = SmartRank(a).CompareTo(SmartRank(b));
            if (rank == 0) rank = ItemRank(a).CompareTo(ItemRank(b));
            if (rank == 0) rank = StringComparer.OrdinalIgnoreCase.Compare(a.display_name, b.display_name);
            return rank != 0 ? rank : StringComparer.Ordinal.Compare(a.id, b.id);
        }
    }
}
