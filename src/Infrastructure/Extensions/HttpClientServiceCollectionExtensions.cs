using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Polly;
using Polly.Extensions.Http;

namespace CleanArchitecture.Blazor.Infrastructure.Extensions;
public static class HttpClientServiceCollectionExtensions
{
    public static void AddHttpClientService(this IServiceCollection services, IConfiguration config)
    {
         services.AddHttpClient("ocr", c =>
            {
                c.BaseAddress = new Uri("https://paddleocr.i247365.net/predict/ocr_system");
                c.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            }).AddTransientHttpErrorPolicy(policy => policy.WaitAndRetryAsync(3, _ => TimeSpan.FromSeconds(30)));

        var policy = HttpPolicyExtensions.HandleTransientHttpError().OrResult(response => (int)response.StatusCode == 500).WaitAndRetryAsync(5, _ => TimeSpan.FromSeconds(10));
        services.AddHttpClient("yolo", c =>
        {
            var endpoint = config.GetValue<string>("DetectObjectApi:Endpoint")?? "http://127.0.0.1:8010/";
        
            c.BaseAddress = new Uri(endpoint);
            c.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "multipart/form-data");
            c.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }).AddPolicyHandler(policy);

        services.AddHttpClient("detection", c =>
        {
            var endpoint = config.GetValue<string>("CompareFaceApi:Endpoint") ?? "http://127.0.0.1:8000/";
            var apikey = config.GetValue<string>("CompareFaceApi:DetectionApiKey") ?? "00000000-0000-0000-0000-000000000003";
            c.BaseAddress = new Uri(endpoint);
            c.DefaultRequestHeaders.TryAddWithoutValidation("x-api-key", apikey);
            c.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "multipart/form-data");
            c.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }).AddPolicyHandler(policy);
        services.AddHttpClient("recognition", c =>
        {
            var endpoint = config.GetValue<string>("CompareFaceApi:Endpoint") ?? "http://127.0.0.1:8000/";
            var apikey = config.GetValue<string>("CompareFaceApi:RecognitionApiKey") ?? "00000000-0000-0000-0000-000000000004";
            c.BaseAddress = new Uri(endpoint);
            c.DefaultRequestHeaders.TryAddWithoutValidation("x-api-key", apikey);
            c.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "multipart/form-data");
            c.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }).AddPolicyHandler(policy);
    }
       
}
