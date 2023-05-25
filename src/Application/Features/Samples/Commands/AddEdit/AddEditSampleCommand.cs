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
    private readonly IConfiguration _configuration;
    private readonly IApplicationDbContext _context;
    private readonly IMapper _mapper;
    private readonly IStringLocalizer<AddEditSampleCommandHandler> _localizer;
    public AddEditSampleCommandHandler(
        IConfiguration configuration,
        IApplicationDbContext context,
        IStringLocalizer<AddEditSampleCommandHandler> localizer,
        IMapper mapper
        )
    {
        _configuration = configuration;
        _context = context;
        _localizer = localizer;
        _mapper = mapper;
    }
    public async Task<Result<int>> Handle(AddEditSampleCommand request, CancellationToken cancellationToken)
    {

        var dto = _mapper.Map<SampleDto>(request);
        if (request.Id > 0)
        {
            var item = await _context.Samples.FindAsync(new object[] { request.Id }, cancellationToken) ?? throw new NotFoundException($"Sample with id: [{request.Id}] not found.");
            item = _mapper.Map(dto, item);
            item.Result =await SyncToCompareFace(item);
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

    private async Task<string> SyncToCompareFace(Sample sample)
    {
        try
        {
            var endpoint = _configuration.GetValue<string>("CompareFaceApi:Endpoint");
            var apikey = _configuration.GetValue<string>("CompareFaceApi:RecognitionApiKey");
            var host = endpoint.Split(':')[0];
            var port = endpoint.Split(':')[1].Replace("/", "");
            var client = new CompreFaceClient(domain: host, port: port);
            var faceRecognitionService = client.GetCompreFaceService<RecognitionService>(apikey);
            var result = await faceRecognitionService.Subject.DeleteAsync(new DeleteSubjectRequest() { ActualSubject = sample.Name });
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
            return "Sync to Docker";
        }catch(Exception e)
        {
            return e.Message;
        }
    }
}

