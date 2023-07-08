using DotNetDocker_DockerService.Models;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace DotNetDocker_DockerService
{
    internal class Program
    {
        private const string CONTAINER_CREATED_RESPONSE_QUEUE = "containerCreatedResponseQueue";
        private const string CREATE_CONTAINER_REQUEST_QUEUE = "createContainerRequestQueue";
        private const string DELETE_CONTAINER_REQUEST_QUEUE = "deleteContainerRequestQueue";
        private const string EXCHANGE_NAME = "docker-service";
        private const string EXECUTE_CODE_REQUEST_QUEUE = "executeCodeRequestQueue";
        private const string EXECUTED_CODE_RESPONSE_QUEUE = "executedCodeResponseQueue";
        private const string HELLO_CONSUME_QUEUE = "helloConsumeQueue";
        private const string HELLO_PUBLISH_QUEUE = "helloPublishQueue";
        // Assumes RabbitMQ is installed and running on localhost on the standard port (5672) and all queues were declared in Blazor server app.
        public static void Main(string[] args)
        {
            var factory = new ConnectionFactory
            {
                HostName = "localhost",
                //DispatchConsumersAsync = true
            };

            using var connection = factory.CreateConnection();

            using var helloConsumedChannel = connection.CreateModel();
            using var helloPublishedChannel = connection.CreateModel();
            using var createContainerRequestChannel = connection.CreateModel();
            using var containerCreatedResponseChannel = connection.CreateModel();
            using var executeCodeResponseChannel = connection.CreateModel();
            using var executeCodeRequestChannel = connection.CreateModel();
            using var deleteContainerRequestChannel = connection.CreateModel();

            Console.WriteLine(" [*] Waiting for messages.");

            #region Hello Queues

            helloPublishedChannel.QueueBind(queue: HELLO_PUBLISH_QUEUE,
                     exchange: EXCHANGE_NAME,
                     routingKey: HELLO_PUBLISH_QUEUE);

            var helloPublishedChannelConsumer = new EventingBasicConsumer(helloPublishedChannel);

            helloPublishedChannelConsumer.Received += (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                Console.WriteLine($" [x] Received message in console app: {message}");

                var bodyResponse = Encoding.UTF8.GetBytes("Returned message from console app");

                helloConsumedChannel.BasicPublish(exchange: EXCHANGE_NAME,
                                     routingKey: HELLO_CONSUME_QUEUE,
                                     basicProperties: null,
                                     body: bodyResponse);
            };

            helloPublishedChannel.BasicConsume(queue: HELLO_PUBLISH_QUEUE,
                                 autoAck: true,
                                 consumer: helloPublishedChannelConsumer);

            #endregion Hello Queues

            #region Create Container Queues

            createContainerRequestChannel.QueueBind(queue: CREATE_CONTAINER_REQUEST_QUEUE,
                 exchange: EXCHANGE_NAME,
                 routingKey: CREATE_CONTAINER_REQUEST_QUEUE);

            var createContainerRequestConsumer = new EventingBasicConsumer(createContainerRequestChannel);

            createContainerRequestConsumer.Received += async (model, ea) =>
            {
                var containerId = await DockerApi.CreateNewContainerAsync();
                await DockerApi.StartContainerAsync(containerId);

                var responseBody = Encoding.UTF8.GetBytes(containerId);

                Console.WriteLine("Publishing new container ID");

                containerCreatedResponseChannel.BasicPublish(exchange: EXCHANGE_NAME,
                                 routingKey: CONTAINER_CREATED_RESPONSE_QUEUE,
                                 basicProperties: null,
                                 body: responseBody);
            };

            createContainerRequestChannel.BasicConsume(queue: CREATE_CONTAINER_REQUEST_QUEUE,
                     autoAck: true,
                     consumer: createContainerRequestConsumer);

            #endregion Create Container Queues

            #region Code Execution Queues

            executeCodeRequestChannel.QueueBind(queue: EXECUTE_CODE_REQUEST_QUEUE,
                exchange: EXCHANGE_NAME,
                routingKey: EXECUTE_CODE_REQUEST_QUEUE);

            var executeCodeRequestConsumer = new EventingBasicConsumer(executeCodeRequestChannel);

            executeCodeRequestConsumer.Received += async (model, ea) =>
            {
                var requestByteArray = ea.Body.ToArray();
                var requestJsonString = Encoding.UTF8.GetString(requestByteArray);
                var request = JsonSerializer.Deserialize<CodeExecutionRequest>(requestJsonString);

                await DockerApi.WriteFileToVolumeAsync(request.ContainerId, request.Code);
                var response = await DockerApi.ExecuteContainerCodeAsync(request.ContainerId);

                var responsePayloadData = new CodeExecutedResult { Stdout = response.stdout, Stderr = response.stderr, ContainerId = request.ContainerId };
                var responsePayloadJsonData = JsonSerializer.Serialize(responsePayloadData);
                var responsePayloadBody = Encoding.UTF8.GetBytes(responsePayloadJsonData);

                executeCodeResponseChannel.BasicPublish(exchange: EXCHANGE_NAME,
                                 routingKey: EXECUTED_CODE_RESPONSE_QUEUE,
                                 basicProperties: null,
                                 body: responsePayloadBody);
            };

            executeCodeRequestChannel.BasicConsume(queue: EXECUTE_CODE_REQUEST_QUEUE,
                     autoAck: true,
                     consumer: executeCodeRequestConsumer);

            #endregion Code Execution Queues

            #region Delete Container Queues

            deleteContainerRequestChannel.QueueBind(queue: DELETE_CONTAINER_REQUEST_QUEUE,
                exchange: EXCHANGE_NAME,
                routingKey: DELETE_CONTAINER_REQUEST_QUEUE);

            var deleteContainerRequestConsumer = new EventingBasicConsumer(deleteContainerRequestChannel);

            deleteContainerRequestConsumer.Received += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var containerId = Encoding.UTF8.GetString(body);
                await DockerApi.DeleteContainerAsync(containerId);
            };

            deleteContainerRequestChannel.BasicConsume(queue: DELETE_CONTAINER_REQUEST_QUEUE,
                     autoAck: true,
                     consumer: deleteContainerRequestConsumer);

            #endregion Delete Container Queues

            Console.WriteLine(" Press [enter] to exit.");
            Console.ReadLine();
        }
    }
}