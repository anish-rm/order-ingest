using Microsoft.AspNetCore.Mvc;
using OrderIngest.Business;
using OrderIngest.Domain;

namespace OrderIngest.Api.Controllers;

[ApiController]
[Route("webhooks")]
public class WebhooksController(
    UberSignatureVerifier signatureVerifier,
    WebhookQueue queue) : ControllerBase
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
        Request.Body.Position = 0;

        var provider = ProviderDetector.Detect(rawBody);
        if (provider is null)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Unrecognized webhook payload",
                detail: "Body is not JSON or matches no known provider shape.");
        }

        // 401, not 400: the request is well-formed but the caller has not
        // proven it is Uber. DoorDash auth is configured out-of-band and is
        // out of scope (see CLAUDE.md); this check is its seam.
        if (provider == OrderProvider.Uber
            && !signatureVerifier.IsValid(rawBody, Request.Headers["X-Uber-Signature"]))
        {
            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Invalid X-Uber-Signature");
        }

        await queue.EnqueueAsync(new WebhookWorkItem(provider.Value, rawBody), ct);

        // 200 with an empty body, per Uber's webhook contract. Processing
        // happens after this response, on the background service.
        return Ok();
    }
}
