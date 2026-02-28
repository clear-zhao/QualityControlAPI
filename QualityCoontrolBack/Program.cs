using Microsoft.EntityFrameworkCore;
using QualityControlAPI.Data;
using QualityControlAPI.Services.Auth;
using QualityControlAPI.Services.Crimping;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);


// 1. ݿ
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
// 安全校验：连接串缺失时直接阻止启动，避免运行期数据库初始化报错
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("缺少数据库连接字符串: DefaultConnection");
}
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(
        connectionString,
        ServerVersion.AutoDetect(connectionString),
        mySqlOptions =>
        {
            // Auto retry for transient DB/network failures to improve self-recovery
            mySqlOptions.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null);
        }));

// 2. 
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// 3. עҵ
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<CrimpingService>();

// 4. SwaggerSwashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 5. 
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});


var app = builder.Build();

// 6. 
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsDevelopment())
{
    // Global exception fallback to keep service stable during unexpected failures
    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("GlobalException");
            var exceptionFeature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
            if (exceptionFeature != null)
            {
                logger.LogError(exceptionFeature.Error, "Unhandled exception on {Method} {Path}", context.Request.Method, context.Request.Path);
            }

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";

            var payload = new
            {
                message = "服务器内部错误，请稍后重试",
                traceId = context.TraceIdentifier
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
        });
    });
}

app.UseCors("AllowAll");

// 
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.Run();