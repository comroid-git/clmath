using System;
using System.IO;
using NUnit.Framework;

namespace clmath.test;

[Parallelizable(ParallelScope.None)]
public class TestSolver
{
    [Test]
    public void TestSquare()
    {
        const string input = "x^2";
        const string output = "sqrt(y)";

        Assert.AreEqual(output, TestUtil.SolverTest(input, "x", "y"));
    }
    
    [Test]
    public void TestQuadric()
    {
        const string input = "x^3";
        const string output = "root[3](y)";

        Assert.AreEqual(output, TestUtil.SolverTest(input, "x", "y"));
    }

    [Test]
    public void TestPythagoras()
    {
        const string input = "sqrt(a^2+b^2)";
        const string output = "sqrt(c^2-a^2)";

        Assert.AreEqual(output, TestUtil.SolverTest(input, "b", "c"));
    }
    
    [Test]
    public void TestACos()
    {
        const string input = "acos(P/S)";
        const string output = "cos(p)*S";

        Assert.AreEqual(output, TestUtil.SolverTest(input, "P", "p"));
    }

    [Test]
    public void TestAdvanced_1()
    {
        const string input = "(x^3)/5";
        const string output = "root[3](y*5)";

        Assert.AreEqual(output, TestUtil.SolverTest(input, "x", "y"));
    }

    [Test]
    public void TestAdvanced_2()
    {
        const string input = "frac(XL)(2*pi*f)";
        const string output = "XL/L/2*pi";

        Assert.AreEqual(output, TestUtil.SolverTest(input, "f", "L"));
    }
}