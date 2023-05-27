// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CleanArchitecture.Blazor.Application.Features.Samples.DTOs;
using CleanArchitecture.Blazor.Application.Features.Samples.Caching;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Configuration;
using Exadel.Compreface.Configuration;
using Exadel.Compreface.Clients.CompreFaceClient;
using Exadel.Compreface.Services.RecognitionService;
using Exadel.Compreface.DTOs.SubjectDTOs.DeleteSubject;
using Exadel.Compreface.DTOs.SubjectDTOs.AddSubject;
using Exadel.Compreface.DTOs.FaceCollectionDTOs.AddSubjectExample;
using Flurl;
using Exadel.Compreface.DTOs.FaceCollectionDTOs.ListAllExampleSubject;
using Exadel.Compreface.Services;
using Exadel.Compreface.DTOs.FaceDetectionDTOs.FaceDetection;
using CleanArchitecture.Blazor.Application.BackendServices;

namespace CleanArchitecture.Blazor.Application.Features.Samples.Commands.AddEdit;

public class AddEditSampleCommand : IMapFrom<SampleDto>, ICacheInvalidatorRequest<Result<int>>
{
    [Description("Id")]
    public int Id { get; set; }
    [Description("Name")]
    public string Name { get; set; } = String.Empty;
    [Description("Description")]
    public string? Description { get; set; }
    [Description("Sample Images")]
    public List<SampleImage>? SampleImages { get; set; }
    [Description("Threshold")]
    public float Threshold { get; set; } = 0.8f;
    [Description("Result")]
    public string? Result { get; set; }
    public IReadOnlyList<IBrowserFile>? UploadPictures { get; set; }

    public string CacheKey => SampleCacheKey.GetAllCacheKey;
    public CancellationTokenSource? SharedExpiryTokenSource => SampleCacheKey.SharedExpiryTokenSource();
}

public class AddEditSampleCommandHandler : IRequestHandler<AddEditSampleCommand, Result<int>>
{
    private readonly ImageSharpProcessor _imageSharpProcessor;
    private readonly ILogger<AddEditSampleCommandHandler> _logger;
    private readonly IConfiguration _configuration;
    private readonly IApplicationDbContext _context;
    private readonly IMapper _mapper;
    private readonly IStringLocalizer<AddEditSampleCommandHandler> _localizer;
    public AddEditSampleCommandHandler(
        ImageSharpProcessor imageSharpProcessor,
        ILogger<AddEditSampleCommandHandler> logger,
        IConfiguration configuration,
        IApplicationDbContext context,
        IStringLocalizer<AddEditSampleCommandHandler> localizer,
        IMapper mapper
        )
    {
        _imageSharpProcessor = imageSharpProcessor;
        _logger = logger;
        _configuration = configuration;
        _context = context;
        _localizer = localizer;
        _mapper = mapper;
    }
    public async Task<Result<int>> Handle(AddEditSampleCommand request, CancellationToken cancellationToken)
    {

        var dto = _mapper.Map<SampleDto>(request);
        var result = await DetectFace(dto);
        if (!string.IsNullOrEmpty(result))
        {
            return await Result<int>.FailureAsync(new string[] { result });
        }
        if (request.Id > 0)
        {
            var item = await _context.Samples.FindAsync(new object[] { request.Id }, cancellationToken) ?? throw new NotFoundException($"Sample with id: [{request.Id}] not found.");
            item = _mapper.Map(dto, item);
            item.Result = await SyncToCompareFace(item);
            // raise a update domain event
            item.AddDomainEvent(new SampleUpdatedEvent(item));
            await _context.SaveChangesAsync(cancellationToken);
            return await Result<int>.SuccessAsync(item.Id);
        }
        else
        {
            var item = _mapper.Map<Sample>(dto);
            // raise a create domain event
            item.AddDomainEvent(new SampleCreatedEvent(item));
            item.Result = await SyncToCompareFace(item);
            _context.Samples.Add(item);
            await _context.SaveChangesAsync(cancellationToken);
            return await Result<int>.SuccessAsync(item.Id);
        }

    }
    private async Task<string> DetectFace(SampleDto sample)
    {
        var endpoint = _configuration.GetValue<string>("CompareFaceApi:Endpoint");
        var apikey = _configuration.GetValue<string>("CompareFaceApi:DetectionApiKey");
        var uri = new Uri(endpoint);
        var host = uri.Scheme + "://" + uri.Host;
        var port = uri.Port.ToString();
        var client = new CompreFaceClient(domain: host, port: port);
        var detectService = client.GetCompreFaceService<DetectionService>(apikey);
        foreach (var img in sample.SampleImages)
        {
            var file = new FileInfo(Path.Combine(Directory.GetCurrentDirectory(), img.Url));
            if (file.Exists)
            {
                var faceDetectionRequestByFilePath = new FaceDetectionRequestByFilePath()
                {
                    FilePath = file.FullName,
                    DetProbThreshold = 0.8m,
                };
                try
                {
                    var detectResponse = await detectService.DetectAsync(faceDetectionRequestByFilePath);
                    if (detectResponse != null)
                    {
                        if (detectResponse.Result.Count == 1)
                        {
                            var bbox = detectResponse.Result[0].Box;
                            await _imageSharpProcessor.GetCropFaceFile(file, bbox.XMin, bbox.YMin, bbox.XMax, bbox.YMax, file);
                        }
                        else if (detectResponse.Result.Count > 1)
                        {
                            return "More than one face in the image";
                        }
                        else
                        {
                            return "No face is found in the given image";

                        }
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Detect Face fault.");
                    return e.Message;
                }
                
            }
        }
        return string.Empty;
    }
    private async Task<string> SyncToCompareFace(Sample sample)
    {
        try
        {
            var endpoint = _configuration.GetValue<string>("CompareFaceApi:Endpoint");
            var apikey = _configuration.GetValue<string>("CompareFaceApi:RecognitionApiKey");
            var uri = new Uri(endpoint);
            var host = uri.Scheme + "://" + uri.Host;
            var port = uri.Port.ToString();
            var client = new CompreFaceClient(domain: host, port: port);
            var faceRecognitionService = client.GetCompreFaceService<RecognitionService>(apikey);
            var list = await faceRecognitionService.Subject.ListAsync();
            if (list.Subjects.Any(x => x.Equals(sample.Name, StringComparison.CurrentCultureIgnoreCase)))
            {
                var deleteAllExamples = await faceRecognitionService.FaceCollection.DeleteAllAsync(new Exadel.Compreface.DTOs.FaceCollectionDTOs.DeleteAllSubjectExamples.DeleteAllExamplesRequest() { Subject = sample.Name });
                foreach (var img in sample.SampleImages)
                {
                    var file = new FileInfo(Path.Combine(Directory.GetCurrentDirectory(), img.Url));
                    if (file.Exists)
                    {
                        var addimage = await faceRecognitionService.FaceCollection.AddAsync(
                                new AddSubjectExampleRequestByFilePath()
                                {
                                    Subject = sample.Name,
                                    DetProbThreShold = Convert.ToDecimal(sample.Threshold),
                                    FilePath = file.FullName
                                });
                    }
                }
            }
            else
            {
                var addresult = await faceRecognitionService.Subject.AddAsync(new AddSubjectRequest() { Subject = sample.Name });
                if (addresult.Subject == sample.Name)
                {
                    foreach (var img in sample.SampleImages)
                    {
                        var file = new FileInfo(Path.Combine(Directory.GetCurrentDirectory(), img.Url));
                        if (file.Exists)
                        {
                            var addimage = await faceRecognitionService.FaceCollection.AddAsync(
                                    new AddSubjectExampleRequestByFilePath()
                                    {
                                        Subject = addresult.Subject,
                                        DetProbThreShold = Convert.ToDecimal(sample.Threshold),
                                        FilePath = file.FullName
                                    });
                        }

                    }

                }
            }
            return "Sync success.";
        }
        catch (Exception e)
        {
            return e.Message;
        }
    }
}

