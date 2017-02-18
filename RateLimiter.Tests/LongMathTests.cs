using NUnit.Framework;

namespace Guava.RateLimiter.Tests
{
    [TestFixture]
    public class LongMathTests
    {
        [Test]
        public void TestSaturatedAdd()
        {
            Assert.AreEqual(long.MaxValue, LongMath.SaturatedAdd(long.MaxValue, long.MaxValue));
            Assert.AreEqual(long.MaxValue, LongMath.SaturatedAdd(long.MaxValue, 0));
            Assert.AreEqual(long.MaxValue, LongMath.SaturatedAdd(long.MaxValue, 1));

            Assert.AreEqual(long.MinValue, LongMath.SaturatedAdd(long.MinValue, long.MinValue));
            Assert.AreEqual(long.MinValue, LongMath.SaturatedAdd(long.MinValue, 0));
            Assert.AreEqual(long.MinValue, LongMath.SaturatedAdd(long.MinValue, -1));

            Assert.AreEqual(-1, LongMath.SaturatedAdd(long.MaxValue, long.MinValue));
            Assert.AreEqual(long.MinValue + 1, LongMath.SaturatedAdd(long.MinValue, 1));
            Assert.AreEqual(long.MaxValue - 1, LongMath.SaturatedAdd(long.MaxValue, -1));
        }
    }
}