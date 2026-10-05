using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using What_To_Do.Data;
using What_To_Do.Domain.Entities;
using What_To_Do.Features.Auth;

namespace What_To_Do.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAppDatabase(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(config.GetConnectionString("DefaultConnection")));
        return services;
    }

    // Identity (no cookie sign-in — the API is JWT-only) + JWT bearer validation.
    public static IServiceCollection AddAppAuthentication(this IServiceCollection services, IConfiguration config)
    {
        var jwtKey = config["Jwt:Key"];
        if (string.IsNullOrWhiteSpace(jwtKey))
            throw new InvalidOperationException(
                "Jwt:Key is not configured. For local development run: " +
                "dotnet user-secrets set \"Jwt:Key\" \"<a random string of at least 32 characters>\"");

        services.AddIdentityCore<AppUser>(opt =>
            {
                opt.Password.RequiredLength = 8;
                opt.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        services.AddScoped<TokenService>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(opt =>
            {
                opt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = config["Jwt:Issuer"],
                    ValidAudience = config["Jwt:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
                };
            });

        services.AddAuthorization();
        return services;
    }

    // Origins the web app is served from (Vite dev server, and common alternates).
    public const string WebAppCorsPolicy = "AllowWebApp";

    public static IServiceCollection AddWebAppCors(this IServiceCollection services)
    {
        services.AddCors(options =>
        {
            options.AddPolicy(WebAppCorsPolicy, policy =>
            {
                policy.WithOrigins(
                        "http://localhost:5173",
                        "http://localhost:3000",
                        "http://localhost:8080")
                      .AllowAnyHeader()
                      .AllowAnyMethod();
            });
        });
        return services;
    }

    // Swagger / OpenAPI with bearer-token support.
    public static IServiceCollection AddAppSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "What To Do API",
                Version = "v1",
                Description = "REST API for the What To Do application"
            });

            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Paste just the JWT — Swagger adds the 'Bearer ' prefix."
            });

            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                    },
                    Array.Empty<string>()
                }
            });
        });
        return services;
    }
}
