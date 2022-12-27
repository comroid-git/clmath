using NUnit.Framework;

namespace clmath.test
{
    [Parallelizable(ParallelScope.None)]
    public sealed class TestUnit
    {
        [SetUp]
        public void SetUp()
        {
            Program.SetUp();

            // doesnt matter if these units are already defined; newer information just overrides
            const string packName = "electric";
            var pack = Program.unitPackages[packName] = new UnitPackage(packName);
            var volts = pack.CreateOrGet(string.Empty, "V");
            var ampere = pack.CreateOrGet(string.Empty, "A");
            var watts = pack.CreateOrGet(string.Empty, "W");
            var hours = pack.CreateOrGet(string.Empty, "h");
            var wattHours = pack.CreateOrGet(string.Empty, "Wh");
            UnitPackage.AddEval(volts, ampere, watts, Component.Operator.Multiply);
            UnitPackage.AddEval(wattHours, hours, watts, Component.Operator.Divide);
            Program.BaseContext.EnabledUnitPacks.Add(packName);
        }

        [Test]
        public void TestSimpleMultiply()
        {
            const string input = "230[V]*16[A]";
            const string output = "3.68[kW]";

            Assert.AreEqual(output, TestUtil.CalcTest(input));
        }

        [Test]
        public void TestSimpleDivide_1()
        {
            const string input = "3680[W]/16[A]";
            const string output = "230[V]";

            Assert.AreEqual(output, TestUtil.CalcTest(input));
        }

        [Test]
        public void TestSimpleDivide_2()
        {
            const string input = "3680[W]/230[V]";
            const string output = "16[A]";

            Assert.AreEqual(output, TestUtil.CalcTest(input));
        }

        [Test]
        public void TestSiPrefix_1()
        {
            const string input = "1[kWh]/1[h]";
            const string output = "1[kW]";

            Assert.AreEqual(output, TestUtil.CalcTest(input));
        }

        [Test]
        public void TestSiPrefix_2()
        {
            const string input = "1[kWh]/1[kh]";
            const string output = "1[W]";

            Assert.AreEqual(output, TestUtil.CalcTest(input));
        }

        [Test]
        public void TestSiPrefix_3()
        {
            const string input = "100*100";
            const string output = "10000";

            Assert.AreEqual(output, TestUtil.CalcTest(input));
        }
    }
}