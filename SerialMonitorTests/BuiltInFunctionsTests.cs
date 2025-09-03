namespace SerialMonitor.Tests
{
    [TestClass()]
    public class BuiltInFunctionsTests
    {
        [TestMethod()]
        public void Crc16Test()
        {
            HexDataCollection repeaterHexMap = new HexDataCollection();
            repeaterHexMap.TryAdd(HexData.Create("$6 0x08 0x43 $1 $2 $5 $3 $4 @crc16"),
                HexData.Create("$6 0x0A 0x63 $1 $2 0x03 0xC2 0x35 $3 $4 @crc16"));

            byte[] incoming = [0x00, 0x08, 0x43, 0x00, 0x00, 0x00, 0x00, 0x00, 0xC7, 0x38];
            byte[] expected = [0x00, 0x0A, 0x63, 0x00, 0x00, 0x03, 0xC2, 0x35, 0x00, 0x00, 0x21, 0x2C];

            if (!repeaterHexMap.TryGetValue(incoming, incoming.Length, out var computed))
                Assert.Fail();

            if (!expected.SequenceEqual(computed))
                Assert.Fail();
        }

        [TestMethod()]
        public void SumTest()
        {
            HexDataCollection repeaterHexMap = new HexDataCollection();
            repeaterHexMap.TryAdd(HexData.Create("0x10 0x58 $1 0x5B 0x16"),
                HexData.Create("0x68 0x0D 0x0D 0x68 0x08 $1 0x00 0x04 0xA0 0x00 0xB1 0x00 0xA0 0x00 0x10 0x20 0x01 @sum[3..] 0x16"));

            byte[] incoming = [0x10, 0x58, 0xFC, 0x5B, 0x16];
            byte[] expected = [0x68, 0x0D, 0x0D, 0x68, 0x08, 0xFC, 0x00, 0x04, 0xA0, 0x00, 0xB1, 0x00, 0xA0, 0x00, 0x10, 0x20, 0x01, 0x92, 0x16];

            if (!repeaterHexMap.TryGetValue(incoming, incoming.Length, out var computed))
                Assert.Fail();

            if (!expected.SequenceEqual(computed))
                Assert.Fail();
        }

        [TestMethod()]
        public void RandTest()
        {
            HexDataCollection repeaterHexMap = new HexDataCollection();
            repeaterHexMap.TryAdd(HexData.Create("0x10 0x58 $1 0x5B 0x16"),
                HexData.Create("0x68 0x0D 0x0D 0x68 0x08 $1 0x00 0x04 0xA0 0x00 0xB1 0x00 0xA0 0x00 @rand[40..130] @rand[20..30] 0x01 @sum[3..] 0x16"));

            byte[] incoming = [0x10, 0x58, 0xFC, 0x5B, 0x16];

            if (!repeaterHexMap.TryGetValue(incoming, incoming.Length, out var computed))
                Assert.Fail();

            if (computed[14] < 40 || computed[14] > 130)
                Assert.Fail();
            if (computed[15] < 20 || computed[15] > 30)
                Assert.Fail();
        }
    }
}