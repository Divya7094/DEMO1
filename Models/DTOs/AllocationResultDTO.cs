// public class AllocationResultDTO
// {
//     public double Equity { get; set; }
//     public double FixedIncome { get; set; }
//     public double Commodities { get; set; }
//     public double Cash { get; set; }
//     public double RealEstate { get; set; }

//     public Dictionary<string, Dictionary<string, double>> SubAllocations { get; set; }

// }

using System.Collections.Generic;
 
namespace WisVestAPI.Models.DTOs
{
    public class AllocationResultDTO
    {
        public Dictionary<string, AssetAllocation> Assets { get; set; } = new Dictionary<string, AssetAllocation>();
    }
 
    public class AssetAllocation
    {
        public double Percentage { get; set; }
        public Dictionary<string, double> SubAssets { get; set; } = new Dictionary<string, double>();
    }
}