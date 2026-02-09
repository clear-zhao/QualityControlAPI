using Microsoft.EntityFrameworkCore;
using QualityControlAPI.Data;
using System;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// 1. 获取连接字符串
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// 2. 注册 MySQL 数据库服务
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

// 3. 注册控制器，并配置 JSON 选项 (处理循环引用和枚举)
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// 注册业务服务 (Scoped: 每个请求创建一个实例，适合 Web API)
builder.Services.AddScoped<QualityControlAPI.Services.Auth.AuthService>();
builder.Services.AddScoped<QualityControlAPI.Services.Crimping.CrimpingService>();

// 4. 配置 Swagger (API文档页面)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 5. 配置 CORS (允许前端跨域访问)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// 6. 开启 Swagger UI (方便你调试接口)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// 7. 使用 CORS
app.UseCors("AllowAll");

app.MapControllers();

// 8. 启动应用
app.Run();