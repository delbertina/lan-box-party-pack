using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace lan_box_party_pack.server.Hubs;

public sealed class SignalRServerEventsDocumentFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        var events = typeof(IPartyClient)
            .GetMethods()
            .Select(method => CreateEventDocument(method, context))
            .ToArray();

        swaggerDoc.Extensions ??= new Dictionary<string, IOpenApiExtension>();
        swaggerDoc.Extensions["x-signalr-server-events"] = new JsonNodeExtension(
            JsonSerializer.SerializeToNode(events)!);
    }

    private static object CreateEventDocument(MethodInfo method, DocumentFilterContext context)
    {
        var payloadType = method.GetParameters().Single().ParameterType;
        context.SchemaGenerator.GenerateSchema(payloadType, context.SchemaRepository);

        return new
        {
            name = method.Name,
            description = $"Server-to-client SignalR event. Payload type: {payloadType.Name}.",
            payloadSchema = new Dictionary<string, string>
            {
                ["$ref"] = $"#/components/schemas/{payloadType.Name}"
            },
            delivery = method.GetCustomAttribute<SignalRServerEventAttribute>()?.Delivery
        };
    }
}
