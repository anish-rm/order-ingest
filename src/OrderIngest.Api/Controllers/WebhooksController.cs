using Microsoft.AspNetCore.Mvc;
using OrderIngest.Business;
using OrderIngest.Domain;

namespace OrderIngest.Api.Controllers;

[ApiController]
[Route("webhooks")]
public class WebhooksController(
    UberSignatureVerifier signatureVerifier,
    WebhookQueue queue,
    ILogger<WebhooksController> logger) : ControllerBase
{
    [HttpPost("orders")]
    public async Task<IActionResult> ReceiveAsync(CancellationToken ct)
    {
        // No [FromBody] parameter: the body is read manually because the
        // signature must be computed over the exact raw bytes. Buffer so
        // the stream can be rewound after this single read.
        Request.EnableBuffering();
        string rawBody;
        using (var reader = new StreamReader(Request.Body, leaveOpen: true))
        {
            rawBody = await reader.ReadToEndAsync(ct);
        }

        // Rewind so middleware behind the controller can still read the body.
        Request.Body.Position = 0;

        var provider = ProviderDetector.Detect(rawBody);
        if (provider is null)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Unrecognized webhook payload",
                detail: "Body is not JSON or matches no known provider shape.");
        }

        // Uber's webhook auth is publicly specified, so it is verified here.
        // DoorDash's is negotiated during merchant onboarding and not
        // publicly documented; its check would plug in at this same point.
        if (provider == OrderProvider.Uber
            && !signatureVerifier.IsValid(rawBody, Request.Headers["X-Uber-Signature"]))
        {
            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Invalid X-Uber-Signature");
        }

        var correlationId = WebhookCorrelation.Extract(provider.Value, rawBody);
        logger.LogInformation(
            "Accepted {Provider} webhook {CorrelationId}", provider, correlationId);

        await queue.EnqueueAsync(new WebhookWorkItem(provider.Value, rawBody, correlationId), ct);

        // 200 with an empty body, per Uber's webhook contract. Processing
        // happens after this response, on the background service.
        return Ok();
    }
}
