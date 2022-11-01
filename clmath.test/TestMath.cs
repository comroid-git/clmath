using NUnit.Framework;

namespace clmath.test;

[Parallelizable(ParallelScope.None)]
public class TestMath
{
    [SetUp]
    public void SetUp() => Program.SetUp();
    
    [Test]
    public void TestSquare()
    {
        const string input = "4^2";
        const string output = "16";

        Assert.AreEqual(output, TestUtil.CalcTest(input));
    }
    
    [Test]
    public void TestCubic()
    {
        const string input = "4^3";
        const string output = "64";

        Assert.AreEqual(output, TestUtil.CalcTest(input));
    }
    
    [Test]
    public void TestFactorial()
    {
        const string input = "5!";
        const string output = "120";

        Assert.AreEqual(output, TestUtil.CalcTest(input));
    }
    
    [Test]
    public void TestFraction()
    {
        const string input = "frac(1)(2)";
        const string output = "0.5";

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
        const string output = "12";

        Assert.AreEqual(output, TestUtil.CalcTest(input));
    }
}