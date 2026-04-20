using Microsoft.AspNetCore.Mvc;
using PricingAggregator.Services;

namespace PricingAggregator.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class PricesController(PricingAggregatorService aggregator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string item, [FromQuery] string? sku, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(item))
            return BadRequest("item is required");

        var result = await aggregator.GetPricesAsync(item.Trim(), sku?.Trim(), ct);
        return Ok(result);
    }
}
