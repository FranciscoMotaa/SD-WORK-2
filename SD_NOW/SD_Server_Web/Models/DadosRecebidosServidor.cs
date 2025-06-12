namespace SD_Server_Web.Models
{
    public class DadosRecebidosServidor
    {
        public int Id { get; set; }
        public string WavyId { get; set; }
        public string Tipo { get; set; }
        public string Valor { get; set; }
        public DateTime DataMedicao { get; set; }
        public string AggregatorId { get; set; }
    }
}
