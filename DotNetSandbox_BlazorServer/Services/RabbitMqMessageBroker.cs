using DotNetSandbox_BlazorServer.Models;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace DotNetSandbox_BlazorServer.Services
{
    public class RabbitMqMessageBroker : IDisposable, IMessageBrokerService
    {
        private const string CONTAINER_CREATED_RESPONSE_QUEUE = "containerCreatedResponseQueue";
        private const string CREATE_CONTAINER_REQUEST_QUEUE = "createContainerRequestQueue";
        private const string DELETE_CONTAINER_REQUEST_QUEUE = "deleteContainerRequestQueue";
        private const string EXCHANGE_NAME = "docker-service";
        private const string EXECUTE_CODE_REQUEST_QUEUE = "executeCodeRequestQueue";
        private const string EXECUTED_CODE_RESPONSE_QUEUE = "executedCodeResponseQueue";
        private const string HELLO_CONSUME_QUEUE = "helloConsumeQueue";
        private const string HELLO_PUBLISH_QUEUE = "helloPublishQueue";
        private readonly IConnection _connection;

        private readonly EventingBasicConsumer _helloChannelConsumer;
        private readonly IModel _helloChannelConsumerChannel;
        private readonly IModel _helloChannelPublisherChannel;
        public RabbitMqMessageBroker()
        {
            var factory = new ConnectionFactory
            {
                HostName = "localhost",
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(10),
                //DispatchConsumersAsync = true
            };

            _connection = factory.CreateConnection();

            #region Hello Queues

            _helloChannelPublisherChannel = _connection.CreateModel();
            _helloChannelConsumerChannel = _connection.CreateModel();

            _helloChannelPublisherChannel.ExchangeDeclare(exchange: EXCHANGE_NAME, type: ExchangeType.Direct, durable: true);

            _helloChannelPublisherChannel.QueueDeclare(queue: HELLO_PUBLISH_QUEUE,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);
            _helloChannelConsumerChannel.QueueDeclare(queue: HELLO_CONSUME_QUEUE,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            _helloChannelConsumerChannel.QueueBind(queue: HELLO_CONSUME_QUEUE,
                exchange: EXCHANGE_NAME,
                routingKey: HELLO_CONSUME_QUEUE);

            _helloChannelConsumer = new EventingBasicConsumer(_helloChannelConsumerChannel);
            _helloChannelConsumer.Received += (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                Console.WriteLine($" [x] Received return message in blazor server: {message}");
            };
            _helloChannelConsumerChannel.BasicConsume(queue: HELLO_CONSUME_QUEUE,
                                 autoAck: true,
                                 consumer: _helloChannelConsumer);

            #endregion Hello Queues

            #region Create Container Queues

            using var createContainerResponseChannel = _connection.CreateModel();
            using var createContainerRequestChannel = _connection.CreateModel();

            createContainerRequestChannel.QueueDeclare(queue: CREATE_CONTAINER_REQUEST_QUEUE,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);
            createContainerResponseChannel.QueueDeclare(queue: CONTAINER_CREATED_RESPONSE_QUEUE,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            #endregion Create Container Queues

            #region Code Execution Queues

            using var executeCodeResponseChannel = _connection.CreateModel();
            using var executeCodeRequestChannel = _connection.CreateModel();

            executeCodeRequestChannel.QueueDeclare(queue: EXECUTE_CODE_REQUEST_QUEUE,
                     durable: true,
                     exclusive: false,
                     autoDelete: false,
                     arguments: null);
            executeCodeResponseChannel.QueueDeclare(queue: EXECUTED_CODE_RESPONSE_QUEUE,
                     durable: true,
                     exclusive: false,
                     autoDelete: false,
                     arguments: null);

            #endregion Code Execution Queues

            #region Delete Container Queues

            using var deleteContainerRequestChannel = _connection.CreateModel();

            deleteContainerRequestChannel.QueueDeclare(queue: DELETE_CONTAINER_REQUEST_QUEUE,
                 durable: true,
                 exclusive: false,
                 autoDelete: false,
                 arguments: null);

            #endregion Delete Container Queues
        }

        // Terrible awful use of message brokering but this is the only way I could get it to be compatible with the SignalR hub.
        public async Task<string> CreateContainerAsync()
        {
            using var createContainerResponseChannel = _connection.CreateModel();
            using var createContainerRequestChannel = _connection.CreateModel();

            createContainerResponseChannel.QueueBind(queue: CONTAINER_CREATED_RESPONSE_QUEUE,
                     exchange: EXCHANGE_NAME,
                     routingKey: CONTAINER_CREATED_RESPONSE_QUEUE);

            var containerIdResponseTcs = new TaskCompletionSource<string>();

            var consumer = new EventingBasicConsumer(createContainerResponseChannel);

            consumer.Received += (model, ea) =>
            {
                var responseBody = ea.Body.ToArray();
                var responseMessage = Encoding.UTF8.GetString(responseBody);
                containerIdResponseTcs.SetResult(responseMessage);
            };

            createContainerRequestChannel.BasicPublish(exchange: EXCHANGE_NAME,
                                 routingKey: CREATE_CONTAINER_REQUEST_QUEUE,
                                 basicProperties: null,
                                 body: null);

            createContainerResponseChannel.BasicConsume(queue: CONTAINER_CREATED_RESPONSE_QUEUE,
                     autoAck: true,
                     consumer: consumer);

            var containerId = await containerIdResponseTcs.Task;

            return containerId;
        }

        public void DeleteContainer(string containerId)
        {
            using var deleteContainerRequestChannel = _connection.CreateModel();

            var body = Encoding.UTF8.GetBytes(containerId);

            deleteContainerRequestChannel.BasicPublish(exchange: EXCHANGE_NAME,
                     routingKey: DELETE_CONTAINER_REQUEST_QUEUE,
                     basicProperties: null,
                     body: body);
        }

        public void Dispose()
        {
            _connection.Dispose();
            _helloChannelConsumerChannel.Dispose();
            _helloChannelPublisherChannel.Dispose();
        }

        public async Task<(string stdout, string stderr)> ExecuteGroupCodeAsync(string containerId, string code)
        {
            using var executeCodeResponseChannel = _connection.CreateModel();
            using var executeCodeRequestChannel = _connection.CreateModel();

            executeCodeResponseChannel.QueueBind(queue: EXECUTED_CODE_RESPONSE_QUEUE,
                     exchange: EXCHANGE_NAME,
                     routingKey: EXECUTED_CODE_RESPONSE_QUEUE);

            var executedCodeResponseTcs = new TaskCompletionSource<(string Stdout, string Stderr)>();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            cts.Token.Register(() => executedCodeResponseTcs.TrySetCanceled());

            var consumer = new EventingBasicConsumer(executeCodeResponseChannel);

            consumer.Received += (model, ea) =>
            {
                var responseByteArray = ea.Body.ToArray();
                var responseJsonString = Encoding.UTF8.GetString(responseByteArray);
                var response = JsonSerializer.Deserialize<CodeExecutedResult>(responseJsonString);

                // This causes the program to deadlock if one groups code execution finishes before another groups. Don't do this.
                if (response.ContainerId == containerId)
                {
                    executeCodeResponseChannel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                    executedCodeResponseTcs.SetResult((response.Stdout, response.Stderr));
                }
            };

            var requestPayloadData = new CodeExecutionRequest { ContainerId = containerId, Code = code };
            var requestPayloadJsonData = JsonSerializer.Serialize(requestPayloadData);
            var requestPayloadBody = Encoding.UTF8.GetBytes(requestPayloadJsonData);

            executeCodeRequestChannel.BasicPublish(exchange: EXCHANGE_NAME,
                                     routingKey: EXECUTE_CODE_REQUEST_QUEUE,
                                     basicProperties: null,
                                     body: requestPayloadBody);

            executeCodeResponseChannel.BasicConsume(queue: EXECUTED_CODE_RESPONSE_QUEUE,
                         autoAck: false,
                         consumer: consumer);

            try
            {
                var response = await executedCodeResponseTcs.Task;
                return response;
            }
            catch (TaskCanceledException)
            {
                return ("An error was encountered.", "");
            }
        }

        public void TestHelloMessaging()
        {
            var body = Encoding.UTF8.GetBytes("Testing hello channel publishing from blazor server");

            _helloChannelPublisherChannel.BasicPublish(exchange: EXCHANGE_NAME,
                                 routingKey: HELLO_PUBLISH_QUEUE,
                                 basicProperties: null,
                                 body: body);
        }
    }
}