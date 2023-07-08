namespace DotNetSandbox_BlazorServer.Services
{
    public interface IMessageBrokerService
    {
        Task<string> CreateContainerAsync();

        void DeleteContainer(string containerId);

        void Dispose();

        Task<(string stdout, string stderr)> ExecuteGroupCodeAsync(string containerId, string code);

        void TestHelloMessaging();
    }
}