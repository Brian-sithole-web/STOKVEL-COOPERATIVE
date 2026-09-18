using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Stokvel.Api.Middleware;
using Stokvel.Domain;
using Stokvel.Infrastructure;
using Stokvel.Infrastructure.Identity;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var connectionString = builder.Configuration.GetConnectionString("Default")
                       ?? "Data Source=stokvel.db";
builder.Services.AddStokvelInfrastructure(connectionString);

var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("Configure Jwt:Key.");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddCors(o => o.AddPolicy("spa", p =>
    p.WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:5173", "http://localhost:5174"])
        .AllowAnyHeader()
        .AllowAnyMethod()));

var app = builder.Build();
await app.Services.InitializeDatabaseAsync();
if (app.Environment.IsDevelopment())
    await EnsureDevelopmentAdministratorAsync(app.Services);

app.UseMiddleware<ExceptionMiddleware>();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("spa");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();

static async Task EnsureDevelopmentAdministratorAsync(IServiceProvider services)
{
    const string email = "admin@pkvela.coop";
    const string password = "Pkvela#Admin1";

    using var scope = services.CreateScope();
    var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var administrator = await users.FindByEmailAsync(email);
    if (administrator is null)
    {
        administrator = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = "Platform Administrator",
            EmailConfirmed = true,
            MustChangePassword = false
        };
        var created = await users.CreateAsync(administrator, password);
        if (!created.Succeeded)
            throw new InvalidOperationException(string.Join(" ", created.Errors.Select(error => error.Description)));
    }
    else
    {
        var resetToken = await users.GeneratePasswordResetTokenAsync(administrator);
        var reset = await users.ResetPasswordAsync(administrator, resetToken, password);
        if (!reset.Succeeded)
            throw new InvalidOperationException(string.Join(" ", reset.Errors.Select(error => error.Description)));
    }

    if (!await users.IsInRoleAsync(administrator, SystemRoles.PlatformAdmin))
        await users.AddToRoleAsync(administrator, SystemRoles.PlatformAdmin);
}
