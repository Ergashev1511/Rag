using Microsoft.OpenApi.Models;

namespace Rag.Api.Security;

internal static class SwaggerSetup
{
    /// <summary>Swagger UI da X-Api-Key va X-User-Id header larini kiritish imkoniyati.</summary>
    public static IServiceCollection AddSwagger(this IServiceCollection services) =>
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo { Title = "Dashboard AI Assistant (RAG)", Version = "v1" });

            AddHeader(options, ApiKeyEndpointFilter.HeaderName, "Hujjat boshqaruvi uchun admin kaliti");
            AddHeader(options, HttpCurrentUser.HeaderName, "Chat uchun foydalanuvchi ID si");
        });

    private static void AddHeader(Swashbuckle.AspNetCore.SwaggerGen.SwaggerGenOptions options, string header, string description)
    {
        options.AddSecurityDefinition(header, new OpenApiSecurityScheme
        {
            Name = header,
            Description = description,
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.ApiKey
        });

        options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = header } }] = []
        });
    }
}
