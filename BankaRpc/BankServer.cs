using System.Text;
using BankRpc;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using static BankRpc.AllClients;

var factory = new ConnectionFactory { HostName = "localhost" };
using var connection = factory.CreateConnection();
using var channel = connection.CreateModel();

channel.QueueDeclare(queue: "client_request",
                     durable: false,
                     exclusive: false,
                     autoDelete: false,
                     arguments: null);

channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

var consumer = new EventingBasicConsumer(channel);

channel.BasicConsume(queue: "client_request",
                     autoAck: false,
                     consumer: consumer);

Console.WriteLine(" [x] Awaiting RPC requests");

consumer.Received += (model, ea) => // burası server ın consumer kısmı burda mesajlar rpc_queue den alınıyor ve reply a aktarılıyor
{
    string response = string.Empty;

    var body = ea.Body.ToArray();
    var props = ea.BasicProperties;
    var replyProps = channel.CreateBasicProperties();
    replyProps.CorrelationId = props.CorrelationId;

    try
    {
        var message = Encoding.UTF8.GetString(body);
        string[] splittted = message.Split("-");
         Client client= JsonConvert.DeserializeObject<Client>(splittted[0]);
        int n = int.Parse(splittted[1]);
        Console.WriteLine($" [.] ParaCekme({n})");
        response = ParaCekme(client, n);
        //int n = int.Parse(message);
        //Console.WriteLine($" [.] ParaCekme({message})");
        //response = ParaCekme(n,new Client("sa","as",100));
    }
    catch (Exception e)
    {
        Console.WriteLine($" [.] {e.Message}");
        response = string.Empty;
    }
    finally
    {
        var responseBytes = Encoding.UTF8.GetBytes(response);

        channel.BasicPublish(exchange: string.Empty,
                             routingKey: props.ReplyTo, //reply_queue ya aktarıldı
                             basicProperties: replyProps,
                             body: responseBytes);

        channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
    }
};

Console.WriteLine(" Press [enter] to exit.");
Console.ReadLine();


    static string ParaCekme(Client client,int n)
    {
    if ( client.amount- n > 0)
    {
        client.amount -= n;
        return "Kalan bakiye: "+client.amount;
    }
    return "İşleminiz için bankada yeterli bakiyeniz yok.";
    }

