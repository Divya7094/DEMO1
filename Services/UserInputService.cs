using WisVestAPI.Services.Interfaces;
using WisVestAPI.Models.DTOs;
using System.Text.Json;

namespace WisVestAPI.Services
{
    public class UserInputService : IUserInputService
    {
        private readonly IAllocationService _allocationService;
        private readonly ILogger<UserInputService> _logger;

        public UserInputService(IAllocationService allocationService, ILogger<UserInputService> logger)
        {
            _allocationService = allocationService;
            _logger = logger;
        }

        public async Task<AllocationResultDTO> HandleUserInput(UserInputDTO input)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input), "User input cannot be null.");

            _logger.LogInformation($"Received input: {JsonSerializer.Serialize(input)}");

            Dictionary<string, Dictionary<string, double>> allocationDictionary;

            try
            {
                allocationDictionary = (await _allocationService.CalculateFinalAllocation(input))
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value as Dictionary<string, double> ?? throw new InvalidCastException($"Value for key '{kvp.Key}' is not a valid Dictionary<string, double>.")
                    );
                if (allocationDictionary == null)
                    throw new InvalidOperationException("Allocation calculation failed.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error during allocation calculation: {ex.Message}");
                throw;
            }

            var result = new AllocationResultDTO();
            try{
                foreach (var allocation in allocationDictionary)
                {
                    if (allocation.Value is Dictionary<string, double> subAssets)
                    {
                        result.Assets[allocation.Key] = new AssetAllocation
                        {
                            Percentage = subAssets.Values.Sum(), // Sum of all sub-assets
                            SubAssets = subAssets // Assign sub-assets directly
                        };
                    }
                    else
                    {
                        throw new InvalidCastException($"Value for key '{allocation.Key}' is not a valid Dictionary<string, double>.");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error while processing allocation: {ex.Message}");
                throw new InvalidOperationException("Error while processing allocation.", ex);
            }

            try{

            _logger.LogInformation($"Calculated allocation: {JsonSerializer.Serialize(result)}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error while serializing result: {ex.Message}");
                throw;
            }

            return result;
        }
    }
}
