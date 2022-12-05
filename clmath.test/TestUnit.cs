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
            var volts = pack.Get("V");
            var ampere = pack.Get("A");
            var watts = pack.Get("W");
            var hours = pack.Get("h");
            var watthours = pack.Get("Wh");
            volts.AddProduct(ampere, watts);
            watthours.AddQuotient(hours, watts);
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