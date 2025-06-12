using AnaliseService;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Mvc;
using SD_Server_Web.Models;
using System.Text;

namespace SD_Server_Web.Controllers
{
    public class AnaliseController : Controller
    {
        private readonly OceanoDbContext _context;

        public AnaliseController(OceanoDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Index(string tipo, string operacao)
        {
            // Lê o CSV do servidor (ajuste o caminho conforme necessário)
            string caminhoCSV = @"D:\SD\SD_NOW\server_data.csv";
            string csvConteudo = System.IO.File.ReadAllText(caminhoCSV, Encoding.UTF8);

            // Chama o serviço gRPC
            using var channel = GrpcChannel.ForAddress("http://localhost:5087");
            var client = new Analise.AnaliseClient(channel);
            var resp = await Task.Run(() => client.Analisar(new PedidoAnalise
            {
                Tipo = tipo,
                Operacao = operacao,
                ConteudoCSV = csvConteudo
            }));

            ViewBag.Resultado = resp.Resultado;
            return View();
        }
    }
}
