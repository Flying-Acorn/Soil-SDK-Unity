// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedMemberInSuper.Global

using FlyingAcorn.Soil.Economy.Logic;
using FlyingAcorn.Soil.Economy.Models;
using Newtonsoft.Json;

namespace FlyingAcorn.Soil.Economy.Models
{
    public class UserVirtualCurrency : VirtualCurrency, IWithBalance
    {
        /// <summary>
        /// The balance. The server keeps 64-bit balances: one above <c>int.MaxValue</c> reads as <c>int.MaxValue</c>
        /// (decreasing by that much leaves the rest for the next read) instead of failing the whole response.
        /// </summary>
        [JsonConverter(typeof(ClampedIntConverter))]
        public int Balance { get; set; }
    }
}
