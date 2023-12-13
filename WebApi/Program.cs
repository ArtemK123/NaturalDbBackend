using System.Reflection;
using Microsoft.Extensions.Options;
using WebApi;
using WebApi.Options;
using WebApi.Services;

var localFrontendCorsPolicyName = "LocalFrontendCorsPolicy";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy(name: localFrontendCorsPolicyName,
        policy  =>
        {
            policy.WithOrigins("http://localhost:3000")
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
});

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));

builder.Services.Configure<OpenAiOptions>(builder.Configuration.GetSection(OpenAiOptions.SectionPath));
builder.Services.AddTransient<OpenAiOptions>(s => s.GetRequiredService<IOptions<OpenAiOptions>>().Value);

builder.Services.Configure<BigQueryOptions>(builder.Configuration.GetSection(BigQueryOptions.SectionPath));
builder.Services.AddTransient<BigQueryOptions>(s => s.GetRequiredService<IOptions<BigQueryOptions>>().Value);

builder.Services.AddTransient<IOpenAiMessageSender, OpenAiMessageSender>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseCors(localFrontendCorsPolicyName);
}

app.UseSwagger();
app.UseSwaggerUI(o => {
    o.EnableTryItOutByDefault();
});

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();