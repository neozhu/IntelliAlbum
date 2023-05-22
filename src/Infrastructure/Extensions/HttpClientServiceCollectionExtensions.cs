using System.Net.Http.Headers;
using Polly;

namespace CleanArchitecture.Blazor.Infrastructure.Extensions;
public static class HttpClientServiceCollectionExtensions
{
    public static void AddHttpClientService(this IServiceCollection services)
    {
         services.AddHttpClient("ocr", c =>
            {
                c.BaseAddress = new Uri("https://paddleocr.i247365.net/predict/ocr_system");
                c.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            }).AddTransientHttpErrorPolicy(policy => policy.WaitAndRetryAsync(3, _ => TimeSpan.FromSeconds(30)));

        services.AddHttpClient("yolo", c =>
        {
            c.BaseAddress = new Uri("https://paddleocr.i247365.net/predict/ocr_system");
            c.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "multipart/form-data");
            c.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }).AddTransientHttpErrorPolicy(policy => policy.WaitAndRetryAsync(2, _ => TimeSpan.FromSeconds(10)));

        services.AddHttpClient("face", c =>
        {
            c.BaseAddress = new Uri("https://paddleocr.i247365.net/predict/ocr_system");
            c.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "multipart/form-data");
            c.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }).AddTransientHttpErrorPolicy(policy => policy.WaitAndRetryAsync(2, _ => TimeSpan.FromSeconds(10)));
    }
       
}
