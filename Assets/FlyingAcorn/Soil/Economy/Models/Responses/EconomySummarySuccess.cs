// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedMemberInSuper.Global
#pragma warning disable IDE1006

using System.Collections.Generic;

namespace FlyingAcorn.Soil.Economy.Models.Responses
{
    public class EconomySummarySuccess
    {
        public List<UserVirtualCurrency> virtual_currencies { get; set; }
        public List<UserInventoryItem> inventory_items { get; set; }
    }
}
