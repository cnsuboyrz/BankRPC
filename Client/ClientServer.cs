using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;
using System.Threading.Channels;
namespace BankRpc
{
    public class AllClients
    {
        List<Client> clients = new List<Client>();
        public AllClients()
        {
            clients.Add(new Client("jane", "doe", 800));
            clients.Add(new Client("joe", "doe", 100));
            clients.Add(new Client("john", "roe", 200));
            clients.Add(new Client("johnny", "smith", 200));
        }

        public Client FindClient(string isim)
        {
            foreach (Client client in clients)
            {
                if (client.Name.Equals(isim))
                {
                    return client;
                }
            }
            return null;
        }

        public class Client : IDisposable
        {
            private const string queue_name = "client_request";

            //clientta bulunan özellikler
            public int amount { get; set; }
            public string Name { get; set; }
            public string Surnanme { get; set; }


            private readonly IConnection connection1;
            private readonly IModel channel1;
            private readonly string replyQueueName;
            private readonly ConcurrentDictionary<string, TaskCompletionSource<string>> callbackMapper = new(); 

            public Client(string _name, string surname, int _amount)
            {
                amount = _amount;
                Name = _name;
                Surnanme = surname;
                var factory = new ConnectionFactory { HostName = "localhost" };
                connection1 = factory.CreateConnection();
                channel1 = connection1.CreateModel();
                replyQueueName = channel1.QueueDeclare().QueueName;

                var consumer1 = new EventingBasicConsumer(channel1);
                consumer1.Received += (model, ea) => // client 
                {
                    if (!callbackMapper.TryRemove(ea.BasicProperties.CorrelationId, out var tcs))   
                        return;
                    var body = ea.Body.ToArray();
                    var response = Encoding.UTF8.GetString(body);
                    tcs.TrySetResult(response);

                };

                channel1.BasicConsume(consumer: consumer1,
                                     queue: replyQueueName,
                                     autoAck: true);
            }

            public Task<string> CallAsync(string message, Client client, CancellationToken cancellationToken = default)
            {
                IBasicProperties props = channel1.CreateBasicProperties(); //kanalın özelliklerini bulduk

                var correlationId = Guid.NewGuid().ToString(); //kanalın corId sini atadık (globally unique id)
                props.CorrelationId = correlationId;

                props.ReplyTo = replyQueueName;  //kanalın cevaplama queuesunu oluşturduk 
               // kullanıcının girdiği sayıyı byte a çevirdik
               
                String jsonified = JsonConvert.SerializeObject(client);
                String lastmessage = jsonified +"-"+ message;
                var sendingresult = Encoding.UTF8.GetBytes(lastmessage);

                //var clientBuffer = Encoding.UTF8.GetBytes(jsonified);

                //var messageBytes = Encoding.UTF8.GetBytes(message);

                var tcs = new TaskCompletionSource<string>();
                callbackMapper.TryAdd(correlationId, tcs);

                channel1.BasicPublish(exchange: string.Empty, //exchange in ismini boş bıraktık
                                     routingKey: queue_name, // exchange sayesinde alınan sayı hangi queue yazılacak
                                     basicProperties: props,
                                     body: sendingresult); // rpc-queueya mesaj(alınan sayı) eklendi

                cancellationToken.Register(() => callbackMapper.TryRemove(correlationId, out _));
                return tcs.Task;

            }

            public void Dispose()
            {
                connection1.Close();
            }

            public class ClientSide
            {
                public static async Task Main(string[] args)
                {
                    Console.WriteLine("RPC Client");
                    Console.WriteLine("Çekilecek para:");
                    string n = args.Length > 0 ? args[0] : "20";

                    await InvokeAsync(n);

                    Console.WriteLine(" Press [enter] to exit.");
                    Console.ReadLine();


                }

                private static async Task InvokeAsync(string n)
                {
                    AllClients islemler = new AllClients();
                    var client2 = islemler.FindClient("jane");
                   
                    Console.WriteLine(" [x] Request for "+client2.Name);
                    var response = await client2.CallAsync(n, client2);
                    Console.WriteLine(" [.] Got "+ response);
                }
            }
        }
    }
}