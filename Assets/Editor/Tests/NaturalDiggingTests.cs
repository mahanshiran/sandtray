using NUnit.Framework;
using Sandplay.Sand;

namespace Sandplay.Tests
{
    public class NaturalDiggingTests
    {
        [Test]
        public void DigProfileHasSoftRimAndSmoothMonotonicShoulder()
        {
            Assert.AreEqual(1, SandToolController.DigFalloff(0));
            Assert.AreEqual(0, SandToolController.DigFalloff(1));
            Assert.AreEqual(0, SandToolController.DigFalloff(1.2f));
            Assert.Less(SandToolController.DigFalloff(.99f), .00001f);
            float previous = 1;
            for (int i = 1; i <= 100; i++)
            {
                float value = SandToolController.DigFalloff(i / 100f);
                Assert.LessOrEqual(value, previous);
                Assert.GreaterOrEqual(value, 0); previous = value;
            }
        }
    }
}
