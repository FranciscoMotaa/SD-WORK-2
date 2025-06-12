using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;

namespace SD_Server_Web.Models
{
    public class OceanoDbContext : DbContext
    {
        public OceanoDbContext(DbContextOptions<OceanoDbContext> options)
            : base(options)
        {
        }

        public DbSet<DadosRecebidosServidor> DadosRecebidosServidor { get; set; }
    }
}