using Microsoft.AspNetCore.Mvc;
using WisVestAPI.Models.DTOs;
using WisVestAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
namespace WisVestAPI.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class UserInputController : ControllerBase
    {
        private readonly IUserInputService _userInputService;

        public UserInputController(IUserInputService userInputService)
        {
            _userInputService = userInputService;
        }

        [HttpPost("submit-input")]
        public async Task<IActionResult> SubmitInput([FromBody] UserInputDTO input)
        {
            if (input == null) return BadRequest("Input is null");
        
            try
            {
                var result = await _userInputService.HandleUserInput(input);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }
    }
}
