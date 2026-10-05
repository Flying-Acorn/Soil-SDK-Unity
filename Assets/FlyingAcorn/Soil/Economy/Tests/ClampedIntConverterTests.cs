using System.Collections.Generic;
using FlyingAcorn.Soil.Economy.Logic;
using Newtonsoft.Json;
using NUnit.Framework;

namespace FlyingAcorn.Soil.Economy.Tests
{
    public class ClampedIntConverterTests
    {
        // Shaped like UserVirtualCurrency, which lives in the engine-bound assembly tests cannot reference.
        private class Currency
        {
            public string Identifier { get; set; }

            [JsonConverter(typeof(ClampedIntConverter))]
            public int Balance { get; set; }
        }

        private class Summary
        {
            public List<Currency> virtual_currencies { get; set; }
        }

        private static int Read(string balance) =>
            JsonConvert.DeserializeObject<Currency>($"{{\"identifier\": \"gem\", \"balance\": {balance}}}").Balance;

        [TestCase("0", 0)]
        [TestCase("50", 50)]
        [TestCase("2147483647", int.MaxValue)]
        [TestCase("2147483648", int.MaxValue)]
        [TestCase("42949672950", int.MaxValue)]
        [TestCase("9223372036854775807", int.MaxValue)]
        [TestCase("99999999999999999999999", int.MaxValue)]
        [TestCase("-2147483649", int.MinValue)]
        [TestCase("-99999999999999999999999", int.MinValue)]
        [TestCase("12.0", 12)]
        [TestCase("\"3000000000\"", int.MaxValue)]
        [TestCase("\"7\"", 7)]
        [TestCase("null", 0)]
        public void BalancesAboveIntAreClampedInsteadOfFailing(string json, int expected)
        {
            Assert.AreEqual(expected, Read(json));
        }

        [Test]
        public void OneHugeBalanceDoesNotSpoilTheOthers()
        {
            var summary = JsonConvert.DeserializeObject<Summary>(
                "{\"virtual_currencies\": [{\"identifier\": \"ReferralGem\", \"balance\": 4294967294}, " +
                "{\"identifier\": \"gem\", \"balance\": 120}]}");
            Assert.AreEqual(2, summary.virtual_currencies.Count);
            Assert.AreEqual(int.MaxValue, summary.virtual_currencies[0].Balance);
            Assert.AreEqual(120, summary.virtual_currencies[1].Balance);
        }

        [Test]
        public void WritesAndReadsBackThePlainNumber()
        {
            // The local cache writes the models with the same converter.
            var json = JsonConvert.SerializeObject(new Currency { Identifier = "gem", Balance = 1234 });
            Assert.AreEqual("{\"Identifier\":\"gem\",\"Balance\":1234}", json);
            Assert.AreEqual(1234, JsonConvert.DeserializeObject<Currency>(json).Balance);
        }

        [Test]
        public void TextThatIsNotANumberStillFails()
        {
            Assert.Throws<JsonSerializationException>(() => Read("\"lots\""));
            Assert.Throws<JsonSerializationException>(() => Read("true"));
        }

        [Test]
        public void ClampKeepsValuesInRange()
        {
            Assert.AreEqual(5, ClampedIntConverter.Clamp(5));
            Assert.AreEqual(int.MaxValue, ClampedIntConverter.Clamp(long.MaxValue));
            Assert.AreEqual(int.MinValue, ClampedIntConverter.Clamp(long.MinValue));
        }
    }
}
