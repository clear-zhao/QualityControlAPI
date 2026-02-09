using Microsoft.EntityFrameworkCore;
using QualityControlAPI.Data;
using QualityControlAPI.Services.Auth;
using QualityControlAPI.Services.Crimping;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// 1. 数据库配置
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
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

// 4. Swagger（Swashbuckle）
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 5. 跨域配置
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

var app = builder.Build();

// 6. 中间件管道
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");

// 如果你后面有鉴权/授权，这两行要加上（没用到也不影响）
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.Run();
