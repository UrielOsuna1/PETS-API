using PA_BACKEND.Data;
using PA_BACKEND.Middleware;

var builder = WebApplication.CreateBuilder(args);

// cargar configuración
builder.Configuration.AddJsonFile("Properties/launchSettings.json", optional: true, reloadOnChange: true);
builder.Configuration.AddEnvironmentVariables();

builder.Services.AddControllers();


// ✅ CORS (SIN AFECTAR FUNCIONALIDAD)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy
            .WithOrigins(
                "https://pets-front-production.up.railway.app",
                "http://localhost:4200"
            )
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});


// manejo de errores de modelo
builder.Services.Configure<Microsoft.AspNetCore.Mvc.ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState.Values
            .SelectMany(v => v.Errors)
            .Select(e => e.ErrorMessage)
            .ToList();

        var response = new PA_BACKEND.DTOs.Common.ResponseAPIDTO<object>
        {
            Success = false,
            Data = new object(),
            Message = errors.Count > 0
                ? string.Join("; ", errors)
                : PA_BACKEND.DTOs.Common.SecureMessages.ValidationError,
            ErrorCode = PA_BACKEND.DTOs.Common.ErrorCodes.ValidationError
        };

        return new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(response);
    };
});


// 🔐 AUTH
builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        options.TokenValidationParameters =
            new Microsoft.IdentityModel.Tokens.TokenValidationParameters
            {
                ValidateAudience = true,
                ValidateIssuer = true,
                ValidateIssuerSigningKey = true,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,

                IssuerSigningKey =
                    new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                        System.Text.Encoding.UTF8.GetBytes(
                            builder.Configuration["Jwt:Key"]
                            ?? throw new InvalidOperationException("JWT Key missing"))),

                ValidIssuer =
                    builder.Configuration["Jwt:Issuer"]
                    ?? throw new InvalidOperationException("JWT Issuer missing"),

                ValidAudience =
                    builder.Configuration["Jwt:Audience"]
                    ?? throw new InvalidOperationException("JWT Audience missing"),

                RoleClaimType = System.Security.Claims.ClaimTypes.Role
            };
    });

builder.Services.AddAuthorization();


// 🧩 DI
builder.Services.AddScoped<PA_BACKEND.Data.Interface.IAuthRepository, PA_BACKEND.Data.Repositories.AuthRepository>();
builder.Services.AddScoped<PA_BACKEND.Data.Interface.ITokenRepository, PA_BACKEND.Data.Repositories.TokenRepository>();
builder.Services.AddScoped<PA_BACKEND.Data.Interface.ICryptoRepository, PA_BACKEND.Data.Repositories.CryptoRepository>();
builder.Services.AddScoped<PA_BACKEND.Data.Interface.IGatewayRepository, PA_BACKEND.Data.Repositories.GatewayRepository>();

builder.Services.AddSingleton<PostgreSQLConfiguration>();
builder.Services.AddScoped<UsuarioService>();
builder.Services.AddScoped<MascotaService>();
builder.Services.AddScoped<PetStatusService>();
builder.Services.AddScoped<AdoptionRequestService>();

builder.Services.AddSingleton<PA_BACKEND.Data.PostgreSQLConfiguration>();
builder.Services.AddSingleton<IConfiguration>(builder.Configuration);


// 📄 SWAGGER
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "PA Backend API",
        Version = "v1",
        Description = "API para el sistema de protección animal"
    });

    c.AddSecurityDefinition("Bearer",
        new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Description = "Authorization: Bearer {token}",
            Name = "Authorization",
            In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
            Scheme = "Bearer"
        });

    c.AddSecurityRequirement(
        new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
        {
            {
                new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    Reference =
                        new Microsoft.OpenApi.Models.OpenApiReference
                        {
                            Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                },
                Array.Empty<string>()
            }
        });
});

var app = builder.Build();


// 🌐 SWAGGER
app.UseSwagger();

app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "PA Backend API v1");
    c.RoutePrefix = "swagger";
});


// 🛑 manejo global de errores
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";

        var response = new PA_BACKEND.DTOs.Common.ResponseAPIDTO<object>
        {
            Success = false,
            Data = new object(),
            Message = PA_BACKEND.DTOs.Common.SecureMessages.InternalServerError,
            ErrorCode = PA_BACKEND.DTOs.Common.ErrorCodes.InternalError
        };

        await context.Response.WriteAsJsonAsync(response);
    });
});


// ✅ ORDEN CORRECTO PARA RAILWAY + CORS
app.UseRouting();

app.UseCors("AllowAll");

app.UseAuthentication();

app.UseAuthorization();

app.UseAuthorizationHeaderFix();

app.UseTokenBlacklistValidation();

app.MapControllers();


// 🚀 PUERTO PARA RAILWAY
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";

app.Run($"http://0.0.0.0:{port}");