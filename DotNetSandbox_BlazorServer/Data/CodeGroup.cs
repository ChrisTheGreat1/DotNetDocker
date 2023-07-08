namespace DotNetSandbox_BlazorServer.Data
{
    // Complete group object with DateTime properties could be transmitted via SignalR.
    // But this may cause concerns with data transmit size, exposure of class object
    // to the Razor component and providing opportunity for inadvertent modification, etc.
    internal sealed class CodeGroup
    {
        private const string INITIAL_CODE_SNIPPET = "using System;\r\n\r\npublic class Program\r\n{\r\n    public static void Main()\r\n    {\r\n        Console.WriteLine(\"Hello World\");\r\n    }\r\n}";
       
        public CodeGroup(string containerid)
        {
            GroupId = Guid.NewGuid().ToString();
            DockerContainerId = containerid;
        }

        public string CodeEditorContents { get; private set; } = INITIAL_CODE_SNIPPET;
        public string CodeExecutionResult { get; private set; } = String.Empty;
        public string DockerContainerId { get; private set; }
        public string GroupId { get; private set; }
        public void UpdateEditorContents(string editorContents)
        {
            CodeEditorContents = editorContents;
        }

        public void UpdateExecutionResult(string result)
        {
            CodeExecutionResult = result;
        }
    }
}