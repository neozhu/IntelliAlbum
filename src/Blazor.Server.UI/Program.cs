using Blazor.Server.UI;
using Blazor.Server.UI.Services.Notifications;
using CleanArchitecture.Blazor.Application;
using CleanArchitecture.Blazor.Application.BackendServices;
using CleanArchitecture.Blazor.Application.Services.BackendServices;
using CleanArchitecture.Blazor.Infrastructure;
using CleanArchitecture.Blazor.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.Connections;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.RegisterSerilog();
builder.Services.AddBlazorUiServices();
builder.Services.AddInfrastructureServices(builder.Configuration)
    .AddApplicationServices();

WebApplication app = builder.Build();

app.MapHealthChecks("/health");
app.UseExceptionHandler("/Error");
app.MapFallbackToPage("/_Host");
app.UseInfrastructure(builder.Configuration);
app.UseWebSockets();
app.MapBlazorHub(options => options.Transports = HttpTransportType.WebSockets);

if (app.Environment.IsDevelopment())
{
    // Initialise and seed database
    using (IServiceScope scope = app.Services.CreateScope())
    {
        ApplicationDbContextInitializer initializer = scope.ServiceProvider.GetRequiredService<ApplicationDbContextInitializer>();
        await initializer.InitialiseAsync();
        await initializer.SeedAsync();
        INotificationService? notificationService = scope.ServiceProvider.GetService<INotificationService>();
        if (notificationService is InMemoryNotificationService inMemoryNotificationService)
        {
            inMemoryNotificationService.Preload();
        }
      

    }
}
else
{
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
// Prime the cache
//app.Services.GetRequiredService<ImageCache>().WarmUp().Wait();

var workservice = app.Services.GetRequiredService<WorkService>();
workservice.StartService();
var indexservice = app.Services.GetRequiredService<IndexingService>();
indexservice.StartService();
var thumbnailService = app.Services.GetRequiredService<ThumbnailService>();
var objectDetectService = app.Services.GetRequiredService<ObjectDetectService>();
var faceDetectService = app.Services.GetRequiredService<FaceDetectService>();
var faceRecognizeService = app.Services.GetRequiredService<FaceRecognizeService>();

//var rescanService = app.Services.GetRequiredService<RescanService>();
//await rescanService.MarkAllForRescan( RescanTypes.Thumbnails);
//var file = new FileInfo("d:\\400.jpg");
//var file2 = new FileInfo("d:\\400_face3.jpg");
//var file3 = new FileInfo("d:\\400_face4.jpg");
//var faceapi = app.Services.GetRequiredService<FaceAIService>();
//var result = await faceapi.DetectFace(file);
//var result2 = await faceapi.RecognizeFace(file);

//var imp = app.Services.GetRequiredService<ImageSharpProcessor>();
//await imp.GetCropFaceFile(file, result.Result[0].Box.XMin, result.Result[0].Box.YMin, result.Result[0].Box.XMax, result.Result[0].Box.YMax, file2);
//await imp.GetCropFaceFile(file, result.Result[1].Box.XMin, result.Result[1].Box.YMin, result.Result[1].Box.XMax, result.Result[1].Box.YMax, file3);
////var thumbService = scope.ServiceProvider.GetRequiredService<ThumbnailService>();
await app.RunAsync();