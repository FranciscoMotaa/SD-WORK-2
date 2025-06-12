using Grpc.Core;
using System;
using System.Collections.Generic;
using PreprocessamentoService;
using System.Globalization;

namespace PreprocessamentoService.Services
{
    public class PreProcessamentoService : PreProcessamento.PreProcessamentoBase
    {
        public override Task<DadoCSV> Processar(DadoOriginal request, ServerCallContext context)
        {
            string csv = ConversorParaCSV(request.Formato, request.Conteudo);
            return Task.FromResult(new DadoCSV { Conteudo = csv });
        }

        private string ConversorParaCSV(string formato, string conteudo)
        {
            // Cabeçalho fixo:
            const string header = "WAVY_ID;TIPO;VALOR;DATA";
            var fmt = formato.Trim().ToLower();
            var linhasSaida = new List<string> { header };

            try
            {
                if (fmt == "csv")
                {
                    // ex.: "WAVY_001,temperatura,20.5,2025-05-27 14:22:33"
                    var linhas = new List<string>();
                    foreach (var linha in conteudo.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                    {
                        var cols = linha.Split(',', StringSplitOptions.RemoveEmptyEntries);
                        if (cols.Length < 4)
                            continue;

                        // cols[2] já contém o ponto como separador decimal
                        string wavy = cols[0].Trim();
                        string tipo = cols[1].Trim();
                        string valor = cols[2].Trim().Replace(',', '.');
                        if (double.TryParse(valor, NumberStyles.Any, CultureInfo.InvariantCulture, out double valNum))
                            valor = valNum.ToString(CultureInfo.InvariantCulture);
                        string data = cols[3].Trim();
                        if (data.Length > 10)
                            data = data.Substring(0, 10);

                        linhas.Add($"{wavy};{tipo};{valor};{data}");
                    }
                    // devolve só os registos, sem cabeçalho
                    return string.Join('\n', linhas);
                }

                if (fmt == "txt")
                {
                    // ex.: "WAVY_001|temperatura|20,5|2025-05-27 14:22:33"
                    var cols = conteudo.Split('|', StringSplitOptions.RemoveEmptyEntries);
                    if (cols.Length >= 4)
                    {
                        var valStr = cols[2].Trim().Replace(',', '.');
                        if (double.TryParse(valStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double valNum))
                            valStr = valNum.ToString(CultureInfo.InvariantCulture);
                        var data = cols[3].Trim();
                        if (data.Length > 10) data = data.Substring(0, 10);
                        linhasSaida.Add($"{cols[0]};{cols[1]};{valStr};{data}");
                    }
                    return string.Join('\n', linhasSaida);
                }

                if (fmt == "json")
                {
                    // ex.: {"wavy":"WAVY_001","tipo":"ph","valor":8.4,"data":"2025-05-27 14:22:33"}
                    string wavy = Entre(conteudo, "\"wavy\":\"", "\"");
                    string tipo = Entre(conteudo, "\"tipo\":\"", "\"");
                    string valor = Entre(conteudo, "\"valor\":", ",").Trim().Replace(',', '.');
                    if (double.TryParse(valor, NumberStyles.Any, CultureInfo.InvariantCulture, out double valNum))
                        valor = valNum.ToString(CultureInfo.InvariantCulture);
                    string data = Entre(conteudo, "\"data\":\"", "\"");
                    if (data.Length > 10) data = data.Substring(0, 10);

                    // força ponto decimal
                    valor = valor.Replace(',', '.');
                    if (!string.IsNullOrEmpty(wavy) &&
                        !string.IsNullOrEmpty(tipo) &&
                        !string.IsNullOrEmpty(valor) &&
                        !string.IsNullOrEmpty(data))
                    {
                        linhasSaida.Add($"{wavy};{tipo};{valor};{data}");
                    }
                    return string.Join('\n', linhasSaida);
                }

                if (fmt == "xml")
                {
                    // ex.: <medicao><wavy>WAVY_001</wavy>…</medicao>
                    string wavy = Entre(conteudo, "<wavy>", "</wavy>");
                    string tipo = Entre(conteudo, "<tipo>", "</tipo>");
                    string valor = Entre(conteudo, "<valor>", "</valor>").Trim().Replace(',', '.');
                    if (double.TryParse(valor, NumberStyles.Any, CultureInfo.InvariantCulture, out double valNum))
                        valor = valNum.ToString(CultureInfo.InvariantCulture);
                    string data = Entre(conteudo, "<data>", "</data>");
                    if (data.Length > 10) data = data.Substring(0, 10);

                    if (!string.IsNullOrEmpty(wavy) &&
                        !string.IsNullOrEmpty(tipo) &&
                        !string.IsNullOrEmpty(valor) &&
                        !string.IsNullOrEmpty(data))
                    {
                        linhasSaida.Add($"{wavy};{tipo};{valor};{data}");
                    }
                    return string.Join('\n', linhasSaida);
                }

                // Formato desconhecido: devolve apenas cabeçalho
                return "";
            }
            catch
            {
                // Em caso de erro, devolve somente o cabeçalho
                return "";
            }
        }

        // Extrai texto entre ini…fim
        private string Entre(string texto, string ini, string fim)
        {
            int i = texto.IndexOf(ini, StringComparison.InvariantCultureIgnoreCase);
            if (i < 0) return "";
            i += ini.Length;
            int f = texto.IndexOf(fim, i, StringComparison.InvariantCultureIgnoreCase);
            if (f < 0) return texto.Substring(i).Trim();
            return texto.Substring(i, f - i).Trim();
        }
    }
}
