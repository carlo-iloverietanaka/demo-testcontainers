using Microsoft.AspNetCore.Mvc;
using SMS.API.Input;
using SMS.Application.Output;
using SMS.Application.Services.Sms;

namespace SMS.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/sms")]
[Produces("application/json")]
public class SmsController : ControllerBase
{
    private readonly ISmsService _service;

    public SmsController(ISmsService service)
    {
        _service = service;
    }

    [HttpGet("{externalId}")]
    [ProducesResponseType(typeof(SmsOutput), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetSms(string externalId)
    {
        var item = await _service.FindByExternalId(externalId);

        if (item is YieldError error)
        {
            return Problem(statusCode: error.Code, title: error.Error, detail: error.ErrorDescription);
        }

        if (item is not YieldOk<SmsOutput> fine)
        {
            return Problem(statusCode: StatusCodes.Status500InternalServerError,
                title: "Internal Server Error",
                detail: "An unexpected error occurred while processing your request.");
        }

        return Ok(fine.Value);
    }

    [HttpPost]
    [ProducesResponseType(typeof(SmsOutput), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> CreateSms(CreateSmsInput request)
    {
        var item = await _service.CreateSms(new SmsInput(request.Content, request.ContentType, request.Sender,
            request.Recipient, request.Medium));

        if (item is YieldError error)
        {
            return Problem(statusCode: error.Code, title: error.Error, detail: error.ErrorDescription);
        }

        if (item is not YieldOk<SmsOutput> fine)
        {
            return Problem(statusCode: StatusCodes.Status500InternalServerError,
                title: "Internal Server Error",
                detail: "An unexpected error occurred while processing your request.");
        }

        return Created($"/api/v1/sms/{fine.Value.ExternalId}", fine.Value);
    }
}