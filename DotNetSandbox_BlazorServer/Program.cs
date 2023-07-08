using DotNetSandbox_BlazorServer.Hubs;
using DotNetSandbox_BlazorServer.Services;
using Microsoft.AspNetCore.ResponseCompression;

namespace DotNetSandbox_BlazorServer
{
    public class Program
    {
        public static void Main(string[] args)
        {

            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddRazorPages();
            builder.Services.AddServerSideBlazor();

            // Note that singleton and scoped services are not recommended for Blazor Server applications
            // but I need to use them here regardless.
            builder.Services.AddSingleton<ICodeGroupService, CodeGroupService>();
            builder.Services.AddSingleton<IMessageBrokerService, RabbitMqMessageBroker>();

            builder.Services.AddResponseCompression(opts =>
            {
                opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
                      new[] { "application/octet-stream" });
            });

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseResponseCompression();

                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();

            app.UseStaticFiles();

            app.UseRouting();

            app.MapBlazorHub();
            app.MapHub<CodeHub>("/codehub");
            app.MapFallbackToPage("/_Host");

            app.Run();
        }
    }
}