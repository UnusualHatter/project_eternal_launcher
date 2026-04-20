using PricingAggregator.Services;
using PricingAggregator.Sources;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
	.AddJsonOptions(options =>
	{
		options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
	});
builder.Services.AddMemoryCache();

builder.Services.AddHttpClient<PricesTfSource>();
builder.Services.AddHttpClient<SteamMarketSource>();

builder.Services.AddScoped<IPricingSource, PricesTfSource>();
builder.Services.AddScoped<IPricingSource, SteamMarketSource>();
builder.Services.AddScoped<PricingAggregatorService>();

var app = builder.Build();

app.MapControllers();

app.Run();
