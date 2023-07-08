namespace DotNetDocker_DockerService.Models
{
    public class CodeExecutedResult
    {
        public string ContainerId { get; set; } = default!;
        public string Stderr { get; set; } = "";
        public string Stdout { get; set; } = "";
    }
}