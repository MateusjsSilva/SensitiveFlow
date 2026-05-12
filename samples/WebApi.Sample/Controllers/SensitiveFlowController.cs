using Microsoft.AspNetCore.Mvc;
using SensitiveFlow.Core.Discovery;
using SensitiveFlow.Diagnostics.Extensions;
using WebApi.Sample.Infrastructure;

namespace WebApi.Sample.Controllers;

[ApiController]
[Route("sensitiveflow")]
public sealed class SensitiveFlowController : ControllerBase
{
    private readonly IServiceProvider _services;

    public SensitiveFlowController(IServiceProvider services)
    {
        _services = services;
    }

    [HttpGet("discovery")]
    public ContentResult Discovery()
    {
        var report = SensitiveDataDiscovery.Scan(typeof(Employee).Assembly);
        return Content(report.ToMarkdown(), "text/markdown");
    }

    [HttpGet("diagnostics")]
    public IActionResult Diagnostics()
    {
        var report = _services.ValidateSensitiveFlow();
        return Ok(new
        {
            report.IsValid,
            report.Diagnostics,
        });
    }
}
