using DocumentSharingAPI.Models;
using DocumentSharingAPI.Repositories;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.FileProviders;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// Firebase Admin SDK Initialization
var firebaseConfig = builder.Configuration.GetSection("Firebase");
var adminSdkJsonFile = firebaseConfig.GetValue<string>("AdminSdkJsonFile") ?? "documentsharingapp-33403-firebase-adminsdk-fbsvc-97cac5a7d5.json";
var projectId = firebaseConfig.GetValue<string>("ProjectId");

if (string.IsNullOrEmpty(projectId))
{
    Console.WriteLine("CRITICAL ERROR: Firebase:ProjectId is not configured in appsettings.json.");
    throw new InvalidOperationException("Firebase ProjectId is required.");
}

var firebaseAdminSdkPath = Path.Combine(Directory.GetCurrentDirectory(), adminSdkJsonFile);
if (FirebaseApp.DefaultInstance == null)
{
    if (File.Exists(firebaseAdminSdkPath))
    {
        try
        {
            FirebaseApp.Create(new AppOptions()
            {
                Credential = GoogleCredential.FromFile(firebaseAdminSdkPath)
            });
            Console.WriteLine("Firebase Admin SDK initialized successfully from: " + firebaseAdminSdkPath);
            Console.WriteLine("Firebase Project ID: " + projectId);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error initializing Firebase Admin SDK from {firebaseAdminSdkPath}: {ex.Message}");
            Console.WriteLine($"StackTrace: {ex.StackTrace}");
            throw;
        }
    }
    else
    {
        Console.WriteLine($"Firebase Admin SDK JSON file not found at: {firebaseAdminSdkPath}. Firebase services may not work.");
        throw new FileNotFoundException($"Firebase Admin SDK JSON file not found at: {firebaseAdminSdkPath}");
    }
}
else
{
    Console.WriteLine("Firebase Admin SDK already initialized.");
}

// CORS Configuration
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", corsBuilder =>
    {
        corsBuilder.WithOrigins("http://localhost:5173")
                   .AllowAnyMethod()
                   .AllowAnyHeader()
                   .AllowCredentials();
    });
});

// Controllers and JSON Options
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.WriteIndented = true;
    });

// JWT Authentication with Firebase
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = $"https://securetoken.google.com/{projectId}";
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuers = new[] { $"https://securetoken.google.com/{projectId}" },
            ValidateAudience = true,
            ValidAudiences = new[] { projectId },
            ValidateLifetime = true
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                var loggerFactory = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>();
                var logger = loggerFactory.CreateLogger("JwtBearerEvents");
                logger.LogInformation("--- OnTokenValidated Event Triggered ---");
                var claimsIdentity = context.Principal?.Identity as ClaimsIdentity;

                if (claimsIdentity != null && context.Principal != null)
                {
                    logger.LogInformation("Token Principal Name: {PrincipalName}", context.Principal.Identity?.Name);
                    logger.LogInformation("Token AuthenticationType: {AuthType}", context.Principal.Identity?.AuthenticationType);

                    foreach (var claim in context.Principal.Claims)
                    {
                        logger.LogInformation("  Claim Type: [{ClaimType}], Claim Value: [{ClaimValue}]", claim.Type, claim.Value);
                    }

                    var adminClaim = context.Principal.Claims.FirstOrDefault(c => c.Type == "admin");
                    if (adminClaim != null)
                    {
                        logger.LogInformation("Found 'admin' claim. Raw Value: '{AdminClaimValue}'", adminClaim.Value);
                        if (bool.TryParse(adminClaim.Value, out bool isAdminValue) && isAdminValue ||
                            adminClaim.Value.Equals("true", StringComparison.OrdinalIgnoreCase))
                        {
                            claimsIdentity.AddClaim(new Claim(ClaimTypes.Role, "Admin"));
                            logger.LogInformation("Successfully added 'Admin' role to identity.");
                        }
                        else
                        {
                            logger.LogWarning("'admin' claim found, but its value is not recognized as 'true'. Value: {AdminClaimValue}", adminClaim.Value);
                        }
                    }
                    else
                    {
                        logger.LogWarning("'admin' claim NOT found. User will NOT be granted 'Admin' role.");
                    }
                }
                else
                {
                    logger.LogError("ClaimsIdentity or Principal is null in OnTokenValidated.");
                }
                logger.LogInformation("--- End OnTokenValidated Event ---");
                return Task.CompletedTask;
            },
            OnAuthenticationFailed = context =>
            {
                var loggerFactory = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>();
                var logger = loggerFactory.CreateLogger("JwtBearerEvents");
                logger.LogError(context.Exception, "JWT Authentication Failed.");
                return Task.CompletedTask;
            }
        };
    });

// Authorization Policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Admin", policy => policy.RequireRole("Admin"));
});

// Database Context
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Dependency Injection
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<ICommentRepository, CommentRepository>();
builder.Services.AddScoped<IFollowRepository, FollowRepository>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<IPostRepository, PostRepository>();
builder.Services.AddScoped<IPostCommentRepository, PostCommentRepository>();
builder.Services.AddScoped<IRecommendationRepository, RecommendationRepository>();
builder.Services.AddScoped<IBadgeRepository, BadgeRepository>();
builder.Services.AddScoped<IUserBadgeRepository, UserBadgeRepository>();
builder.Services.AddScoped<IUserDocumentRepository, UserDocumentRepository>();

// Swagger Configuration
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "DocumentSharingAPI", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = @"JWT Authorization header using the Bearer scheme. 
                      Enter 'Bearer' [space] and then your token in the text input below.
                      Example: 'Bearer 12345abcdef'",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement()
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                },
                Scheme = "oauth2",
                Name = "Bearer",
                In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            },
            new List<string>()
        }
    });
});

var app = builder.Build();

// Ensure Files directory exists
var filesPath = Path.Combine(Directory.GetCurrentDirectory(), "Files");
if (!Directory.Exists(filesPath))
{
    Directory.CreateDirectory(filesPath);
}

// Configure HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "DocumentSharingAPI v1"));
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();

// Serve static files
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(filesPath),
    RequestPath = "/Files"
});
app.UseStaticFiles();
app.UseCors("AllowFrontend");
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();