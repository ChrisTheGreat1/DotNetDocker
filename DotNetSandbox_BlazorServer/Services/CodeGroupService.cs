using DotNetSandbox_BlazorServer.Data;

namespace DotNetSandbox_BlazorServer.Services
{
    public class CodeGroupService : ICodeGroupService
    {
        private static object lockObj = new();
        private List<CodeGroup> CodeGroupList { get; set; } = new();
        public void CreateGroup(string containerId)
        {
            CodeGroupList.Add(new CodeGroup(containerId));
        }

        public void DeleteGroup(string guid)
        {
            var containerId = CodeGroupList.Single(group => group.GroupId == guid).DockerContainerId;
            CodeGroupList.Remove(CodeGroupList.Single(group => group.GroupId == guid));
        }

        public List<string> GetAllGroupGuidStrings()
        {
            return CodeGroupList.Select(group => group.GroupId).ToList();
        }

        public string GetGroupCodeExecutionResult(string guid)
        {
            return CodeGroupList.Single(group => group.GroupId == guid).CodeExecutionResult;
        }

        public string GetGroupEditorContainerId(string guid)
        {
            return CodeGroupList.Single(group => group.GroupId == guid).DockerContainerId;
        }

        public string GetGroupEditorContents(string guid)
        {
            return CodeGroupList.Single(group => group.GroupId == guid).CodeEditorContents;
        }

        public bool GroupExists(string guid)
        {
            return CodeGroupList.Any(group => group.GroupId.Equals(guid));
        }

        public void UpdateCodeExecutionResult(string guid, string result)
        {
            CodeGroupList.Single(group => group.GroupId == guid).UpdateExecutionResult(result);
        }

        public void UpdateGroupEditorContents(string guid, string code)
        {
            CodeGroupList.Single(group => group.GroupId == guid).UpdateEditorContents(code);
        }
    }
}