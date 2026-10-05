using What_To_Do.Common.Json;
using What_To_Do.Extensions;

var builder = WebApplication.CreateBuilder(args);

// ── Services ───────────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
        opts.JsonSerializerOptions.Converters.Add(new UtcDateTimeConverter());
    });

builder.Services
    .AddAppDatabase(builder.Configuration)
    .AddAppAuthentication(builder.Configuration)
    .AddWebAppCors()
    .AddAppSwagger();

var app = builder.Build();

// ── Middleware ─────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "What To Do API v1");
    });
}

app.UseHttpsRedirection();
app.UseCors(ServiceCollectionExtensions.WebAppCorsPolicy);
app.UseAuthentication();   // MUST come before UseAuthorization
app.UseAuthorization();
app.MapControllers();
app.Run();
