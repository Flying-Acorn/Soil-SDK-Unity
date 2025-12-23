// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedMemberInSuper.Global

using FlyingAcorn.Soil.Economy.Models;

namespace FlyingAcorn.Soil.Economy.Models
{
    public class UserInventoryItem : InventoryItem, IWithBalance
    {
        public int Balance { get; set; }
    }
}
