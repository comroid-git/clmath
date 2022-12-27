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
            Program.SimplePrint = true;

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
            pack.Finalize(Program.BaseContext);
            Program.BaseContext.EnabledUnitPacks.Add(packName);
        }

        [Test]
        public void TestSimpleMultiply()
        {
            const string input = "230V*16A";
            const string output = "3.68kW";

            Assert.AreEqual(output, TestUtil.CalcTest(input).ToString());
        }

        [Test]
        public void TestSimpleDivide_1()
        {
            const string input = "3680W/16A";
            const string output = "230V";

            Assert.AreEqual(output, TestUtil.CalcTest(input).ToString());
        }

        [Test]
        public void TestSimpleDivide_2()
        {
            const string input = "3680W/230V";
            const string output = "16A";

            Assert.AreEqual(output, TestUtil.CalcTest(input).ToString());
        }

        [Test]
        public void TestSiPrefix_1()
        {
            const string input = "1kWh/1h";
            const string output = "1kW";

            Assert.AreEqual(output, TestUtil.CalcTest(input).ToString());
        }

        [Test]
        public void TestSiPrefix_2()
        {
            const string input = "1kWh/1kh";
            const string output = "1W";

            Assert.AreEqual(output, TestUtil.CalcTest(input).ToString());
        }

        [Test]
        public void TestSiPrefix_3()
        {
            const string input = "100*100";
            const string output = "10k";

            Assert.AreEqual(output, TestUtil.CalcTest(input).ToString());
        }

        [Test]
        public void TestSiPrefix_4()
        {
            const string input = "1M?";
            const string output = "1000000";

            Assert.AreEqual(output, TestUtil.CalcTest(input).ToString());
        }

        [Test]
        public void TestSiPrefix_5()
        {
            const string input = "1k*1k*1k";
            const string output = "1G";

            Assert.AreEqual(output, TestUtil.CalcTest(input).ToString());
        }
    }
}