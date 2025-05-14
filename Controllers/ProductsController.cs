using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using WisVestAPI.Models;
using WisVestAPI.Models.DTOs;
using WisVestAPI.Models.Matrix;
using Microsoft.AspNetCore.Authorization;
namespace WisVestAPI.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class ProductsController : ControllerBase
    {
        private readonly string _jsonFilePath = Path.Combine(Directory.GetCurrentDirectory(), "Repositories","Matrix","product_json.json");


[HttpGet("products")]
public async Task<IActionResult> LoadProducts()
{
    try
    {
        var productJsonFilePath = "Repositories/Matrix/product_json.json";

        if (!System.IO.File.Exists(productJsonFilePath))
            return NotFound($"Product JSON file not found at {productJsonFilePath}");

        var json = await System.IO.File.ReadAllTextAsync(productJsonFilePath);

        // Deserialize into a nested dictionary structure
        var productData = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, List<Product>>>>(json);

        if (productData == null)
            return BadRequest("Failed to deserialize product data. Ensure the JSON structure is correct.");

        // Flatten the nested structure into a single list of products
        var products = new List<Product>();
        foreach (var assetClass in productData.Values)
        {
            foreach (var subAssetClass in assetClass.Values)
            {
                products.AddRange(subAssetClass);
            }
        }

        var productDTOs = products.Select(p => new ProductDTO
        {
            ProductName = p.ProductName,
            AnnualReturn = p.AnnualReturn,
            AssetClass = p.AssetClass,
            SubAssetClass = p.SubAssetClass,
            Liquidity = p.Liquidity,
            Pros = p.Pros,
            Cons = p.Cons,
            RiskLevel = p.RiskLevel,
            description = p.description
        }).ToList();

        return Ok(productDTOs);
    }
    catch (JsonException ex)
    {
        Console.WriteLine($"Error reading JSON file: {ex.Message}");
        return StatusCode(500, $"Error reading JSON file: {ex.Message}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"An unexpected error occurred: {ex.Message}");
        return StatusCode(500, $"An unexpected error occurred: {ex.Message}");
    }
}
    }
}
