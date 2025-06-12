using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SD_Server_Web.Models;
using System.Threading.Tasks;

public class DadosController : Controller
{
    private readonly OceanoDbContext _context;

    public DadosController(OceanoDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var dados = await _context.DadosRecebidosServidor.ToListAsync();
        return View(dados);
    }
}
