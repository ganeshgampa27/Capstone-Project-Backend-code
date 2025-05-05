using ResumeBuilderBackend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.OpenApi.Models;
using ResumeBuilderBackend.Services;
using ResumeBuilderBackend.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Logging Configuration
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));

// Detailed logging for specific namespaces
builder.Logging.AddFilter("Microsoft", LogLevel.Warning);
builder.Logging.AddFilter("System", LogLevel.Warning);
builder.Logging.AddFilter("ResumeBuilderBackend", LogLevel.Debug); // Added for debugging
builder.Logging.AddFilter("Loginform", LogLevel.Debug);

// Swagger Configuration with more security options
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Q-Resume Builder API",
        Version = "v1",
        Description = "API for login, registration, and resume building functionality",
        Contact = new OpenApiContact
        {
            Name = "Support Team",
            Email = "support@qresumebuilder.com"
        }
    });

    // Add JWT Authentication to Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT Authorization header using the Bearer scheme."
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });

    // Avoid schema conflicts
    c.CustomSchemaIds(type => type.FullName);
});

// Verify and configure connection string
var connectionString = builder.Configuration.GetConnectionString("RegistrationdbConnection");
ArgumentException.ThrowIfNullOrEmpty(connectionString, "Database connection string 'RegistrationdbConnection'");

// Database Context Configuration
builder.Services.AddDbContext<RegistrationDBcontext>(options =>
    options.UseSqlServer(connectionString));

// Dependency Injection Configuration
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<TemplateService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<IEmailservice, EmailService>();
builder.Services.AddScoped<ResumeService>();

// Configure Email Settings
builder.Services.Configure<EmailSettings>(
    builder.Configuration.GetSection("EmailSettings"));

// JWT Configuration
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("JWT key not found in configuration.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false, // TODO: Set to true and configure ValidIssuer for production
            ValidateAudience = false, // TODO: Set to true and configure ValidAudience for production
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromMinutes(5),
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

// CORS Configuration
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("https://ambitious-smoke-0be5abf00.6.azurestaticapps.net")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // Added to support credentials if needed
    });
});

// Build the application
var app = builder.Build();

// Development-specific configurations
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Q-Resume Builder API v1");
        c.DisplayRequestDuration();
    });
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// Database Migration
try
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<RegistrationDBcontext>();
    context.Database.Migrate();
}
catch (Exception ex)
{
    // Log migration errors
    app.Logger.LogError(ex, "An error occurred while migrating the database.");
}

// Middleware Configuration
app.UseHttpsRedirection();
app.UseCors("AllowFrontend"); // Apply CORS policy
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();



app.Run();