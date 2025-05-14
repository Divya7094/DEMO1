// using System;

// namespace WisVestAPI.Services
// {
//     public class InvestmentAmountService
//     {
//         private readonly ILogger<InvestmentAmountService> _logger;
//         public InvestmentAmountService(ILogger<InvestmentAmountService> logger)
//         {
//             _logger = logger;
            
//         }
//         public double CalculateInvestmentAmount(double percentageSplit, double targetAmount, double annualReturn, string investmentHorizon)
//         {
//             try{
//             // Parse the investment horizon (e.g., "5 years" -> 5)
//             _logger.LogInformation("Calculating investment amount with PercentageSplit: {PercentageSplit}, TargetAmount: {TargetAmount}, AnnualReturn: {AnnualReturn}, InvestmentHorizon: {InvestmentHorizon}.",
//                     percentageSplit, targetAmount, annualReturn, investmentHorizon);
//             int years = investmentHorizon switch
//             {
//                 "Short" => 2,
//                 "Moderate" => 5,
//                 "Long" => 8,
//                 _ => throw new ArgumentException("Invalid investment horizon value") // Handle invalid input
//             };

//             _logger.LogInformation("Parsed investment horizon: {Years} years.", years);

//             // Formula: Investment = (PercentageSplit * TargetAmount) / (1 + AnnualReturn/100)^Years
//             double denominator = Math.Pow(1 + (annualReturn / 100), years);
//             double investmentAmount = (percentageSplit/100) * targetAmount / denominator;
//              _logger.LogInformation("Calculated investment amount: {InvestmentAmount}.", Math.Round(investmentAmount, 2));
//             return Math.Round(investmentAmount, 2); // Round to 2 decimal places
//             }
//                 catch (ArgumentException ex)
//             {
//                 _logger.LogError(ex, "Invalid input: {Message}", ex.Message);
//                 throw ; // Re-throw the exception to propagate it to the caller
//             }
//             catch (DivideByZeroException ex)
//             {
//                 _logger.LogError(ex, "Math error: {Message}", ex.Message);
//                 throw; // Re-throw the exception to propagate it to the caller
//             }
//             catch (OverflowException ex)
//             {
//                 _logger.LogError(ex, "Overflow error: {Message}", ex.Message);
//                 throw; // Re-throw the exception to propagate it to the caller
//             }
//             catch (Exception ex)
//             {
//                 _logger.LogError(ex, "Unexpected error: {Message}", ex.Message);
//                 throw; // Re-throw the exception to propagate it to the caller
//             }
//         }
//     }
// }

using System;

namespace WisVestAPI.Services
{
    public class InvestmentAmountService
    {
        private readonly ILogger<InvestmentAmountService> _logger;

        private const double BASE_PERCENTAGE = 100.0;
        private const double BASE_RATE = 1.0;

        private readonly Dictionary<string, int> _investmentHorizonYears = new()
        {
            { "short", 2 },
            { "moderate", 5 },
            { "long", 8 }
        };

        public InvestmentAmountService(ILogger<InvestmentAmountService> logger)
        {
            _logger = logger;
        }

        public double CalculateInvestmentAmount(double percentageSplit, double targetAmount, double annualReturn, string investmentHorizon)
        {
            try
            {
                // Validate inputs
                if (percentageSplit <= 0 || percentageSplit > BASE_PERCENTAGE)
                    throw new ArgumentException($"Percentage split must be between 1 and {BASE_PERCENTAGE}.");

                if (targetAmount <= 0)
                    throw new ArgumentException("Target amount must be greater than zero.");

                if (annualReturn < 0)
                    throw new ArgumentException("Annual return cannot be negative.");

                if (string.IsNullOrWhiteSpace(investmentHorizon))
                    throw new ArgumentException("Investment horizon cannot be null or empty.");

                _logger.LogInformation("Calculating investment amount with PercentageSplit: {PercentageSplit}, TargetAmount: {TargetAmount}, AnnualReturn: {AnnualReturn}, InvestmentHorizon: {InvestmentHorizon}.",
                    percentageSplit, targetAmount, annualReturn, investmentHorizon);

                // Convert investment horizon using dictionary
                if (!_investmentHorizonYears.TryGetValue(investmentHorizon.ToLower(), out int years))
                    throw new ArgumentException($"Invalid investment horizon value: {investmentHorizon}");

                _logger.LogInformation("Parsed investment horizon: {Years} years.", years);

                // Formula with configurable constants
                double denominator = Math.Pow(BASE_RATE + (annualReturn / BASE_PERCENTAGE), years);
                if (denominator == 0)
                    throw new DivideByZeroException("Denominator in investment calculation is zero.");

                double investmentAmount = (percentageSplit / BASE_PERCENTAGE) * targetAmount / denominator;
                investmentAmount = Math.Round(investmentAmount, 2);

                _logger.LogInformation("Calculated investment amount: {InvestmentAmount}.", investmentAmount);
                return investmentAmount;
            }
            catch (ArgumentException ex)
            {
                _logger.LogError(ex, "Invalid input detected: {Message}", ex.Message);
                throw new ArgumentException("Error processing input parameters", ex);
            }
            catch (DivideByZeroException ex)
            {
                _logger.LogError(ex, "Mathematical error encountered: {Message}", ex.Message);
                throw new DivideByZeroException("Investment calculation failed due to divide by zero.", ex);
            }
            catch (OverflowException ex)
            {
                _logger.LogError(ex, "Overflow error detected: {Message}", ex.Message);
                throw new OverflowException("Calculation resulted in an overflow error.", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error occurred: {Message}", ex.Message);
                throw new InvalidOperationException("An unexpected error occurred during investment calculation.", ex);
            }
        }
    }
}



