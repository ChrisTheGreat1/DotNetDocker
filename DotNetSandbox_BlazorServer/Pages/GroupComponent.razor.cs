using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace DotNetSandbox_BlazorServer.Pages
{
    public partial class GroupComponent
    {
        private bool codeExecuting = false;
        private string codeResult = String.Empty;
        private bool groupExists = false;
        private string groupTextEditor = String.Empty;
        private HubConnection? hubConnection;

        [Parameter]
        public string GroupGuid { get; set; } = default!;

        public async ValueTask DisposeAsync()
        {
            if (hubConnection is not null && hubConnection.ConnectionId is not null)
            {
                await hubContext.Groups.RemoveFromGroupAsync(hubConnection.ConnectionId, GroupGuid);
                await hubConnection.DisposeAsync();
            }
        }

        protected override async Task OnInitializedAsync()
        {
            hubConnection = new HubConnectionBuilder()
                .WithUrl(Navigation.ToAbsoluteUri("/codehub"))
                .Build();

            hubConnection.On<string>("ReceiveCodeFromGroup", (code) =>
            {
                groupTextEditor = code;
                InvokeAsync(StateHasChanged);
            });

            hubConnection.On<string>("GroupCodeExecutionResult", (result) =>
            {
                codeResult = result;
                InvokeAsync(StateHasChanged);
            });

            hubConnection.On("GroupCodeExecuting", () =>
            {
                codeExecuting = true;
                InvokeAsync(StateHasChanged);
            });

            hubConnection.On("GroupCodeExecutingFinished", () =>
            {
                codeExecuting = false;
                InvokeAsync(StateHasChanged);
            });

            hubConnection.On<string, string, bool>("RetrieveGroupState", (groupEditorContents, groupCodeResult, isGroupLocked) =>
            {
                groupTextEditor = groupEditorContents;
                codeResult = groupCodeResult;
                codeExecuting = isGroupLocked;
                InvokeAsync(StateHasChanged);
            });

            hubConnection.On<bool>("CheckGroupExists", (_groupExists) =>
            {
                groupExists = _groupExists;
                InvokeAsync(StateHasChanged);
            });

            await hubConnection.StartAsync();
            await hubConnection.SendAsync("CheckGroupExists", GroupGuid);
            await hubContext.Groups.AddToGroupAsync(hubConnection.ConnectionId, GroupGuid);
            await hubConnection.SendAsync("RetrieveGroupState", GroupGuid);
        }

        private async Task DeleteGroup()
        {
            if (hubConnection is not null)
            {
                await hubConnection.SendAsync("DeleteGroup", GroupGuid);
            }
        }

        private async Task ExecuteGroupCode()
        {
            if (hubConnection is not null)
            {
                await hubConnection.SendAsync("ExecuteGroupCode", groupTextEditor, GroupGuid);
            }
        }

        private async Task FormatTextArea()
        {
            var tree = CSharpSyntaxTree.ParseText(groupTextEditor);
            var root = tree.GetRoot().NormalizeWhitespace();
            var formattedCode = root.ToFullString();

            groupTextEditor = formattedCode;

            await SendCodeToGroup();
        }

        private async Task SendCodeToGroup()
        {
            if (hubConnection is not null)
            {
                await hubConnection.SendAsync("SendCodeToGroup", groupTextEditor, GroupGuid);
            }
        }
    }
}