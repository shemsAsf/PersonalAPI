using PersonalAPI.GlobalEndpoints;
using PersonalAPI.Projects.EkaterinaPotapovaDesign;
using PersonalAPI.Projects.Portfolio;

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapHelloEndpoint();
app.MapEkaterinaEndpoint();
app.MapPortfolioEndpoint();

app.Run();
