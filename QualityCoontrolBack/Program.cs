using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using QualityControlAPI.Data;
using QualityControlAPI.Services.Auth;
using QualityControlAPI.Services.Crimping;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// 1. 数据库配置
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("未找到数据库连接字符串 DefaultConnection。");
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

// 2. 控制器配置
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// 3. 注册业务服务
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<CrimpingService>();

// 4. Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 5. CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

var app = builder.Build();

// 全局异常处理：统一记录日志并返回标准错误，避免泄露内部实现细节。
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var logger = context.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("GlobalExceptionHandler");

        var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
        if (exception != null)
        {
            logger.LogError(exception, "Unhandled exception while processing request {Path}", context.Request.Path);
        }

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";

        await context.Response.WriteAsJsonAsync(new
        {
            message = "服务器内部错误，请稍后重试",
            traceId = context.TraceIdentifier
        });
    });
});

// 6. 中间件管道
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.Run();
