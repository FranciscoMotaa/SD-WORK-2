using Grpc.Core;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using AnaliseService; // Namespace gerado pelo proto

namespace AnaliseService.Services
{
    public class AnaliseServiceImpl : Analise.AnaliseBase
    {
        public override Task<ResultadoAnalise> Analisar(PedidoAnalise request, ServerCallContext context)
        {
            var linhas = request.ConteudoCSV.Split('\n', System.StringSplitOptions.RemoveEmptyEntries);
            var valores = new List<double>();

            int tipoIdx = -1, valorIdx = -1;
            bool cabecalho = true;

            foreach (var linha in linhas)
            {
                var cols = linha.Trim().Split(';');
                if (cabecalho)
                {
                    for (int i = 0; i < cols.Length; i++)
                    {
                        if (cols[i].Trim().ToUpper() == "TIPO") tipoIdx = i;
                        if (cols[i].Trim().ToUpper() == "VALOR") valorIdx = i;
                    }
                    cabecalho = false;
                    continue;
                }

                if (tipoIdx == -1 || valorIdx == -1)
                    break;

                if (cols.Length > System.Math.Max(tipoIdx, valorIdx) && cols[tipoIdx].Trim() == request.Tipo)
                {
                    if (double.TryParse(cols[valorIdx], NumberStyles.Any, CultureInfo.InvariantCulture, out double val))
                        valores.Add(val);
                }
            }

            string resultado = "Sem dados";
            if (valores.Count > 0)
            {
                switch (request.Operacao.ToLower())
                {
                    case "media":
                        resultado = $"Média: {valores.Average():F2}";
                        break;
                    case "contagem":
                        resultado = $"Contagem: {valores.Count}";
                        break;
                    case "minimo":
                        resultado = $"Mínimo: {valores.Min():F2}";
                        break;
                    case "maximo":
                        resultado = $"Máximo: {valores.Max():F2}";
                        break;
                    default:
                        resultado = "Operação não suportada";
                        break;
                }
            }

            return Task.FromResult(new ResultadoAnalise { Resultado = resultado });
        }
    }
}
