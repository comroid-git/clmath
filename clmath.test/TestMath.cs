﻿using System;
using NUnit.Framework;

namespace clmath.test;

[Parallelizable(ParallelScope.None)]
public sealed class TestMath
{
    [SetUp]
    public void SetUp()
    {
        Program.SetUp();
        Program.SimplePrint = true;
    }

    [Test]
    public void TestPi()
    {
        Assert.AreEqual(Math.PI, TestUtil.CalcTest("pi"), TestUtil.Delta);
    }

    [Test]
    [Repeat(TestUtil.Repeat)]
    public void TestPow()
    {
        var x = TestUtil.Random();
        var y = TestUtil.RandomI();

        Assert.AreEqual(Math.Pow(x, y), TestUtil.CalcTest($"{x}^{y}"), TestUtil.Delta);
    }

    [Test]
    [Repeat(TestUtil.Repeat)]
    public void TestFactorial()
    {
        var x = TestUtil.RNG.Next(12);
        var result = 1;
        for (var rem = x; rem > 0; rem--)
            result *= rem;

        Assert.AreEqual(result, TestUtil.CalcTest($"{x}!"), TestUtil.Delta);
    }

    [Test]
    [Repeat(TestUtil.Repeat)]
    public void TestFraction()
    {
        var x = TestUtil.Random();
        var y = TestUtil.Random();

        Assert.AreEqual(x / y, TestUtil.CalcTest($"frac({x})({y})"), TestUtil.Delta);
    }

    [Test]
    public void TestRad()
    {
        var bak = Program.DRG;
        Program.DRG = CalcMode.Rad;
        Assert.AreEqual(1, TestUtil.CalcTest("sin(90)"), TestUtil.Delta);
        Program.DRG = bak;
    }

    [Test]
    public void TestGrad()
    {
        var bak = Program.DRG;
        Program.DRG = CalcMode.Grad;
        Assert.AreEqual(0.8961922009806601d, TestUtil.CalcTest("sin(1)"), TestUtil.Delta);
        Program.DRG = bak;
    }

    [Test]
    [Repeat(TestUtil.Repeat)]
    public void TestPrecedence_1()
    {
        var x = TestUtil.RNG.Next(255);
        var y = TestUtil.RNG.Next(255);
        var z = TestUtil.RNG.Next(255);

        Assert.AreEqual(x + y * z, TestUtil.CalcTest($"{x}+{y}*{z}"), TestUtil.Delta);
    }

    [Test]
    [Repeat(TestUtil.Repeat)]
    public void TestPrecedence_2()
    {
        var x = TestUtil.RNG.Next(128);
        var y = TestUtil.RNG.Next(64);
        var z = TestUtil.RNG.Next(4);

        Assert.AreEqual(x * Math.Pow(y, z), TestUtil.CalcTest($"{x}*{y}^{z}"), 0.01);
    }

    [Test]
    [Repeat(TestUtil.Repeat)]
    public void TestUnitCast_1()
    {
        var x = TestUtil.RNG.Next(10_000, 100_000);

        Assert.AreEqual((double)x / 63_241, TestUtil.CalcTest($"{x}Au as Ly"), 0.001);
    }
}