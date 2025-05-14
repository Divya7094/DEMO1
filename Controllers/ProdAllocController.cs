// using Microsoft.AspNetCore.Mvc;
// using WisVestAPI.Services;
// using System.Text.Json;

// [ApiController]
// [Route("api/[controller]")]
// public class AllocationController : ControllerBase
// {
//     private readonly ProductAllocationService _productAllocationService;

//     public AllocationController(ProductAllocationService productAllocationService)
//     {
//         _productAllocationService = productAllocationService;
//     }

//     [HttpPost("calculate-product-allocations")]
//     public async Task<IActionResult> CalculateProductAllocations([FromBody] Dictionary<string, Dictionary<string, double>> subAllocationResult)
//     {
//         try
//         {
//             Console.WriteLine($"Sub-allocation Data Received: {JsonSerializer.Serialize(subAllocationResult)}");
//             var result = await _productAllocationService.CalculateProductAllocations(subAllocationResult);
//             return Ok(result);
//         }
//         catch (Exception ex)
//         {
//             return StatusCode(500, $"Internal server error: {ex.Message}");
//         }
//     }
// }

// using Microsoft.AspNetCore.Mvc;
// using WisVestAPI.Services;
// using WisVestAPI.Models.DTOs; // Assuming you create a DTO for sub-allocation
// using Microsoft.Extensions.Logging;
// using System.Text.Json;
// using System.Threading.Tasks;

// [ApiController]
// [Route("api/[controller]")]
// public class AllocationController : ControllerBase
// {
//     private readonly ProductAllocationService _productAllocationService;
//     private readonly ILogger<AllocationController> _logger;

//     public AllocationController(ProductAllocationService productAllocationService, ILogger<AllocationController> logger)
//     {
//         _productAllocationService = productAllocationService;
//         _logger = logger;
//     }

//     [HttpPost("calculate-product-allocations")]
//     public async Task<IActionResult> CalculateProductAllocations([FromBody] SubAllocationResultDTO subAllocationResult)
//     {
//         if (subAllocationResult == null)
//         {
//             return BadRequest("Sub-allocation data cannot be null.");
//         }

//         try
//         {


//             // Log the incoming data in a structured format
//             _logger.LogInformation("Sub-allocation Data Received: {SubAllocationResult}", JsonSerializer.Serialize(subAllocationResult));

//             // Map SubAllocationResultDTO to Dictionary<string, Dictionary<string, double>>
//             var subAllocationDictionary = subAllocationResult.ToDictionary();

//             var result = await _productAllocationService.CalculateProductAllocations(subAllocationDictionary);

//             if (result == null)
//             {
//                 return NotFound("No product allocations found based on the provided sub-allocation data.");
//             }

//             return Ok(result);
//         }
//         catch (Exception ex)
//         {
//             // Log exception details
//             _logger.LogError(ex, "Error occurred while calculating product allocations.");
//             return StatusCode(500, $"Internal server error: {ex.Message}");
//         }
//     }
// }


using Microsoft.AspNetCore.Mvc;
using WisVestAPI.Services;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Threading.Tasks;
using WisVestAPI.Models.DTOs;
using WisVestAPI.Models.Matrix;
using Microsoft.AspNetCore.Authorization;
namespace WisVestAPI.Controllers
{
    
    [ApiController]
    [Route("api/[controller]")]
    public class ProductAllocationController : ControllerBase
    {
        private readonly ProductAllocationService _productAllocationService;
        private readonly ILogger<ProductAllocationController> _logger;

        public ProductAllocationController(ProductAllocationService productAllocationService, ILogger<ProductAllocationController> logger)
        {
            _productAllocationService = productAllocationService;
            _logger = logger;
        }

//         [HttpPost("calculate-product-allocations")]
//         public async Task<IActionResult> CalculateProductAllocations([FromBody] ProductAllocationRequestDTO request)
//         {
//             if (request?.SubAllocationResult == null)
//             {
//                 return BadRequest("Sub-allocation data cannot be null.");
//             }

//             if (request.UserInput == null || string.IsNullOrEmpty(request.UserInput.InvestmentHorizon))
//             {
//                 _logger.LogWarning("Investment Horizon is missing in the user input.");
//                 return BadRequest("Investment Horizon is required.");
//             }

//             try
//             {
//                 _logger.LogInformation("Sub-allocation Data Received: {SubAllocationResult}", JsonSerializer.Serialize(request.SubAllocationResult));

//                 var subAllocationDictionary = request.SubAllocationResult.ToDictionary();

//                 var result = await _productAllocationService.CalculateProductAllocations(
//                     subAllocationDictionary,
//                     request.UserInput
//                 );

//                 if (result == null || result.Count == 0)
//                 {
//                     return NotFound("No product allocations found based on the provided sub-allocation data.");
//                 }

//                 _logger.LogInformation("Calculated Product Allocations: {ProductAllocations}", JsonSerializer.Serialize(result));

//                 return Ok(result);
//             }
//             catch (Exception ex)
//             {
//                 _logger.LogError(ex, "Error occurred while calculating product allocations.");
//                 return StatusCode(500, $"Internal server error: {ex.Message}");
//             }
//         }
//     }
// }
// [HttpPost("calculate-product-allocations")]
// public async Task<IActionResult> CalculateProductAllocations([FromBody] ProductAllocationRequestDTO request)
// {
//     if (request?.SubAllocationResult == null)
//     {
//         return BadRequest("Sub-allocation data cannot be null.");
//     }

//     if (request.UserInput == null || string.IsNullOrEmpty(request.UserInput.InvestmentHorizon))
//     {
//         _logger.LogWarning("Investment Horizon is missing in the user input.");
//         return BadRequest("Investment Horizon is required.");
//     }

//     try
//     {
//         _logger.LogInformation("Sub-allocation Data Received: {SubAllocationResult}", JsonSerializer.Serialize(request.SubAllocationResult));

//         // Transform SubAllocationResult to the required type
//         var subAllocationDictionary = request.SubAllocationResult
//             .ToDictionary(
//                 kvp => kvp.Key,
//                 kvp => kvp.Value.ToDictionary(
//                     product => product.ProductName,
//                     product => product.PercentageSplit
//                 )
//             );

//         // Call the service method
//         var result = await _productAllocationService.CalculateProductAllocations(
//             subAllocationDictionary,
//             request.UserInput
//         );

//         if (result == null || result.Count == 0)
//         {
//             return NotFound("No product allocations found based on the provided sub-allocation data.");
//         }

//         return Ok(result);
//     }
//     catch (Exception ex)
//     {
//         _logger.LogError(ex, "Error occurred while calculating product allocations.");
//         return StatusCode(500, $"Internal server error: {ex.Message}");
//     }
// }
//     }
// }

// [HttpPost("calculate-product-allocations")]
// public async Task<IActionResult> CalculateProductAllocations([FromBody] AllocationResultDTO allocationResult)
// {
//     if (allocationResult == null || allocationResult.Assets == null)
//     {
//         return BadRequest("Allocation result cannot be null.");
//     }

//     try
//     {
//         // Transform AllocationResultDTO to the required input format for ProductAllocationService
//         var subAllocationResult = allocationResult.Assets.ToDictionary(
//             asset => asset.Key,
//             asset => asset.Value.SubAssets
//         );

//         // Call ProductAllocationService to calculate product allocations
//         var result = await _productAllocationService.CalculateProductAllocations(subAllocationResult, userInput);

//         if (result == null || result.Count == 0)
//         {
//             return NotFound("No product allocations found based on the provided allocation data.");
//         }

//         return Ok(result);
//     }
//     catch (Exception ex)
//     {
//         _logger.LogError(ex, "Error occurred while calculating product allocations.");
//         return StatusCode(500, $"Internal server error: {ex.Message}");
//     }
// }
//     }
// }
[HttpGet("get-product-allocations")]
        public async Task<IActionResult> GetProductAllocations()
        {
            try
            {
                var productAllocations = await _productAllocationService.GetProductAllocationsAsync();
                return Ok(productAllocations); // Return the data as a JSON response
            }
            catch (Exception ex)
            {
                // Log the error and return a 500 Internal Server Error response
                return StatusCode(500, $"An error occurred while retrieving product allocations: {ex.Message}");
            }
        }


        
[HttpPost("calculate-product-allocations")]
public async Task<IActionResult> CalculateProductAllocations(
    [FromBody] AllocationResultDTO allocationResult, [FromQuery] double targetAmount, [FromQuery] string investmentHorizon)
{

    Console.WriteLine($"Sub-allocation Data Received: {JsonSerializer.Serialize(allocationResult)}");
    Console.WriteLine($"Target Amount: {targetAmount}");
    Console.WriteLine($"Investment Horizon: {investmentHorizon}");
    if (allocationResult == null || allocationResult.Assets == null)
    {
        return BadRequest("Allocation result cannot be null.");
    }

    if (targetAmount <= 0)
    {
        return BadRequest("Target amount must be greater than zero.");
    }

    if (string.IsNullOrEmpty(investmentHorizon))
    {
        return BadRequest("Investment horizon is required.");
    }

    try
    {
        // Transform AllocationResultDTO to the required input format for ProductAllocationService
        var subAllocationResult = allocationResult.Assets.ToDictionary(
            asset => asset.Key,
            asset => asset.Value.SubAssets
        );

        // Call ProductAllocationService to calculate product allocations
        var result = await _productAllocationService.CalculateProductAllocations(
            subAllocationResult,
            targetAmount,
            investmentHorizon
        );

        if (result == null || result.Count == 0)
        {
            return NotFound("No product allocations found.");
        }

        return Ok(result);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error occurred while calculating product allocations.");
        return StatusCode(500, $"Internal server error: {ex.Message}");
    }
}

        
    }
}