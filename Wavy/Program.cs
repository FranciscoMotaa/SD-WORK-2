using System;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using RabbitMQ.Client;

namespace Wavy
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== WAVY - Publicador RabbitMQ ===");

            // 1) Pergunta quantas WAVYs simular
            Console.Write("Quantas WAVYs você deseja criar? ");
            int numeroWavys = int.Parse(Console.ReadLine() ?? "1");

            var wavyThreads = new List<Thread>();

            // 2) Criação das WAVYs com ID automático
            for (int i = 1; i <= numeroWavys; i++)
            {
                // ID sequencial 001, 002, …
                string wavyId = $"WAVY_{i:000}";
                Console.WriteLine($"[WAVY] Iniciando instância {wavyId}");

                // Cria e armazena a thread de publicação
                var thread = new Thread(() => EnviarPeriodicamente(wavyId))
                {
                    IsBackground = true
                };
                wavyThreads.Add(thread);
            }

            // 3) Arranca todas as threads (WAVYs)
            foreach (var t in wavyThreads)
                t.Start();

            Console.WriteLine("Pressione ENTER para encerrar todas as WAVYs.");
            Console.ReadLine();
        }

        static void EnviarPeriodicamente(string wavyId)
        {
            // Tipos e formatos suportados
            string[] tipos = { "temperatura", "pressao", "ph" };
            string[] formatos = { "txt", "csv", "xml", "json" };
            int[] intervalos = { 5000, 10000, 20000 }; // 5s, 10s, 20s

            var rand = new Random();
            var factory = new ConnectionFactory() { HostName = "localhost" };

            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();

            channel.ExchangeDeclare(exchange: "sensores", type: "topic");

            try
            {
                while (true)
                {
                    // Escolhe tipo, formato e gera valor realista
                    string tipo = tipos[rand.Next(tipos.Length)];
                    string formato = formatos[rand.Next(formatos.Length)];
                    double valor;

                    if (tipo == "ph")
                    {
                        valor = rand.Next(30, 91) / 10.0; // pH entre 3.0 e 9.0
                    }
                    else if (tipo == "temperatura")
                    {
                        // Temperatura entre 10.0 e 35.0 graus Celsius
                        valor = Math.Round(rand.NextDouble() * (35.0 - 10.0) + 10.0, 1);
                    }
                    else if (tipo == "pressao")
                    {
                        // Pressão entre 980.0 e 1050.0 hPa
                        valor = Math.Round(rand.NextDouble() * (1050.0 - 980.0) + 980.0, 1);
                    }
                    else
                    {
                        valor = rand.Next(100, 1001); // fallback
                    }

                    string data = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                    // Monta a mensagem no formato escolhido
                    string mensagem = formato switch
                    {
                        "txt" => $"{wavyId}|{tipo}|{valor}|{data}",
                        "csv" => $"{wavyId},{tipo},{valor},{data}",
                        "xml" => $"<medicao><wavy>{wavyId}</wavy><tipo>{tipo}</tipo><valor>{valor}</valor><data>{data}</data></medicao>",
                        "json" => $"{{\"wavy\":\"{wavyId}\",\"tipo\":\"{tipo}\",\"valor\":{valor},\"data\":\"{data}\"}}",
                        _ => $"{wavyId}|{tipo}|{valor}|{data}"
                    };

                    // Publica em "sensores" com routingKey tipo.formato
                    string routingKey = $"{tipo}.{formato}";
                    var body = Encoding.UTF8.GetBytes(mensagem);
                    channel.BasicPublish(
                        exchange: "sensores",
                        routingKey: routingKey,
                        basicProperties: null,
                        body: body
                    );

                    Console.WriteLine($"[{wavyId}] Publicado: '{mensagem}' em '{routingKey}'");

                    // Aguarda intervalo aleatório antes da próxima
                    int espera = intervalos[rand.Next(intervalos.Length)];
                    Thread.Sleep(espera);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{wavyId}] [ERRO] Ao enviar dados: {ex.Message}");
            }
        }
    }
}
