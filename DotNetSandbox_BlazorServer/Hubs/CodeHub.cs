using DotNetSandbox_BlazorServer.Services;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace DotNetSandbox_BlazorServer.Hubs
{
    public class CodeHub : Hub
    {
        private static ConcurrentDictionary<string, SemaphoreSlim> _semaphores = new();
        private readonly ICodeGroupService _codeGroupService;
        private readonly IMessageBrokerService _messageBrokerService;
        public CodeHub(ICodeGroupService codeGroupService, IMessageBrokerService messageBrokerService)
        {
            _codeGroupService = codeGroupService;
            _messageBrokerService = messageBrokerService;
        }

        public async Task CheckGroupExists(string groupId)
        {
            await Clients.Caller.SendAsync("CheckGroupExists", _codeGroupService.GroupExists(groupId));
        }

        public async Task CreateGroup()
        {
            //__messageBrokerService.TestHelloMessaging();
            var containerId = await _messageBrokerService.CreateContainerAsync();
            _codeGroupService.CreateGroup(containerId);
            await UpdateGroupsList();
        }

        public async Task DeleteGroup(string groupId)
        {
            if (!_codeGroupService.GroupExists(groupId)) return;

            // Use semaphore to prevent code group deletion from occurring while Docker container is executing.
            var semaphore = _semaphores.GetOrAdd(groupId, _ => new SemaphoreSlim(1, 1));
            if (!await semaphore.WaitAsync(0)) return; // Try to acquire semaphore. If it is already acquired, return out of method.

            var containerId = _codeGroupService.GetGroupEditorContainerId(groupId);
            _codeGroupService.DeleteGroup(groupId);
            _messageBrokerService.DeleteContainer(containerId);

            await Clients.Group(groupId).SendAsync("CheckGroupExists", _codeGroupService.GroupExists(groupId));
        }

        public async Task ExecuteGroupCode(string code, string groupId)
        {
            if (!_codeGroupService.GroupExists(groupId)) return;

            // Use semaphore to prevent code group execution requests from occurring while Docker container is executing.
            var semaphore = _semaphores.GetOrAdd(groupId, _ => new SemaphoreSlim(1, 1));
            if (!await semaphore.WaitAsync(0)) return; // Try to acquire semaphore. If it is already acquired, return out of method.

            try
            {
                await Clients.Group(groupId).SendAsync("GroupCodeExecuting");
                var containerId = _codeGroupService.GetGroupEditorContainerId(groupId);
                var result = await _messageBrokerService.ExecuteGroupCodeAsync(containerId, code);
                _codeGroupService.UpdateCodeExecutionResult(groupId, result.stdout);
                await Clients.Group(groupId).SendAsync("GroupCodeExecutionResult", result.stdout);
            }
            finally
            {
                semaphore.Release();
                await Clients.Group(groupId).SendAsync("GroupCodeExecutingFinished");
            }
        }

        public async Task RetrieveGroupState(string groupId)
        {
            if (!_codeGroupService.GroupExists(groupId)) return;

            bool isGroupLocked = _semaphores.GetOrAdd(groupId, _ => new SemaphoreSlim(1, 1)).CurrentCount == 0;

            await Clients.Caller.SendAsync(
                "RetrieveGroupState",
                _codeGroupService.GetGroupEditorContents(groupId),
                _codeGroupService.GetGroupCodeExecutionResult(groupId),
                isGroupLocked);
        }

        public async Task SendCodeToGroup(string code, string groupId)
        {
            if (!_codeGroupService.GroupExists(groupId)) return;

            // Use semaphore to block users from editing group code editor simultaneously or when Docker container is executing.
            var semaphore = _semaphores.GetOrAdd(groupId, _ => new SemaphoreSlim(1, 1));
            if (!await semaphore.WaitAsync(0)) return; // Try to acquire semaphore. If it is already acquired, return out of method.

            try
            {
                _codeGroupService.UpdateGroupEditorContents(groupId, code);
                await Clients.Group(groupId).SendAsync("ReceiveCodeFromGroup", code);
            }
            finally
            {
                semaphore.Release();
            }
        }
        public async Task UpdateGroupsList()
        {
            await Clients.All.SendAsync("UpdateGroupsList", _codeGroupService.GetAllGroupGuidStrings());
        }
    }
}