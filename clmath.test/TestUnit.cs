using NUnit.Framework;

namespace clmath.test;

[Parallelizable(ParallelScope.None)]
public sealed class TestUnit
{
    [SetUp]
    public void SetUp()
    {
        Program.SetUp();
        // todo fixme: needs static setup of units
    }

    [Test]
    public void TestSimpleMultiply()
    {
        const string input = "230[V]*16[A]";
        const string output = "3680[W]";

        Assert.AreEqual(output, TestUtil.CalcTest(input));
    }
}