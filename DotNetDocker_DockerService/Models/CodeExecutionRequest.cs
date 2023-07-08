namespace DotNetDocker_DockerService.Models
{
    public class CodeExecutionRequest
    {
        public string Code { get; set; } = "";
        public string ContainerId { get; set; } = "";
    }
}