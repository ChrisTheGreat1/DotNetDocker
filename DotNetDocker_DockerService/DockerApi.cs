using Docker.DotNet;
using Docker.DotNet.Models;

namespace DotNetDocker_DockerService
{
    public static class DockerApi
    {
        private const string CONTAINER_VOLUME_PATH = "/app";

        private const string IMAGE_NAME = "dotnetcontainer";

        // Single static client can still handle concurrent requests/responses across multiple containers.
        private static DockerClient _client = new DockerClientConfiguration().CreateClient();
        public static async Task<string> CreateNewContainerAsync()
        {
            var containerCreationResponse = await _client.Containers.CreateContainerAsync(new CreateContainerParameters
            {
                Image = IMAGE_NAME,
                NetworkDisabled = true,
                Volumes = new Dictionary<string, EmptyStruct>
                {
                    { CONTAINER_VOLUME_PATH, default(EmptyStruct) }
                }
            });

            return containerCreationResponse.ID;
        }

        public static async Task DeleteContainerAsync(string containerID)
        {
            await _client.Containers.KillContainerAsync(containerID, new ContainerKillParameters());
            await _client.Containers.PruneContainersAsync();
        }

        public static async Task<(string stdout, string stderr)> ExecuteContainerCodeAsync(string containerID)
        {
            var execID = await CreateApiExecIdAsync(containerID);
            (string stdout, string stderr) outputVar;

            using (var stream = await _client.Exec.StartAndAttachContainerExecAsync(execID, false))
            {
                outputVar = await stream.ReadOutputToEndAsync(CancellationToken.None);
            }

            return outputVar;
        }

        public static async Task StartContainerAsync(string containerID)
        {
            await _client.Containers.StartContainerAsync(containerID, null);
        }

        public static async Task WriteFileToVolumeAsync(string containerID, string programFileContents)
        {
            var container = await _client.Containers.InspectContainerAsync(containerID);

            var volume = container.Mounts.Single().Name;

            var programPath = $"\\\\wsl$\\docker-desktop-data\\data\\docker\\volumes\\{volume}\\_data\\Program.cs";

            await File.WriteAllTextAsync(programPath, programFileContents);
        }
        private static async Task<string> CreateApiExecIdAsync(string containerID)
        {
            var cmdList = new[] { "dotnet", "run" };

            var execCreateResponse = await _client.Exec.ExecCreateContainerAsync(containerID,
                new ContainerExecCreateParameters()
                {
                    AttachStderr = true,
                    AttachStdout = true,
                    Cmd = cmdList,
                    Tty = false
                });

            var execID = execCreateResponse.ID;

            return execID;
        }
    }
}