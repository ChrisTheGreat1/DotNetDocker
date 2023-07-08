namespace DotNetSandbox_BlazorServer.Services
{
    public interface ICodeGroupService
    {
        void CreateGroup(string containerId);

        void DeleteGroup(string guid);

        List<string>? GetAllGroupGuidStrings();

        string GetGroupCodeExecutionResult(string guid);

        string GetGroupEditorContainerId(string guid);

        string GetGroupEditorContents(string guid);
        bool GroupExists(string guid);

        void UpdateCodeExecutionResult(string guid, string result);

        void UpdateGroupEditorContents(string guid, string code);
    }
}