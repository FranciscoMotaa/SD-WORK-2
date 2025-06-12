using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Grpc.Net.Client;
using AnaliseService;
using Microsoft.Data.SqlClient; // Adicionar esta diretiva using

namespace Server
{
    class Program
    {
        // Mutex para coordenar acesso ao server_data.csv
        // Voltamos a precisar do mutex porque vamos gravar no CSV novamente
        static readonly Mutex csvMutex = new Mutex();

        // Ficheiro onde ficam todos os dados
        static readonly string caminhoCSV =
            @"D:\SD\SD_NOW\server_data.csv";

        // String de conexão para a base de dados OceanoDb
        // ATENÇÃO: Substitui com os teus dados reais!
        static readonly string connectionString = 
            "Server=(localdb)\\MSSQLLocalDB;Database=OceanoDb;Trusted_Connection=True;TrustServerCertificate=True;";

        static void Main(string[] args)
        {
            Console.WriteLine("=== SERVIDOR - Espera por Agregadores ===");

            // 1) Inicializa o CSV com cabeçalho
            InicializarCSV();

            // 2) Arranca menu de análise em background
            var menuThread = new Thread(MenuAnalise) { IsBackground = true };
            menuThread.Start();

            // 3) Escuta TCP na porta 7000
            var listener = new TcpListener(IPAddress.Any, 7000);
            listener.Start();
            Console.WriteLine("[SERVIDOR] A escutar na porta 7000...\n");

            while (true)
            {
                var client = listener.AcceptTcpClient();
                new Thread(() => ProcessarAgregador(client)).Start();
            }
        }

        static void InicializarCSV()
        {
            csvMutex.WaitOne();
            try
            {
                // Adicionar "AGREGADOR_ID" ao cabeçalho
                // Certifica-te de que esta linha é executada para teres o cabeçalho correto
                File.WriteAllText(caminhoCSV,
                    "TIMESTAMP;WAVY_ID;TIPO;VALOR;AGREGADOR_ID\n", Encoding.UTF8);
            }
            finally
            {
                csvMutex.ReleaseMutex();
            }
        }

        static void ProcessarAgregador(TcpClient client)
        {
            try
            {
                using var stream = client.GetStream();
                var buffer = new byte[16_384];
                int lidos = stream.Read(buffer, 0, buffer.Length);
                if (lidos <= 0) return;

                // 1) Recebe o CSV completo do agregador
                var payload = Encoding.UTF8.GetString(buffer, 0, lidos);
                Console.WriteLine("[SERVIDOR] Dados recebidos:\n" + payload);

                // 2) Separa em linhas e ignora o cabeçalho do agregador
                var linhas = payload
                    .Replace("\r", "")
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Skip(1)    // salta "WAVY_ID;TIPO;VALOR;AGREGADOR_ID;DATA"
                    .ToArray();

                if (linhas.Length == 0)
                {
                    Console.WriteLine("[SERVIDOR] Nenhuma linha de dados para gravar.");
                }
                else
                {
                    // --- TENTATIVA DE GRAVAÇÃO NA BASE DE DADOS (NOVA PARTE) ---
                    try
                    {
                        using (SqlConnection connection = new SqlConnection(connectionString))
                        {
                            connection.Open();
                            Console.WriteLine("[SERVIDOR] Conectado à base de dados.");

                            foreach (var linha in linhas)
                            {
                                var cols = linha.Split(';');

                                if (cols.Length != 5)
                                {
                                    Console.WriteLine($"[SERVIDOR] DB: Linha ignorada devido a formato inválido ou incompleto: {linha}");
                                    continue;
                                }

                                string wavyId = cols[0];
                                string tipo = cols[1];
                                string valor = cols[2];
                                string dataMedicaoStr = cols[3];
                                string aggregatorId = cols[4];

                                if (!DateTime.TryParse(dataMedicaoStr, out DateTime dataMedicao))
                                {
                                    Console.WriteLine($"[SERVIDOR] DB: Data inválida: {dataMedicaoStr}. Linha ignorada.");
                                    continue;
                                }

                                string query = "INSERT INTO DadosRecebidosServidor (WavyId, Tipo, Valor, DataMedicao, AggregatorId) VALUES (@WavyId, @Tipo, @Valor, @DataMedicao, @AggregatorId)";

                                using (SqlCommand command = new SqlCommand(query, connection))
                                {
                                    command.Parameters.AddWithValue("@WavyId", wavyId);
                                    command.Parameters.AddWithValue("@Tipo", tipo);
                                    command.Parameters.AddWithValue("@Valor", valor);
                                    command.Parameters.AddWithValue("@DataMedicao", dataMedicao);
                                    command.Parameters.AddWithValue("@AggregatorId", aggregatorId);

                                    command.ExecuteNonQuery();
                                    // Console.WriteLine($"[SERVIDOR] DB: Dados inseridos para WavyId: {wavyId}"); // Comentado para evitar spam na consola
                                }
                            }
                            Console.WriteLine($"[SERVIDOR] DB: DADOS GRAVADOS NA BASE DE DADOS\n");
                        }
                    }
                    catch (SqlException sqlEx)
                    {
                        Console.WriteLine("[SERVIDOR] DB: ERRO ao gravar na base de dados: " + sqlEx.Message);
                        // A execução continua, pois o CSV ainda será gravado
                    }
                    // --- FIM DA TENTATIVA DE GRAVAÇÃO NA BASE DE DADOS ---


                    // --- GRAVAÇÃO NO FICHEIRO CSV (ORIGINAL) ---
                    csvMutex.WaitOne(); // Adquire o mutex antes de escrever no CSV
                    try
                    {
                        using var writer = new StreamWriter(caminhoCSV, append: true, encoding: Encoding.UTF8);
                        foreach (var linha in linhas)
                        {
                            // linha ex: "WAVY_001;temperatura;913;agregadorX;2025-05-22 13:45:01"
                            var cols = linha.Split(';');

                            if (cols.Length != 5) // Ainda verificamos para 5 colunas para o CSV
                            {
                                Console.WriteLine($"[SERVIDOR] CSV: Linha ignorada devido a formato inválido ou incompleto: {linha}");
                                continue;
                            }

                            string wavyId = cols[0];
                            string tipo = cols[1];
                            string valor = cols[2];
                            string aggregatorId = cols[3]; // Usamos o aggregatorId para o CSV
                            // string dataAgregador = cols[4]; // Esta seria a data do agregador, mas optamos por usar DateTime.Now

                            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                            // Adicionar o aggregatorId à linha que será escrita no CSV
                            writer.WriteLine($"{timestamp};{wavyId};{tipo};{valor};{aggregatorId}");
                        }
                        Console.WriteLine($"[SERVIDOR] CSV: DADOS GRAVADOS NO FICHEIRO\n");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("[SERVIDOR] CSV: ERRO ao gravar no ficheiro CSV: " + ex.Message);
                    }
                    finally
                    {
                        csvMutex.ReleaseMutex(); // Libera o mutex
                    }
                    // --- FIM DA GRAVAÇÃO NO FICHEIRO CSV ---
                }

                // 4) ACK para o agregador
                var ack = Encoding.UTF8.GetBytes("OK");
                stream.Write(ack, 0, ack.Length);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[SERVIDOR] Erro geral no ProcessarAgregador: " + ex.Message);
            }
            finally
            {
                client.Close();
            }
        }

        static void MenuAnalise()
        {
            while (true)
            {
                Console.WriteLine("== Menu Análise ==");
                Console.WriteLine("[1] Pedir análise aos dados atuais");
                Console.WriteLine("[ENTER] Continuar a escutar agregadores");
                var key = Console.ReadKey(true);

                if (key.Key == ConsoleKey.D1 || key.Key == ConsoleKey.NumPad1)
                {
                    try
                    {
                        PedirAnalise();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("[SERVIDOR] Erro ao pedir análise: " + ex.Message);
                    }
                }
                else
                {
                    Thread.Sleep(200);
                }
            }
        }

        static void PedirAnalise()
        {
            // Para esta fase de transição, vamos continuar a ler do CSV para o serviço gRPC
            // Assim, garantimos que o serviço de análise continua a funcionar com os dados de backup.
            string csvConteudo = "WAVY_ID;TIPO;VALOR;DATA\n";
            try
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string query = "SELECT WavyId, Tipo, Valor, DataMedicao FROM DadosRecebidosServidor";
                    using (var command = new SqlCommand(query, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string wavyId = reader["WavyId"].ToString();
                            string tipoDb = reader["Tipo"].ToString();
                            string valor = reader["Valor"].ToString();
                            string data = Convert.ToDateTime(reader["DataMedicao"]).ToString("yyyy-MM-dd HH:mm:ss");
                            csvConteudo += $"{wavyId};{tipoDb};{valor};{data}\n";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[SERVIDOR] Erro ao ler dados da base de dados para análise: " + ex.Message);
                return; // Impede a chamada gRPC se a leitura do SQL falhar
            }

            Console.WriteLine("[SERVIDOR] Dados lidos do CSV para análise:\n" + csvConteudo.Substring(0, Math.Min(csvConteudo.Length, 200)) + "..."); // Mostrar apenas um pedaço

            // Valida tipo
            string[] tipos = { "temperatura", "pressao", "ph" };
            string tipo;
            do
            {
                Console.Write("Que TIPO de dado quer analisar (temperatura/pressao/ph)? ");
                tipo = Console.ReadLine()!.Trim().ToLower();
            } while (!tipos.Contains(tipo));

            // Valida operação
            string[] ops = { "media", "contagem", "minimo", "maximo" };
            string op;
            do
            {
                Console.Write("Que operação? (media/contagem/minimo/maximo): ");
                op = Console.ReadLine()!.Trim().ToLower();
            } while (!ops.Contains(op));

            // Chama gRPC de análise
            using var channel = GrpcChannel.ForAddress("http://localhost:5087");
            var client = new Analise.AnaliseClient(channel);
            var resp = client.Analisar(new PedidoAnalise
            {
                Tipo = tipo,
                Operacao = op,
                ConteudoCSV = csvConteudo // Envia o CSV lido do ficheiro
            });

            Console.WriteLine("Resultado da análise: " + resp.Resultado + "\n");
        }
    }
}