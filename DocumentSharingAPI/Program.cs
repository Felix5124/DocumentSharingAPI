using DocumentSharingAPI.Models; // Assuming AppDbContext is here or in Data
using DocumentSharingAPI.Repositories;
using DocumentSharingAPI.Services;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.FileProviders;
using System.IO;
using System.Security.Claims; // Required for ClaimTypes
using Microsoft.Extensions.Logging; // Required for ILogger

var builder = WebApplication.CreateBuilder(args);

// Add ILogger for detailed logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();


// --- Firebase Admin SDK Initialization ---
var firebaseConfig = builder.Configuration.GetSection("Firebase");
var adminSdkJsonFile = firebaseConfig.GetValue<string>("AdminSdkJsonFile");
var projectId = firebaseConfig.GetValue<string>("ProjectId"); // Ensure this is correctly read

if (string.IsNullOrEmpty(projectId))
{
    // This is a critical configuration. Log an error or throw.
    // Using Console.WriteLine for simplicity, but ILogger is better once app runs.
    Console.WriteLine("CRITICAL ERROR: Firebase:ProjectId is not configured in appsettings.json or environment variables. JWT Authentication will likely fail.");
    // For development, you might hardcode it temporarily if appsettings isn't working, but fix appsettings.
    // projectId = "YOUR-FALLBACK-PROJECT-ID"; 
}

var firebaseAdminSdkPath = Path.Combine(Directory.GetCurrentDirectory(), adminSdkJsonFile ?? "firebase_adminsdk.json"); // Provide a default if null

if (FirebaseApp.DefaultInstance == null) // Check if already initialized
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
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error initializing Firebase Admin SDK from {firebaseAdminSdkPath}: {ex.Message}");
        }
    }
    else
    {
        Console.WriteLine($"Firebase Admin SDK JSON file not found at: {firebaseAdminSdkPath}. Firebase dependent services might not work.");
    }
}
else
{
    Console.WriteLine("Firebase Admin SDK already initialized.");
}


// --- CORS Configuration ---
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", corsBuilder =>
    {
        corsBuilder.WithOrigins("http://localhost:5173") // Your frontend URL
                   .AllowAnyMethod()
                   .AllowAnyHeader()
                   .AllowCredentials(); // If you use credentials like cookies or auth headers from frontend
    });
});

// --- Controller and JSON Options ---
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.WriteIndented = true;
    });

// --- JWT Bearer Authentication with Firebase ID Tokens ---
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = $"https://securetoken.google.com/{projectId}"; // Use the projectId read from config
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuers = new[] { $"https://securetoken.google.com/{projectId}" },
            ValidateAudience = true,
            ValidAudiences = new[] { projectId },
            ValidateLifetime = true
        };
        // THIS IS THE CRUCIAL PART FOR ROLE MAPPING
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                var loggerFactory = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>();
                var logger = loggerFactory.CreateLogger("JwtBearerEvents"); // Create a logger instance

                logger.LogInformation("--- OnTokenValidated Event Triggered ---");
                var claimsIdentity = context.Principal?.Identity as ClaimsIdentity;

                if (claimsIdentity != null && context.Principal != null)
                {
                    logger.LogInformation("Token Principal Name (usually Firebase UID from 'sub' claim): {PrincipalName}", context.Principal.Identity?.Name);
                    logger.LogInformation("Token AuthenticationType: {AuthType}", context.Principal.Identity?.AuthenticationType);

                    logger.LogInformation("All claims in the validated token from Firebase:");
                    foreach (var claim in context.Principal.Claims)
                    {
                        logger.LogInformation("  Claim Type: [{ClaimType}], Claim Value: [{ClaimValue}]", claim.Type, claim.Value);
                    }

                    // Attempt to find the 'admin' claim (this is what your UsersController adds to the custom token)
                    var adminClaim = context.Principal.Claims.FirstOrDefault(c => c.Type == "admin");
                    if (adminClaim != null)
                    {
                        logger.LogInformation("Found 'admin' claim. Raw Value: '{AdminClaimValue}'", adminClaim.Value);
                        // Firebase custom claims often come as strings, even if booleans.
                        if (bool.TryParse(adminClaim.Value, out bool isAdminValue) && isAdminValue)
                        {
                            claimsIdentity.AddClaim(new Claim(ClaimTypes.Role, "Admin"));
                            logger.LogInformation("Successfully added 'Admin' role (ClaimTypes.Role) to identity for user.");
                        }
                        else if (adminClaim.Value.Equals("true", StringComparison.OrdinalIgnoreCase)) // Fallback for string "true"
                        {
                            claimsIdentity.AddClaim(new Claim(ClaimTypes.Role, "Admin"));
                            logger.LogInformation("Successfully added 'Admin' role (ClaimTypes.Role) to identity for user (from string 'true').");
                        }
                        else
                        {
                            logger.LogWarning("'admin' claim found, but its value is not recognized as 'true'. Value: {AdminClaimValue}", adminClaim.Value);
                        }
                    }
                    else
                    {
                        logger.LogWarning("'admin' claim NOT found in the token. User will NOT be granted 'Admin' role via this claim.");
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
                // You can add more detailed logging for specific exceptions if needed
                return Task.CompletedTask;
            }
        };
    });

// --- Authorization Policies ---
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Admin", policy => policy.RequireRole("Admin"));
});

// --- Database Context ---
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// --- Dependency Injection for Services and Repositories ---
builder.Services.AddHttpContextAccessor(); // Useful for accessing HttpContext in services
builder.Services.AddScoped<IImageService, ImageService>();
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

// --- API Explorer and Swagger ---
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

// --- Configure the HTTP request pipeline ---
var filesPath = Path.Combine(Directory.GetCurrentDirectory(), "Files");
if (!Directory.Exists(filesPath))
{
    Directory.CreateDirectory(filesPath);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "DocumentSharingAPI v1"));
    app.UseDeveloperExceptionPage(); // More detailed errors in dev
}

app.UseHttpsRedirection();

// Serve static files from the "Files" directory
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(filesPath),
    RequestPath = "/Files" // URL path to access these files
});

app.UseCors("AllowFrontend"); // Apply CORS policy

app.UseRouting(); // Must be before UseAuthentication and UseAuthorization

app.UseAuthentication(); // Enable authentication middleware
app.UseAuthorization();  // Enable authorization middleware

app.MapControllers();

app.Run();
