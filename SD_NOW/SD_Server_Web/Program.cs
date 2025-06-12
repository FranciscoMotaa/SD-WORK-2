// 1. Mova todas as diretivas 'using' para o topo do arquivo
using Microsoft.EntityFrameworkCore; // Certifique-se de que este pacote NuGet está instalado
using SD_Server_Web.Models; // Certifique-se de que seu modelo OceanoDbContext está neste namespace

var builder = WebApplication.CreateBuilder(args);

// 2. Adicione serviços ao contêiner.
//    Adiciona o contexto do banco de dados (OceanoDbContext)
builder.Services.AddDbContext<OceanoDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("OceanoDb")));

//    Adiciona suporte para Controllers e Views (para projetos MVC)
builder.Services.AddControllersWithViews();

var app = builder.Build();

// 3. Configure o pipeline de requisições HTTP.
//    Redireciona para página de erro em ambiente de produção
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

//    Redireciona requisições HTTP para HTTPS
app.UseHttpsRedirection();
//    Habilita o uso de arquivos estáticos (CSS, JS, imagens, etc.)
app.UseStaticFiles();

//    Habilita o roteamento de requisições
app.UseRouting();

//    Habilita a autorização (se você tiver autenticação/autorização configurada)
app.UseAuthorization();

//    Mapeia a rota padrão para Controllers e Actions
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();