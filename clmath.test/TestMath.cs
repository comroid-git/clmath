using NUnit.Framework;

namespace clmath.test;

[Parallelizable(ParallelScope.None)]
public class TestMath
{
    [Test]
    public void TestSquare()
    {
        const string input = "4*4";
        const string output = "16";

        Assert.AreEqual(output, TestUtil.CalcTest(input));
    }

    [Test]
    public void TestRad()
    {
        const string input = "sin(90)";
        const string output = "1";

        var bak = Program.DRG;
        Program.DRG = CalcMode.Rad;
        Assert.AreEqual(output, TestUtil.CalcTest(input));
        Program.DRG = bak;
    }

    [Test]
    public void TestGrad()
    {
        const string input = "sin(1)";
        const string output = "0.8961922009806601";

        var bak = Program.DRG;
        Program.DRG = CalcMode.Grad;
        Assert.AreEqual(output, TestUtil.CalcTest(input));
        Program.DRG = bak;
    }

    [Test]
    public void TestPrecedence_1()
    {
        const string input = "1+2*3";
        const string output = "7";

        Assert.AreEqual(output, TestUtil.CalcTest(input));
    }

    [Test]
    public void TestPrecedence_2()
    {
        const string input = "3*2^2";
        const string output = "15";

        Assert.AreEqual(output, TestUtil.CalcTest(input));
    }
}