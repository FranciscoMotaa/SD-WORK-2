using System;
using System.Text;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Grpc.Net.Client;
using PreprocessamentoService; // Namespace gerado pelo proto

namespace Agregador
{
    class Program
    {
        // gRPC client (compartilhado por todos os agregadores)
        static readonly GrpcChannel _grpcChannel =
            GrpcChannel.ForAddress("http://localhost:5138");
        static readonly PreProcessamento.PreProcessamentoClient _grpcClient =
            new PreProcessamento.PreProcessamentoClient(_grpcChannel);

        // Mutex único para proteger todos os ficheiros
        static readonly Mutex _csvMutex = new Mutex();

        static void Main(string[] args)
        {
            Console.Write("Quantos Agregadores quer criar? ");
            int n = int.Parse(Console.ReadLine() ?? "1");

            var usados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 1; i <= n; i++)
            {
                // gera ID sequencial com 4 dígitos: 5001, 5002, …
                // ou substitui 5000 pelo teu valor-base
                int basePort = 001;
                string id = (basePort + i - 1).ToString();
                Console.WriteLine($"[AGG] Iniciando agregador ID = {id}");

                // escolhe tipo a subscrever (podes manter prompt ou padronizar)
                Console.WriteLine("== Escolha o tipo de dado ==");
                Console.WriteLine("[1] temperatura  [2] pressao  [3] ph  [4] todos");
                Console.Write("Opção: ");
                string opcao = Console.ReadLine()!.Trim();
                string routingKey = opcao switch
                {
                    "1" => "temperatura.*",
                    "2" => "pressao.*",
                    "3" => "ph.*",
                    _ => "*.*"
                };

                string filePath = $@"D:\SD\SD_NOW\agregador_{id}_dados.csv";

                var t = new Thread(() => RodarAgregadorInstance(id, routingKey, filePath))
                {
                    IsBackground = true
                };
                t.Start();
            }

            Console.WriteLine("\nPressione ENTER para encerrar todos os agregadores.");
            Console.ReadLine();
        }

        static void RodarAgregadorInstance(string id, string routingKey, string filePath)
        {
            Console.WriteLine($"[AGG {id}] Subscrição '{routingKey}', CSV em '{filePath}'");

            // 1) inicializa o CSV (cabeçalho único)
            _csvMutex.WaitOne();
            try
            {
                File.WriteAllText(filePath, "WAVY_ID;TIPO;VALOR;DATA;AgregadorID\n");
            }
            finally
            {
                _csvMutex.ReleaseMutex();
            }

            // 2) configurar RabbitMQ consumer
            var factory = new ConnectionFactory { HostName = "localhost" };
            using var conn = factory.CreateConnection();
            using var channel = conn.CreateModel();
            channel.ExchangeDeclare("sensores", "topic");
            var queueName = channel.QueueDeclare().QueueName;
            channel.QueueBind(queueName, "sensores", routingKey);

            var consumer = new EventingBasicConsumer(channel);
            consumer.Received += (_, ea) =>
            {
                var mensagem = Encoding.UTF8.GetString(ea.Body.ToArray()).Trim();
                if (mensagem.Length == 0)
                    return;
                var parts = ea.RoutingKey.Split('.');
                var tipo = parts[0];
                var formato = parts.Length > 1 ? parts[1] : "txt";

                // gRPC: converte para CSV
                var resp = _grpcClient.Processar(new DadoOriginal
                {
                    Tipo = tipo,
                    Formato = formato,
                    Conteudo = mensagem
                }).Conteudo.Trim();
                // ex: "WAVY_001;temperatura;913;2025-05-22"


                // grava no ficheiro local
                _csvMutex.WaitOne();
                try
                {
                    File.AppendAllText(filePath, $"{resp};{id}\n");
                }
                finally
                {
                    _csvMutex.ReleaseMutex();
                }

                Console.WriteLine($"[AGREGADOR {id}] + {resp}");
            };
            channel.BasicConsume(queueName, autoAck: true, consumer);

            new Thread(() =>
            {
                while (true)
                {
                    Thread.Sleep(TimeSpan.FromSeconds(30));

                    string payload;
                    // 1) lê sem limpar ainda
                    _csvMutex.WaitOne();
                    try
                    {
                        payload = File.ReadAllText(filePath);
                    }
                    finally
                    {
                        _csvMutex.ReleaseMutex();
                    }

                    // 2) filtra linhas de dados, removendo qualquer "WAVY_ID;TIPO;VALOR;DATA"
                    var linhas = payload
                        .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                        .Where(l => !l.StartsWith("WAVY_ID;"))
                        .ToArray();

                    // se não houver linhas de dado, não envia
                    if (linhas.Length == 0)
                        continue;

                    // prepara o texto a enviar
                    var toSend = string.Join('\n', linhas);

                    try
                    {
                        // 3) envia ao servidor
                        using var tcp = new System.Net.Sockets.TcpClient("127.0.0.1", 7000);
                        using var stream = tcp.GetStream();
                        var data = Encoding.UTF8.GetBytes(toSend);
                        stream.Write(data, 0, data.Length);

                        // 4) lê o ACK
                        var buf = new byte[256];
                        int len = stream.Read(buf, 0, buf.Length);
                        var ack = Encoding.UTF8.GetString(buf, 0, len).Trim();
                        Console.WriteLine($"[AGG {id}] Servidor: {ack}");

                        // 5) só limpa se for OK
                        if (ack.Equals("OK", StringComparison.OrdinalIgnoreCase))
                        {
                            _csvMutex.WaitOne();
                            try
                            {
                                File.WriteAllText(filePath, "WAVY_ID;TIPO;VALOR;DATA\n");
                            }
                            finally
                            {
                                _csvMutex.ReleaseMutex();
                            }
                        }
                        else
                        {
                            Console.WriteLine($"[AGG {id}] ACK inválido, mantendo dados para re-tentar.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[AGG {id}] Erro TCP→Servidor: {ex.Message}");
                    }
                }
            })
            { IsBackground = true }
            .Start();

            // mantém viva a thread principal
            Console.WriteLine($"[AGREGADOR {id}] Em escuta...");
            while (true) Thread.Sleep(1000);
        }
    }
}