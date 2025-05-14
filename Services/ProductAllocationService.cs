using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using WisVestAPI.Models.DTOs;
using WisVestAPI.Models.Matrix;
using System.Text.Json.Serialization;

namespace WisVestAPI.Services
{
    public class ProductAllocationService
    {
        private readonly string _productJsonFilePath = "Repositories/Matrix/product_json.json";
        private readonly string _outputJsonFilePath = "ProductAllocations.json"; // File to save the result
        private readonly InvestmentAmountService _investmentAmountService;
        private readonly ILogger<ProductAllocationService> _logger;

        public ProductAllocationService(ILogger<ProductAllocationService> logger, InvestmentAmountService investmentAmountService)
        {
            _logger = logger;
            _investmentAmountService = investmentAmountService;
        }

        public async Task<Dictionary<string, Dictionary<string, Dictionary<string, Product>>>> CalculateProductAllocations(
            Dictionary<string, Dictionary<string, double>> subAllocationResult,
            double targetAmount,
        string investmentHorizon)
        {
            try{
            _logger.LogInformation("Starting product allocation calculation.");
            var productData = await LoadProductDataAsync();
            var productAllocations = new Dictionary<string, Dictionary<string, Dictionary<string, Product>>>();

            foreach (var assetClass in subAllocationResult.Keys)
            {
                var subAssetClasses = subAllocationResult[assetClass];

                foreach (var subAssetClass in subAssetClasses.Keys)
                {
                    var percentageSplit = subAssetClasses[subAssetClass];
                    var products = GetProductsForAssetClass(productData, assetClass, subAssetClass);

                    if (products == null || products.Count == 0)
                    {
                        _logger.LogWarning("No products found for sub-asset class: {SubAssetClass} in asset class: {AssetClass}.", subAssetClass, assetClass);
                        //Console.WriteLine($"No products found for sub-asset class: {subAssetClass} in asset class: {assetClass}");
                        continue;
                    }

                    double totalReturns = products.Sum(p => p.AnnualReturn);
                    if (totalReturns <= 0)
                    {
                        _logger.LogWarning("Total return is zero or negative for sub-asset {SubAssetClass}. Skipping allocation.", subAssetClass);
                        Console.WriteLine($"Total return is zero or negative for sub-asset {subAssetClass}, skipping allocation.");
                        continue;
                    }

                    var productSplit = new Dictionary<string, Product>();
                    foreach (var product in products)
                    {
                        var splitRatio = product.AnnualReturn / totalReturns;
                        var allocation = Math.Round(splitRatio * percentageSplit, 2);
                        product.PercentageSplit = allocation;

                        product.InvestmentAmount = _investmentAmountService.CalculateInvestmentAmount(
                    allocation,
                    targetAmount,
                    product.AnnualReturn,
                    investmentHorizon
                        );

                        // You could optionally store this investment amount in the product object if needed
                        productSplit[product.ProductName] = product;
                    }

                    if (!productAllocations.ContainsKey(assetClass))
                        productAllocations[assetClass] = new Dictionary<string, Dictionary<string, Product>>();

                    productAllocations[assetClass][subAssetClass] = productSplit;
                }
            }

            await SaveProductAllocationsToFileAsync(productAllocations);

                _logger.LogInformation("Final Product Allocations: {ProductAllocations}", JsonSerializer.Serialize(productAllocations));
                return productAllocations;
            }catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating product allocations.");
                //Console.WriteLine($"Error calculating product allocations: {ex.Message}");
                throw; // Re-throw the exception to propagate it to the caller
            }
        }

        private async Task<Dictionary<string, Dictionary<string, List<Product>>>> LoadProductDataAsync()
        {
            try{
            _logger.LogInformation("Loading product data from JSON file: {FilePath}", _productJsonFilePath);
            if (!File.Exists(_productJsonFilePath))
                throw new FileNotFoundException($"Product JSON file not found at {_productJsonFilePath}");

            var json = await File.ReadAllTextAsync(_productJsonFilePath);
            //Console.WriteLine($"Raw JSON Content: {json}");
             _logger.LogInformation("Raw JSON Content: {JsonContent}", json);
            var productData = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, List<Product>>>>(json);
            if (productData == null)
                throw new InvalidOperationException("Failed to deserialize product data. Ensure the JSON structure is correct.");

            _logger.LogInformation("Product data loaded successfully.");
            return productData.ToDictionary(
        assetClass => NormalizeKey(assetClass.Key),  // Normalize asset class keys
        assetClass => assetClass.Value.ToDictionary(
            subAssetClass => subAssetClass.Key,  // No normalization for sub-assets
            subAssetClass => subAssetClass.Value
        )
    );
    } catch (Exception ex)
    {
         _logger.LogError(ex, "Error loading product data.");
        //Console.WriteLine($"Error loading product data: {ex.Message}");
        throw; // Re-throw the exception to propagate it to the caller
    }

}
        private List<Product> GetProductsForAssetClass(
            Dictionary<string, Dictionary<string, List<Product>>> productData,
            string assetClass,
            string subAssetClass)
        {
            try{
            assetClass = NormalizeKey(assetClass);
            subAssetClass = subAssetClass.Trim();
            _logger.LogInformation("Fetching products for Asset Class: {AssetClass}, Sub-Asset Class: {SubAssetClass}.", assetClass, subAssetClass);
            if (!productData.ContainsKey(assetClass) || !productData[assetClass].ContainsKey(subAssetClass))
            {
                 _logger.LogWarning("No products found for asset class '{AssetClass}' and sub-asset class '{SubAssetClass}'.", assetClass, subAssetClass);
                    return new List<Product>();
            }
                

            return productData[assetClass][subAssetClass];
            } catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting products for asset class.");
                //Console.WriteLine($"Error getting products for asset class: {ex.Message}");
                return new List<Product>(); // Return an empty list in case of error
            }
        }

        public async Task<Dictionary<string, Dictionary<string, Dictionary<string, Product>>>> GetProductAllocationsAsync()
{
    try
    {
        _logger.LogInformation("Attempting to read product allocations from JSON file: {FilePath}", _outputJsonFilePath);

        if (!File.Exists(_outputJsonFilePath))
        {
            _logger.LogWarning("Product allocations file not found at {FilePath}. Returning an empty result.", _outputJsonFilePath);
            return new Dictionary<string, Dictionary<string, Dictionary<string, Product>>>();
        }

        var json = await File.ReadAllTextAsync(_outputJsonFilePath);
        _logger.LogInformation("Product allocations JSON read successfully.");

        var productAllocations = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, Dictionary<string, Product>>>>(json);

        if (productAllocations == null)
        {
            _logger.LogWarning("Deserialized product allocations are null. Returning an empty result.");
            return new Dictionary<string, Dictionary<string, Dictionary<string, Product>>>();
        }

        return productAllocations;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error occurred while reading product allocations from file.");
        throw; // Re-throw the exception to propagate it to the caller
    }
}

        private async Task SaveProductAllocationsToFileAsync(
            Dictionary<string, Dictionary<string, Dictionary<string, Product>>> productAllocations)
        {
            try
            {
                // Serialize the product allocations to a JSON string
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true // Makes the JSON output more readable
                };
                string jsonString = JsonSerializer.Serialize(productAllocations, options);

                // Write the JSON string to the specified file (overwrites the file if it exists)
                await File.WriteAllTextAsync(_outputJsonFilePath, jsonString);

                _logger.LogInformation("Product allocations saved successfully to {FilePath}", _outputJsonFilePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while saving product allocations to file.");
                throw;
            }
        }

        private string NormalizeKey(string input)
        {
            return input.Trim().ToLower().Replace(" ", "");
        }
    }

    public class Product
    {
        [JsonPropertyName("product_name")]
        public string ProductName { get; set; }

        [JsonPropertyName("annual_return")]
        public double AnnualReturn { get; set; }

        [JsonPropertyName("asset_class")]
        public string AssetClass { get; set; }

        [JsonPropertyName("sub_asset_class")]
        public string SubAssetClass { get; set; }

        [JsonPropertyName("liquidity")]
        public string Liquidity { get; set; }

        [JsonPropertyName("pros")]
        public List<string> Pros { get; set; }

        [JsonPropertyName("cons")]
        public List<string> Cons { get; set; }

        [JsonPropertyName("risk_level")]
        public string RiskLevel { get; set; }

        [JsonPropertyName("percentage_split")]
        public double PercentageSplit { get; set; }
        public double InvestmentAmount { get; set; }
    }


    public class AssetDTO
    {
        public double Percentage { get; set; }
        public Dictionary<string, double> SubAssets { get; set; }
    }
}


