using Azure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace CleanArchitecture.Blazor.Application.Services.BackendServices;

public class FaceAIService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<YoloAIService> _logger;
    public const string DETECTIONNAME = "detection";
    public const string RECOGNITIONNNAME = "recognition";
    public const string FACEDETECTION_REQUEST = "api/v1/detection/detect";
    public const string FACERECOGNITION_REQUEST = "api/v1/recognition/recognize";

    public FaceAIService(IHttpClientFactory httpClientFactory,
        ILogger<YoloAIService> logger)
	{
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<FaceDetectObject> DetectFace(FileInfo image)
    {
        using (var client = _httpClientFactory.CreateClient(DETECTIONNAME))
        {
            try
            {
                //var facePlugins = "calculator";
                var requestContent = new MultipartFormDataContent();
                var fileContent = new StreamContent(image.OpenRead());
                //requestContent.Add(new StringContent(facePlugins), "face_plugins");
                requestContent.Add(fileContent, "file", image.Name);
                var response = await client.PostAsync(FACEDETECTION_REQUEST, requestContent);
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
            return new FaceDetectObject();
        }
    }
    public async Task<FaceDetectObject> RecognizeFace(FileInfo image)
    {
        using (var client = _httpClientFactory.CreateClient(RECOGNITIONNNAME))
        {
            try
            {
                var detect_faces = "true";
                var requestContent = new MultipartFormDataContent();
                var fileContent = new StreamContent(image.OpenRead());
                requestContent.Add(new StringContent(detect_faces), "detect_faces");
                requestContent.Add(fileContent, "file", image.Name);
                var response = await client.PostAsync(FACERECOGNITION_REQUEST, requestContent);
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
                _logger.LogError(e, $"目标检测请求失败:{image.Name}");
            }
            return new FaceDetectObject();
        }
    }
    private FaceDetectObject ParseDetectResult(string responseContent)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true, // 忽略属性名称的大小写
        };

        var detectResult = JsonSerializer.Deserialize<FaceDetectObject>(responseContent, options);
        return detectResult;
    }

     

}

public class Box
{
    [JsonPropertyName("probability")]
    public float Probability { get; set; }
    [JsonPropertyName("x_max")]
    public int XMax { get; set; }
    [JsonPropertyName("y_max")]
    public int YMax { get; set; }
    [JsonPropertyName("x_min")]
    public int XMin { get; set; }
    [JsonPropertyName("y_min")]
    public int YMin { get; set; }
}

public class Similarity
{
    public string Subject { get; set; }
    [JsonPropertyName("similarity")]
    public float SimilarityScore { get; set; }
}

public class Result
{
    public Box Box { get; set; }
    public List<List<int>> Landmarks { get; set; }
    public double[] Embedding { get; set; }
    [JsonPropertyName("subjects")]
    public List<Similarity> Similarities { get; set; }
}

public class FaceDetectObject
{
    public List<Result> Result { get; set; } = new();
}

