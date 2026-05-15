using Microsoft.AspNetCore.Mvc;
using SensitiveFlow.Core.Interfaces;

namespace Redis.Microservice.Sample.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PseudonymizationController : ControllerBase
{
    private readonly IPseudonymizer _pseudonymizer;
    private readonly ILogger<PseudonymizationController> _logger;

    public PseudonymizationController(
        IPseudonymizer pseudonymizer,
        ILogger<PseudonymizationController> logger)
    {
        _pseudonymizer = pseudonymizer;
        _logger = logger;
    }

    /// <summary>
    /// Anonymizes a sensitive value into a token.
    /// The same value always returns the same token across all service instances.
    /// </summary>
    [HttpPost("anonymize")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AnonymizeResponse>> Anonymize([FromBody] AnonymizeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Value))
        {
            return BadRequest("Value cannot be empty");
        }

        try
        {
            var token = await _pseudonymizer.PseudonymizeAsync(request.Value);
            _logger.LogInformation("Anonymized value to token: {Token}", token);
            return Ok(new AnonymizeResponse { Token = token });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error anonymizing value");
            return StatusCode(500, "Error anonymizing value");
        }
    }

    /// <summary>
    /// Reverses a token back to its original value.
    /// Works across all service instances sharing the same Redis instance.
    /// </summary>
    [HttpPost("deanonymize")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DeanonymizeResponse>> Deanonymize([FromBody] DeanonymizeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return BadRequest("Token cannot be empty");
        }

        try
        {
            var value = await _pseudonymizer.ReverseAsync(request.Token);
            _logger.LogInformation("Deanonymized token: {Token}", request.Token);
            return Ok(new DeanonymizeResponse { Value = value });
        }
        catch (KeyNotFoundException)
        {
            _logger.LogWarning("Token not found: {Token}", request.Token);
            return NotFound("Token not found or has expired");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deanonymizing token");
            return StatusCode(500, "Error deanonymizing token");
        }
    }

    /// <summary>
    /// Health check for the pseudonymization service.
    /// Verifies Redis connectivity and service readiness.
    /// </summary>
    [HttpGet("health")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<HealthResponse>> Health()
    {
        try
        {
            var testValue = "health-check";
            var token = await _pseudonymizer.PseudonymizeAsync(testValue);
            await _pseudonymizer.ReverseAsync(token);

            return Ok(new HealthResponse { Status = "healthy", Timestamp = DateTime.UtcNow });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");
            return StatusCode(503, new HealthResponse { Status = "unhealthy", Error = ex.Message });
        }
    }
}

public record AnonymizeRequest(string Value);
public record AnonymizeResponse { public required string Token { get; init; } }

public record DeanonymizeRequest(string Token);
public record DeanonymizeResponse { public required string Value { get; init; } }

public record HealthResponse
{
    public required string Status { get; init; }
    public DateTime? Timestamp { get; init; }
    public string? Error { get; init; }
}
