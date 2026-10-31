using GarantieExpress.Host.Hosting;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});
builder.AddGarantieExpressHost();

var app = builder.Build();
app.UseGarantieExpressHost();

return await app.RunGarantieExpressAsync();
