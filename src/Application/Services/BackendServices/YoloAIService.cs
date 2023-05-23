using Azure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace CleanArchitecture.Blazor.Application.Services.BackendServices;

public class YoloAIService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<YoloAIService> _logger;
    public const string NAME = "yolo";
    public const string RequestUrl = "img_object_detection_to_json";

    public YoloAIService(IHttpClientFactory httpClientFactory,
        ILogger<YoloAIService> logger)
	{
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async  Task<ObejctDetectResult> DetectObject(FileInfo image)
    {
        using (var client = _httpClientFactory.CreateClient(NAME))
        {
            try
            {
                var requestContent = new MultipartFormDataContent();
                var fileContent = new StreamContent(image.OpenRead());
                requestContent.Add(fileContent, "file", image.Name);
                var response = await client.PostAsync(RequestUrl, requestContent);
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var detectResult = ParseDetectResult(responseContent);
                    return detectResult;
                }
                else
                {
                    _logger.LogError($"目标检测请求失败:{image.Name} status:" + response.StatusCode);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e,$"目标检测请求失败:{image.Name}");
            }
            return new ObejctDetectResult();
        }
    }
    private ObejctDetectResult ParseDetectResult(string responseContent)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true, // 忽略属性名称的大小写
        };

        var detectResult = JsonSerializer.Deserialize<ObejctDetectResult>(responseContent, options);
        return detectResult;
    }

     

}

public class ObejctDetectResult
{
    [JsonPropertyName("detect_objects")]
    public List<DetectObject>? DetectObjects { get; set; }
    [JsonPropertyName("detect_objects_names")]
    public string? DetectObjectsNames { get; set; }
}

public class DetectObject
{
    public string Name { get; set; }
    public double Confidence { get; set; }
    public BoundingBox BBox { get; set; }
}

public class BoundingBox
{
    public double Xmin { get; set; }
    public double Ymin { get; set; }
    public double Xmax { get; set; }
    public double Ymax { get; set; }
}

