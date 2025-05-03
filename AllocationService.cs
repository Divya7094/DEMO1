using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using WisVestAPI.Models;
using System.Threading.Tasks;
using WisVestAPI.Models.DTOs;
using WisVestAPI.Models.Matrix;
using WisVestAPI.Repositories.Matrix;
using WisVestAPI.Services.Interfaces;
 
namespace WisVestAPI.Services
{
    public class AllocationService : IAllocationService
    {
        private readonly MatrixRepository _matrixRepository;
 
        public AllocationService(MatrixRepository matrixRepository)
        {
            _matrixRepository = matrixRepository;
        }
 
        private const string CashKey = "cash";
        private const string EquityKey = "equity";
        private const string FixedIncomeKey = "fixedIncome";
        private const string CommoditiesKey = "commodities";
        private const string RealEstateKey = "realEstate";
 
        public async Task<Dictionary<string, object>> CalculateFinalAllocation(UserInputDTO input)
        {
            Console.WriteLine("Starting allocation calculation...");
 
            // Load the allocation matrix
            var allocationMatrix = await _matrixRepository.LoadMatrixDataAsync();
            if (allocationMatrix == null)
            {
                Console.WriteLine("Error: Allocation matrix is null.");
                throw new InvalidOperationException("Allocation matrix data is null.");
            }
            Console.WriteLine("Allocation matrix loaded successfully.");
 
            // Step 1: Map input values to match JSON keys
            var riskToleranceMap = new Dictionary<string, string>
            {
                { "Low", "Low" },
                { "Medium", "Mid" },
                { "High", "High" }
            };
 
            var investmentHorizonMap = new Dictionary<string, string>
            {
                { "Short", "Short" },
                { "Moderate", "Mod" },
                { "Long", "Long" }
            };
 
            var riskToleranceKey = riskToleranceMap[input.RiskTolerance ?? throw new ArgumentException("RiskTolerance is required")];
            var investmentHorizonKey = investmentHorizonMap[input.InvestmentHorizon ?? throw new ArgumentException("InvestmentHorizon is required")];
            var riskHorizonKey = $"{riskToleranceKey}+{investmentHorizonKey}";
 
            Console.WriteLine($"Looking up base allocation for key: {riskHorizonKey}");
 
            // Step 2: Determine base allocation
            if (!allocationMatrix.Risk_Horizon_Allocation.TryGetValue(riskHorizonKey, out var baseAllocation))
            {
                Console.WriteLine($"Error: No base allocation found for key: {riskHorizonKey}");
                throw new ArgumentException($"Invalid combination of RiskTolerance and InvestmentHorizon: {riskHorizonKey}");
            }
            Console.WriteLine($"Base allocation found: {JsonSerializer.Serialize(baseAllocation)}");
 
            // Clone the base allocation to avoid modifying the original matrix
            var finalAllocation = new Dictionary<string, double>(baseAllocation);
 
            // Step 3: Apply age adjustment rules
            var ageRuleKey = GetAgeGroup(input.Age);
            Console.WriteLine($"Looking up age adjustment rules for key: {ageRuleKey}");
 
            if (allocationMatrix.Age_Adjustment_Rules.TryGetValue(ageRuleKey, out var ageAdjustments))
            {
                Console.WriteLine($"Age adjustments found: {JsonSerializer.Serialize(ageAdjustments)}");
                foreach (var adjustment in ageAdjustments)
                {
                    if (finalAllocation.ContainsKey(adjustment.Key))
                    {
                        finalAllocation[adjustment.Key] += adjustment.Value;
                    }
                }
            }
            else
            {
                Console.WriteLine($"No age adjustments found for key: {ageRuleKey}");
            }
 
            // Step 4: Apply goal tuning
            Console.WriteLine($"Looking up goal tuning for goal: {input.Goal}");
            if (string.IsNullOrEmpty(input.Goal))
            {
                throw new ArgumentException("Goal is required.");
            }
            if (allocationMatrix.Goal_Tuning.TryGetValue(input.Goal, out var goalTuning))
            {
                Console.WriteLine($"Goal tuning found: {JsonSerializer.Serialize(goalTuning)}");
 
                switch (input.Goal)
                {
                    case "Emergency Fund":
                        if (finalAllocation.ContainsKey(CashKey) && finalAllocation[CashKey] < 40)
                        {
                            var cashDeficit = 40 - finalAllocation[CashKey];
                            finalAllocation[CashKey] += cashDeficit;
 
                            var categoriesToReduce = new[] { EquityKey, FixedIncomeKey, CommoditiesKey, RealEstateKey };
                            var reductionPerCategory = cashDeficit / categoriesToReduce.Length;
                            foreach (var category in categoriesToReduce)
                            {
                                if (finalAllocation.ContainsKey(category))
                                {
                                    finalAllocation[category] -= reductionPerCategory;
                                }
                            }
                        }
                        break;
 
                    case "Retirement":
                        if (goalTuning.TryGetValue("fixedIncome_boost", out var fixedIncomeBoost) && finalAllocation.ContainsKey(FixedIncomeKey))
                        {
                            finalAllocation[FixedIncomeKey] += GetDoubleFromObject(fixedIncomeBoost);
                        }
                        if (goalTuning.TryGetValue("realEstate_boost", out var realEstateBoost) && finalAllocation.ContainsKey(RealEstateKey))
                        {
                            finalAllocation[RealEstateKey] += GetDoubleFromObject(realEstateBoost);
                        }
                        break;
 
                    case "Wealth Accumulation":
                        if (finalAllocation.ContainsKey(EquityKey) && finalAllocation.Values.Any() && finalAllocation[EquityKey] < finalAllocation.Values.Max())
                        {
                            finalAllocation[EquityKey] += 10;
                            var sumAfterEquityBoost = finalAllocation.Values.Sum();
                            var remainingAdjustment = 100 - sumAfterEquityBoost;
                            var otherKeys = finalAllocation.Keys.Where(k => k != EquityKey).ToList();
                            if (otherKeys.Any())
                            {
                                foreach (var key in otherKeys)
                                {
                                    finalAllocation[key] += remainingAdjustment / otherKeys.Count();
                                }
                            }
                        }
                        break;
 
                    case "Child Education":
                        if (goalTuning.TryGetValue("fixedIncome_boost", out var fixedIncomeBoostChild) && finalAllocation.ContainsKey(FixedIncomeKey))
                        {
                            finalAllocation[FixedIncomeKey] += GetDoubleFromObject(fixedIncomeBoostChild);
                        }
                        if (goalTuning.TryGetValue("equityReduction_moderate", out var equityReduction) && finalAllocation.ContainsKey(EquityKey))
                        {
                            finalAllocation[EquityKey] -= GetDoubleFromObject(equityReduction);
                        }
                        break;
 
                    case "Big Purchase":
                        if (goalTuning.TryGetValue("balanced", out var balancedObj) &&
                            bool.TryParse(balancedObj.ToString(), out var balanced) && balanced)
                        {
                            Console.WriteLine("Big Purchase goal tuning: balancing enabled.");
 
                            double threshold = 30.0;
                            var keys = finalAllocation.Keys.ToList();
                            double totalExcess = 0.0;
 
                            foreach (var assetKey in keys)
                            {
                                if (finalAllocation[assetKey] > threshold)
                                {
                                    double excess = finalAllocation[assetKey] - threshold;
                                    totalExcess += excess;
                                    finalAllocation[assetKey] = threshold;
                                    Console.WriteLine($"{assetKey} capped at {threshold}, excess {excess}% collected.");
                                }
                            }
 
                            var underThresholdKeys = keys.Where(k => finalAllocation[k] < threshold).ToList();
                            int count = underThresholdKeys.Count();
 
                            if (count > 0 && totalExcess > 0)
                            {
                                double share = totalExcess / count;
                                foreach (var key in underThresholdKeys)
                                {
                                    finalAllocation[key] += share;
                                    Console.WriteLine($"{share}% added to {key}. New value: {finalAllocation[key]}%");
                                }
                            }
 
                            // Normalize after potential balancing
                            var totalAfterBigPurchase = finalAllocation.Values.Sum();
                            if (Math.Abs(totalAfterBigPurchase - 100) > 0.01)
                            {
                                var keyToAdjust = finalAllocation.OrderByDescending(kv => kv.Value).First().Key;
                                finalAllocation[keyToAdjust] += 100 - totalAfterBigPurchase;
                            }
                        }
                        break;
                }
 
                // Normalize after goal tuning adjustments
                var totalAfterGoalTuning = finalAllocation.Values.Sum();
                if (Math.Abs(totalAfterGoalTuning - 100) > 0.01)
                {
                    var keyToAdjust = finalAllocation.OrderByDescending(kv => kv.Value).First().Key;
                    finalAllocation[keyToAdjust] += 100 - totalAfterGoalTuning;
                }
            }
            else
            {
                Console.WriteLine($"No goal tuning found for goal: {input.Goal}");
            }
 
            // Step 5: Normalize allocation to ensure it adds up to 100%
            var total = finalAllocation.Values.Sum();
            if (Math.Abs(total - 100) > 0.01)
            {
                var adjustmentFactor = 100 / total;
                foreach (var key in finalAllocation.Keys.ToList())
                {
                    finalAllocation[key] *= adjustmentFactor;
                }
            }
 
            // Step 6: Compute and add sub-allocations
            var subMatrix = await LoadSubAllocationMatrixAsync();
            var subAllocations = ComputeSubAllocations(finalAllocation, input.RiskTolerance!, subMatrix);

            // Step 7: Compute product splits for each sub-allocation
            var productSplits = await ComputeProductSplits(subAllocations);
 
            // Step 8: Format the final result according to the expected structure
            var finalFormattedResult = new Dictionary<string, object>();
 
//             
            foreach (var subAllocation in subAllocations)
            {
                finalFormattedResult[subAllocation.Key] = new
                {
                    Percentage = subAllocation.Value.Values.Sum(),
                    SubAssets = subAllocation.Value,
                    // Products = productSplits.ContainsKey(subAllocation.Key) ? productSplits[subAllocation.Key] : new Dictionary<string, double>()
                                        Products = productSplits.ContainsKey(subAllocation.Key)
                        ? productSplits[subAllocation.Key].SelectMany(kv => kv.Value)
                            .GroupBy(kv => kv.Key)
                            .ToDictionary(g => g.Key, g => g.Sum(kv => kv.Value))
                        : new Dictionary<string, double>();
                };
            }

            return new Dictionary<string, object> { ["assets"] = finalFormattedResult };
        }

        private async Task<Dictionary<string, Dictionary<string, Dictionary<string, double>>>> ComputeProductSplits(Dictionary<string, Dictionary<string, double>> subAllocations)
        {
            // Load product data
            var productFilePath = Path.Combine("Repositories", "Matrix", "product_json.json");
            if (!File.Exists(productFilePath))
                throw new FileNotFoundException("Product data file not found.");

            var productJson = await File.ReadAllTextAsync(productFilePath);
            var productData = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, List<Product>>>>(productJson);

            var productSplits = new Dictionary<string, Dictionary<string, Dictionary<string, double>>>();

            foreach (var subAllocation in subAllocations)
            {
                var assetClass = subAllocation.Key;
                var subAssets = subAllocation.Value;

                foreach (var subAsset in subAssets)
                {
                    var subAssetName = subAsset.Key;
                    var subAssetPercentage = subAsset.Value;

                    if (!productData.ContainsKey(assetClass) || !productData[assetClass].ContainsKey(subAssetName))
                        continue;

                    var products = productData[assetClass][subAssetName];
                    // var totalReturn = products.Sum(p => p.AnnualReturn);
                    var totalReturn = products.Sum(p => double.Parse(p.Return.TrimEnd('%')) / 100.0);

                    var productWeights = products.ToDictionary(
                        p => p.Name,
                        p => (double.Parse(p.Return.TrimEnd('%')) / 100.0 / totalReturn) * subAssetPercentage
                    );

                    if (!productSplits.ContainsKey(assetClass))
                        productSplits[assetClass] = new Dictionary<string, Dictionary<string, double>>();

                    productSplits[assetClass][subAssetName] = productWeights;
                }
            }

            return productSplits;
        }

        private async Task<SubAllocationMatrix> LoadSubAllocationMatrixAsync()
        {
            var filePath = Path.Combine("Repositories", "Matrix", "SubAllocationMatrix.json");
            if (!File.Exists(filePath))
                throw new FileNotFoundException("SubAllocationMatrix.json not found.");

            var json = await File.ReadAllTextAsync(filePath);
            var matrix = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, Dictionary<string, int>>>>(json);

            return new SubAllocationMatrix { Matrix = matrix! };
        }

        private string GetAgeGroup(int age)
        {
            if (age < 30) return "<30";
            if (age <= 45) return "30-45";
            if (age <= 60) return "45-60";
            return "60+";
        }

        private double GetDoubleFromObject(object obj)
        {
            if (obj is JsonElement jsonElement)
            {
                if (jsonElement.ValueKind == JsonValueKind.Number)
                {
                    return jsonElement.GetDouble();
                }
                throw new InvalidCastException($"JsonElement is not a number: {jsonElement}");
            }
 
            if (obj is IConvertible convertible)
            {
                return convertible.ToDouble(null);
            }
 
            throw new InvalidCastException($"Unable to convert object of type {obj.GetType()} to double.");
        }

        private Dictionary<string, Dictionary<string, double>> ComputeSubAllocations(
    Dictionary<string, double> finalAllocations,
    string riskLevel,
    SubAllocationMatrix subMatrix)
{
    var subAllocationsResult = new Dictionary<string, Dictionary<string, double>>();
    var assetClassMapping = new Dictionary<string, string>
    {
        { "equity", "Equity" },
        { "fixedIncome", "Fixed Income" },
        { "commodities", "Commodities" },
        { "cash", "Cash Equivalence" },
        { "realEstate", "Real Estate" }
    };

    foreach (var assetClass in finalAllocations)
    {
        var className = assetClass.Key;
        var totalPercentage = assetClass.Value;

        if (!assetClassMapping.TryGetValue(className, out var mappedClassName))
        {
            Console.WriteLine($"No mapping found for asset class: {className}");
            continue;
        }

        if (!subMatrix.Matrix.ContainsKey(mappedClassName))
        {
            Console.WriteLine($"No sub-allocation rules found for asset class: {mappedClassName}");
            continue; // Skip if no suballocation rules for this asset class
        }

        var subcategories = subMatrix.Matrix[mappedClassName];
        var weights = new Dictionary<string, int>();

        // Collect weights for this risk level
        foreach (var sub in subcategories)
        {
            if (sub.Value.ContainsKey(riskLevel))
            {
                weights[sub.Key] = sub.Value[riskLevel];
            }
        }

        var totalWeight = weights.Values.Sum();
        if (totalWeight == 0)
        {
            Console.WriteLine($"No weights found for risk level '{riskLevel}' in asset class '{className}'");
            continue;
        }

        // Calculate suballocation % based on weight
        var calculatedSubs = weights.ToDictionary(
            kv => kv.Key,
            kv => Math.Round((kv.Value / (double)totalWeight) * totalPercentage, 2)
        );

        Console.WriteLine($"Sub-allocations for {className}: {JsonSerializer.Serialize(calculatedSubs)}");
        subAllocationsResult[className] = calculatedSubs;
    }

    return subAllocationsResult;
}
}
    }


// foreach (var mainAllocationPair in finalAllocation)
//             {
//                 var assetClassName = mainAllocationPair.Key;
//                 var assetPercentage = mainAllocationPair.Value;
 
//                 if (subAllocations.ContainsKey(assetClassName))
//                 {
//                     finalFormattedResult[assetClassName] = new Dictionary<string, object>
//                     {
//                         ["percentage"] = Math.Round(assetPercentage, 2),
//                         ["subAssets"] = subAllocations[assetClassName]
//                     };
//                     Console.WriteLine($"Added sub-assets for {assetClassName}: {JsonSerializer.Serialize(subAllocations[assetClassName])}");
//                 }
//                 else
//                 {
//                     finalFormattedResult[assetClassName] = new Dictionary<string, object>
//                     {
//                         ["percentage"] = Math.Round(assetPercentage, 2),
//                         ["subAssets"] = new Dictionary<string, double>() // Empty if no sub-allocations
//                     };

//                     Console.WriteLine($"No sub-assets for {assetClassName}. Added empty sub-assets.");
//                 }
//             }
 
//             Console.WriteLine($"Final formatted allocation: {JsonSerializer.Serialize(finalFormattedResult)}");
//             return new Dictionary<string, object> { ["assets"] = finalFormattedResult }; // Wrap in an "assets" key
//         }
 
//         private string GetAgeGroup(int age)
//         {
//             if (age < 30) return "<30";
//             if (age <= 45) return "30-45";
//             if (age <= 60) return "45-60";
//             return "60+";
//         }
 
//         private double GetDoubleFromObject(object obj)
//         {
//             if (obj is JsonElement jsonElement)
//             {
//                 if (jsonElement.ValueKind == JsonValueKind.Number)
//                 {
//                     return jsonElement.GetDouble();
//                 }
//                 throw new InvalidCastException($"JsonElement is not a number: {jsonElement}");
//             }
 
//             if (obj is IConvertible convertible)
//             {
//                 return convertible.ToDouble(null);
//             }
 
//             throw new InvalidCastException($"Unable to convert object of type {obj.GetType()} to double.");
//         }
 
//         private async Task<SubAllocationMatrix> LoadSubAllocationMatrixAsync()
//         {
//             var filePath = Path.Combine("Repositories", "Matrix", "SubAllocationMatrix.json");
 
//             if (!File.Exists(filePath))
//                 throw new FileNotFoundException("SubAllocationMatrix.json not found.");
 
//             var json = await File.ReadAllTextAsync(filePath);
//             var matrix = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, Dictionary<string, int>>>>(json);
 
//             return new SubAllocationMatrix { Matrix = matrix! };
//         }
 
//         private Dictionary<string, Dictionary<string, double>> ComputeSubAllocations(
//     Dictionary<string, double> finalAllocations,
//     string riskLevel,
//     SubAllocationMatrix subMatrix)
// {
//     var subAllocationsResult = new Dictionary<string, Dictionary<string, double>>();
//     var assetClassMapping = new Dictionary<string, string>
//     {
//         { "equity", "Equity" },
//         { "fixedIncome", "Fixed Income" },
//         { "commodities", "Commodities" },
//         { "cash", "Cash Equivalence" },
//         { "realEstate", "Real Estate" }
//     };

//     foreach (var assetClass in finalAllocations)
//     {
//         var className = assetClass.Key;
//         var totalPercentage = assetClass.Value;

//         if (!assetClassMapping.TryGetValue(className, out var mappedClassName))
//         {
//             Console.WriteLine($"No mapping found for asset class: {className}");
//             continue;
//         }

//         if (!subMatrix.Matrix.ContainsKey(mappedClassName))
//         {
//             Console.WriteLine($"No sub-allocation rules found for asset class: {mappedClassName}");
//             continue; // Skip if no suballocation rules for this asset class
//         }

//         var subcategories = subMatrix.Matrix[mappedClassName];
//         var weights = new Dictionary<string, int>();

//         // Collect weights for this risk level
//         foreach (var sub in subcategories)
//         {
//             if (sub.Value.ContainsKey(riskLevel))
//             {
//                 weights[sub.Key] = sub.Value[riskLevel];
//             }
//         }

//         var totalWeight = weights.Values.Sum();
//         if (totalWeight == 0)
//         {
//             Console.WriteLine($"No weights found for risk level '{riskLevel}' in asset class '{className}'");
//             continue;
//         }

//         // Calculate suballocation % based on weight
//         var calculatedSubs = weights.ToDictionary(
//             kv => kv.Key,
//             kv => Math.Round((kv.Value / (double)totalWeight) * totalPercentage, 2)
//         );

//         Console.WriteLine($"Sub-allocations for {className}: {JsonSerializer.Serialize(calculatedSubs)}");
//         subAllocationsResult[className] = calculatedSubs;
//     }

//     return subAllocationsResult;
// }
// Step 8: Format the final result
            // var finalFormattedResult = new Dictionary<string, object>();