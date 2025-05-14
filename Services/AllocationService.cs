using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
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
        private readonly ILogger<AllocationService> _logger;

        public AllocationService(MatrixRepository matrixRepository, ILogger<AllocationService> logger)
        {
            _matrixRepository = matrixRepository;
            _logger = logger;
        }
 
        private const string CashKey = "cash";
        private const string EquityKey = "equity";
        private const string FixedIncomeKey = "fixedIncome";
        private const string CommoditiesKey = "commodities";
        private const string RealEstateKey = "realEstate";


        private async Task SaveFinalAllocationToFileAsync(Dictionary<string, object> finalAllocation)
        
{
    try
    {
        var filePath = "FinalAllocation.json"; // Specify the file path
        var options = new JsonSerializerOptions
        {
            WriteIndented = true // Makes the JSON output more readable
        };
        string jsonString = JsonSerializer.Serialize(finalAllocation, options);

        // Write the JSON string to the specified file (overwrites the file if it exists)
        await File.WriteAllTextAsync(filePath, jsonString);

        _logger.LogInformation("Final allocation saved successfully to {FilePath}", filePath);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error occurred while saving final allocation to file.");
        throw;
    }
}
        public async Task<Dictionary<string, object>> CalculateFinalAllocation(UserInputDTO input)
        {  
             try{
                //Console.WriteLine("Starting allocation calculation...");
                _logger.LogInformation("Starting allocation calculation...");
                // Load the allocation matrix
                var allocationMatrix = await _matrixRepository.LoadMatrixDataAsync();
                if (allocationMatrix == null)
                {
                    //Console.WriteLine("Error: Allocation matrix is null.");
                    _logger.LogError("Allocation matrix is null.");
                    throw new InvalidOperationException("Allocation matrix data is null.");
                }
                //Console.WriteLine("Allocation matrix loaded successfully.");
                _logger.LogInformation("Allocation matrix loaded successfully.");
    
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
    
                //Console.WriteLine($"Looking up base allocation for key: {riskHorizonKey}");
                _logger.LogInformation($"Looking up base allocation for key: {riskHorizonKey}");
    
                // Step 2: Determine base allocation
                if (!allocationMatrix.Risk_Horizon_Allocation.TryGetValue(riskHorizonKey, out var baseAllocation))
                {
                    //Console.WriteLine($"Error: No base allocation found for key: {riskHorizonKey}");
                    _logger.LogError("No base allocation found for key: {RiskHorizonKey}", riskHorizonKey);
                    throw new ArgumentException($"Invalid combination of RiskTolerance and InvestmentHorizon: {riskHorizonKey}");
                }
                //Console.WriteLine($"Base allocation found: {JsonSerializer.Serialize(baseAllocation)}");
                _logger.LogInformation($"Base allocation found: {JsonSerializer.Serialize(baseAllocation)}");
    
                // Clone the base allocation to avoid modifying the original matrix
                var finalAllocation = new Dictionary<string, double>(baseAllocation);
                try{
                // Step 3: Apply age adjustment rules
                    var ageRuleKey = GetAgeGroup(input.Age);
                    //Console.WriteLine($"Looking up age adjustment rules for key: {ageRuleKey}");
                    _logger.LogInformation("Looking up age adjustment rules for key: {AgeRuleKey}", ageRuleKey);

        
                    if (allocationMatrix.Age_Adjustment_Rules.TryGetValue(ageRuleKey, out var ageAdjustments))
                    {
                        _logger.LogInformation("Age adjustments found: {AgeAdjustments}", JsonSerializer.Serialize(ageAdjustments));
                        //Console.WriteLine($"Age adjustments found: {JsonSerializer.Serialize(ageAdjustments)}");
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
                         _logger.LogWarning("No age adjustments found for key: {AgeRuleKey}", ageRuleKey);
                        //Console.WriteLine($"No age adjustments found for key: {ageRuleKey}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error applying age adjustments: {Message}", ex.Message);
                    //Console.WriteLine($"Error applying age adjustments: {ex.Message}");
                    throw; // Re-throw the exception to propagate it to the caller
                }
    
                // Step 4: Apply goal tuning
                try{
                    _logger.LogInformation("Looking up goal tuning for goal: {Goal}", input.Goal);
                    //Console.WriteLine($"Looking up goal tuning for goal: {input.Goal}");
                    if (string.IsNullOrEmpty(input.Goal))
                    {
                        throw new ArgumentException("Goal is required.");
                    }
                    if (allocationMatrix.Goal_Tuning.TryGetValue(input.Goal, out var goalTuning))
                    {
                         _logger.LogInformation("Goal tuning found: {GoalTuning}", JsonSerializer.Serialize(goalTuning));
                        //Console.WriteLine($"Goal tuning found: {JsonSerializer.Serialize(goalTuning)}");
        
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
                        _logger.LogWarning("No goal tuning found for goal: {Goal}", input.Goal);
                       // Console.WriteLine($"No goal tuning found for goal: {input.Goal}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error applying goal tuning.");
                    //Console.WriteLine($"Error applying goal tuning: {ex.Message}");
                    throw; // Re-throw the exception to propagate it to the caller
                }
    
                // Step 5: Normalize allocation to ensure it adds up to 100%
                try{
                    var total = finalAllocation.Values.Sum();
                    if (Math.Abs(total - 100) > 0.01)
                    {
                        var adjustmentFactor = 100 / total;
                        foreach (var key in finalAllocation.Keys.ToList())
                        {
                            finalAllocation[key] *= adjustmentFactor;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error normalizing allocation.");
                    //Console.WriteLine($"Error normalizing allocation: {ex.Message}");
                    throw; // Re-throw the exception to propagate it to the caller
                }
    
                // Step 6: Compute and add sub-allocations
                try{
                    var subMatrix = await LoadSubAllocationMatrixAsync();
                    var subAllocations = ComputeSubAllocations(finalAllocation, input.RiskTolerance!, subMatrix);
        
                    // Step 7: Format the final result according to the expected structure
                    var finalFormattedResult = new Dictionary<string, object>();
        
                    foreach (var mainAllocationPair in finalAllocation)
                    {
                        var assetClassName = mainAllocationPair.Key;
                        var assetPercentage = mainAllocationPair.Value;
        
                        if (subAllocations.TryGetValue(assetClassName, out var subAssetAllocations))
                        {
                            finalFormattedResult[assetClassName] = new Dictionary<string, object>
                            {
                                ["percentage"] = Math.Round(assetPercentage, 2),
                                ["subAssets"] = subAssetAllocations
                            };
                           // Console.WriteLine($"Added sub-assets for {assetClassName}: {JsonSerializer.Serialize(subAllocations[assetClassName])}");
                           _logger.LogInformation("Added sub-assets for {AssetClassName}: {SubAssets}", assetClassName, JsonSerializer.Serialize(subAllocations[assetClassName]));
                        }
                        else
                        {
                            finalFormattedResult[assetClassName] = new Dictionary<string, object>
                            {
                                ["percentage"] = Math.Round(assetPercentage, 2),
                                ["subAssets"] = new Dictionary<string, double>() // Empty if no sub-allocations
                            };

                            //Console.WriteLine($"No sub-assets for {assetClassName}. Added empty sub-assets.");
                            _logger.LogWarning("No sub-assets for {AssetClassName}. Added empty sub-assets.", assetClassName);
                        }
                    }
                    _logger.LogInformation("Final formatted allocation: {FinalFormattedResult}", JsonSerializer.Serialize(finalFormattedResult));
                    //Console.WriteLine($"Final formatted allocation: {JsonSerializer.Serialize(finalFormattedResult)}");
                    var result = new Dictionary<string, object> { ["assets"] = finalFormattedResult };

        // Save the result to a JSON file
                     await SaveFinalAllocationToFileAsync(result);

                     _logger.LogInformation("Final formatted allocation: {FinalFormattedResult}", JsonSerializer.Serialize(finalFormattedResult));
                     return result;
                }
                catch (Exception ex)
                {
                     _logger.LogError(ex, "Error computing sub-allocations.");
                    //Console.WriteLine($"Error computing sub-allocations: {ex.Message}");
                    throw; // Re-throw the exception to propagate it to the caller
                }
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Error during allocation calculation.");
                //Console.WriteLine($"Error during allocation calculation: {ex.Message}");
                throw; // Re-throw the exception to propagate it to the caller
            }
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

                        // filepath: c:\Users\divya.t1\Desktop\7-5-25-WisVest-Backend-main\Services\AllocationService.cs
                public async Task<Dictionary<string, object>> GetFinalAllocationFromFileAsync()
                {
                    try
                    {
                        var filePath = "FinalAllocation.json"; // Path to the saved JSON file
                
                        _logger.LogInformation("Attempting to read final allocation from JSON file: {FilePath}", filePath);
                
                        if (!File.Exists(filePath))
                        {
                            _logger.LogWarning("Final allocation file not found at {FilePath}. Returning null.", filePath);
                            return null;
                        }
                
                        var json = await File.ReadAllTextAsync(filePath);
                        _logger.LogInformation("Final allocation JSON read successfully.");
                
                        var finalAllocation = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                
                        if (finalAllocation == null)
                        {
                            _logger.LogWarning("Deserialized final allocation is null. Returning null.");
                            return null;
                        }
                
                        return finalAllocation;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error occurred while reading final allocation from file.");
                        throw;
                    }
                }
 
        private async Task<SubAllocationMatrix> LoadSubAllocationMatrixAsync()
        {
            try{
            var filePath = Path.Combine("Repositories", "Matrix", "SubAllocationMatrix.json");

            if (!File.Exists(filePath))
                throw new FileNotFoundException("SubAllocationMatrix.json not found.");

            var json = await File.ReadAllTextAsync(filePath);
            var intMatrix = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, Dictionary<string, int>>>>(json);

            var doubleMatrix = intMatrix.ToDictionary(
                outer => outer.Key,
                outer => outer.Value.ToDictionary(
                    middle => middle.Key,
                    middle => middle.Value.ToDictionary(
                        inner => inner.Key,
                        inner => (double)inner.Value
                    )
                )
            );

            return new SubAllocationMatrix { Matrix = doubleMatrix };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading sub-allocation matrix.");
                //Console.WriteLine($"Error loading sub-allocation matrix: {ex.Message}");
                throw; // Re-throw the exception to propagate it to the caller
            }
        }

        
        private Dictionary<string, Dictionary<string, double>> ComputeSubAllocations(
    Dictionary<string, double> finalAllocations,
    string riskLevel,
    SubAllocationMatrix subMatrix)
    {
        try{
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
                _logger.LogWarning("No mapping found for asset class: {ClassName}", className);
                //Console.WriteLine($"No mapping found for asset class: {className}");
                continue;
            }

            if (!subMatrix.Matrix.ContainsKey(mappedClassName))
            {
                _logger.LogWarning("No sub-allocation rules found for asset class: {MappedClassName}", mappedClassName);
                //Console.WriteLine($"No sub-allocation rules found for asset class: {mappedClassName}");
                continue; // Skip if no suballocation rules for this asset class
            }

            var subcategories = subMatrix.Matrix[mappedClassName];
            var weights = new Dictionary<string, int>();

            // Collect weights for this risk level
            foreach (var sub in subcategories)
            {
                if (sub.Value.ContainsKey(riskLevel))
                {
                    weights[sub.Key] = (int)sub.Value[riskLevel];
                }
            }

            var totalWeight = weights.Values.Sum();
            if (totalWeight == 0)
            {
                _logger.LogWarning("No weights found for risk level '{RiskLevel}' in asset class '{ClassName}'", riskLevel, className);
                //Console.WriteLine($"No weights found for risk level '{riskLevel}' in asset class '{className}'");
                continue;
            }

            // Calculate suballocation % based on weight
            var calculatedSubs = weights.ToDictionary(
                kv => kv.Key,
                kv => Math.Round((kv.Value / (double)totalWeight) * totalPercentage, 2)
            );
            _logger.LogInformation("Sub-allocations for {ClassName}: {CalculatedSubs}", className, JsonSerializer.Serialize(calculatedSubs));
            //Console.WriteLine($"Sub-allocations for {className}: {JsonSerializer.Serialize(calculatedSubs)}");
            subAllocationsResult[className] = calculatedSubs;
        }

        return subAllocationsResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error computing sub-allocations.");
            //Console.WriteLine($"Error computing sub-allocations: {ex.Message}");
            throw; // Re-throw the exception to propagate it to the caller
        }
}

public Dictionary<string, Dictionary<string, double>> TransformAssetsToSubAllocationResult(dynamic assets)
{
    var subAllocationResult = new Dictionary<string, Dictionary<string, double>>();

    foreach (var assetClass in assets)
    {
        var assetClassName = assetClass.Name; // e.g., "equity", "fixedIncome"
        var subAssets = assetClass.Value.subAssets;

        var subAssetAllocations = new Dictionary<string, double>();
        foreach (var subAsset in subAssets)
        {
            var subAssetName = subAsset.Name; // e.g., "Large Cap", "Gov Bonds"
            var allocation = (double)subAsset.Value; // Allocation percentage
            subAssetAllocations[subAssetName] = allocation;
        }

        subAllocationResult[assetClassName] = subAssetAllocations;
    }

    return subAllocationResult;
}
    }
}
