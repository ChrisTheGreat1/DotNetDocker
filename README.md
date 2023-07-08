# DotNetSandbox

This is an experimental project that uses Blazor Server to provide a collaborative sandbox environment to run C# code in. 

Users can create unique groups, and upon navigating to the group page they can enter C# code snippets into the text editor. SignalR groups are utilized such that all users viewing the group page can see updates to the text editor in real time. 

A Docker container containing the .NET SDK is created for each group and used to build the C# program written in the text editor. The container has all networking capabilities disabled (network driver = none) to prevent malicious commands. 

Upon pressing the submit/run button in the group's webpage, the text editor contents are written to the group's Docker container volume and the program is executed in the Docker container. After the program execution finishes, the result is written back to the group webpage. All Docker operations are handled by the Docker Service console application. All communications between the Blazor Server app and the Docker Service console app are facilitated using RabbitMQ. 

*Key technologies utilized:*
- Blazor Server
- Docker & Docker.DotNet nuget package
- SignalR
- RabbitMQ
- SemaphoreSlim & ConcurrentDictionary
 
