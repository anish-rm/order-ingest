using OrderIngest.Business;
using OrderIngest.Domain;

namespace OrderIngest.Api.Endpoints;

public static class WebhookEndpoints
{
    public static void MapWebhookEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/webhooks/orders", HandleAsync);
    }

    private static async Task<IResult> HandleAsync(
        HttpRequest request,
        UberSignatureVerifier signatureVerifier,
        WebhookQueue queue,
        CancellationToken ct)
    {
        // Buffer so the body can be rewound after this single read — the
        // signature must be computed over the exact raw bytes.
        request.EnableBuffering();
        string rawBody;
        using (var reader = new StreamReader(request.Body, leaveOpen: true))
        {
            rawBody = await reader.ReadToEndAsync(ct);
        }
        request.Body.Position = 0;

        var provider = ProviderDetector.Detect(rawBody);
        if (provider is null)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Unrecognized webhook payload",
                detail: "Body is not JSON or matches no known provider shape.");
        }

        // 401, not 400: the request is well-formed but the caller has not
        // proven it is Uber. DoorDash auth is configured out-of-band and is
        // out of scope (see CLAUDE.md); this check is its seam.
        if (provider == OrderProvider.Uber
            && !signatureVerifier.IsValid(rawBody, request.Headers["X-Uber-Signature"]))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Invalid X-Uber-Signature");
        }

        await queue.EnqueueAsync(new WebhookWorkItem(provider.Value, rawBody), ct);

        // 200 with an empty body, per Uber's webhook contract. Processing
        // happens after this response, on the background service.
        return Results.Ok();
    }
}
