// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedMemberInSuper.Global

using FlyingAcorn.Soil.Economy.Models;

namespace FlyingAcorn.Soil.Economy.Models
{
    public class UserVirtualCurrency : VirtualCurrency, IWithBalance
    {
        public int Balance { get; set; }
    }
}
