namespace DotNetSandbox_BlazorServer.Models
{
    public class CodeExecutionRequest
    {
        public string Code { get; set; } = "";
        public string ContainerId { get; set; } = "";
    }
}