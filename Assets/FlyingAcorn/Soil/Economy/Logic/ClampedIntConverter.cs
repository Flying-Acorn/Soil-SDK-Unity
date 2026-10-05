using System;
using System.Globalization;
using System.Numerics;
using Newtonsoft.Json;

namespace FlyingAcorn.Soil.Economy.Logic
{
    /// <summary>
    /// Reads a whole number into an <c>int</c>, clamping it to <c>int.MinValue</c>..<c>int.MaxValue</c> instead of
    /// failing. The server keeps balances as 64-bit numbers: rewards it grants (referrals, leaderboards) can take a
    /// balance past <c>int.MaxValue</c>, and one such balance must not make a whole summary unreadable. A balance
    /// read as <c>int.MaxValue</c> may be larger: decreasing it by that much leaves the rest for the next read.
    /// </summary>
    public sealed class ClampedIntConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => objectType == typeof(int) || objectType == typeof(int?);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue,
            JsonSerializer serializer)
        {
            switch (reader.TokenType)
            {
                case JsonToken.Null:
                case JsonToken.Undefined:
                    return objectType == typeof(int?) ? null : 0;
                case JsonToken.Integer:
                    return reader.Value switch
                    {
                        BigInteger big => big.Sign < 0 ? int.MinValue : int.MaxValue,
                        var value => Clamp(Convert.ToInt64(value, CultureInfo.InvariantCulture)),
                    };
                case JsonToken.Float:
                    return Clamp(Convert.ToDouble(reader.Value, CultureInfo.InvariantCulture));
                case JsonToken.String:
                    var text = (string)reader.Value;
                    if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var whole))
                        return Clamp(whole);
                    if (BigInteger.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var huge))
                        return huge.Sign < 0 ? int.MinValue : int.MaxValue;
                    throw new JsonSerializationException($"Could not read '{text}' as a whole number.");
                default:
                    throw new JsonSerializationException($"Unexpected {reader.TokenType} for a whole number.");
            }
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value == null) writer.WriteNull();
            else writer.WriteValue((int)value);
        }

        public static int Clamp(long value) =>
            value > int.MaxValue ? int.MaxValue : value < int.MinValue ? int.MinValue : (int)value;

        private static int Clamp(double value) =>
            double.IsNaN(value) ? 0 : value >= int.MaxValue ? int.MaxValue : value <= int.MinValue ? int.MinValue : (int)value;
    }
}
