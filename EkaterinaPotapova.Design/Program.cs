using EkaterinaPotapova.Design.Endpoints;

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapHelloEndpoint();

app.Run();
