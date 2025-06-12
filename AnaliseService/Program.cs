using AnaliseService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpc();

var app = builder.Build();

app.MapGrpcService<AnaliseServiceImpl>();

app.MapGet("/", () => "Serviço de análise gRPC ativo!");

app.Run();
