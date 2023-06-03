// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CleanArchitecture.Blazor.Application.Features.Samples.DTOs;
using CleanArchitecture.Blazor.Application.Features.Samples.Caching;
using Microsoft.Extensions.Configuration;
using Exadel.Compreface.Clients.CompreFaceClient;
using Exadel.Compreface.Services.RecognitionService;
using Exadel.Compreface.DTOs.SubjectDTOs.DeleteSubject;

namespace CleanArchitecture.Blazor.Application.Features.Samples.Commands.Delete;
public class DeleteSampleCommand: FilterBase, ICacheInvalidatorRequest<Result<int>>
{
    [ArraySearchFilter()]
    public int[] Id {  get; }
      public string CacheKey => SampleCacheKey.GetAllCacheKey;
      public CancellationTokenSource? SharedExpiryTokenSource => SampleCacheKey.SharedExpiryTokenSource();
      public DeleteSampleCommand(int[] id)
      {
        Id = id;
      }
    }

    public class DeleteSampleCommandHandler : 
                 IRequestHandler<DeleteSampleCommand, Result<int>>

    {
    private readonly ILogger<DeleteSampleCommandHandler> _logger;
    private readonly IConfiguration _configuration;
    private readonly IApplicationDbContext _context;
        private readonly IMapper _mapper;
        private readonly IStringLocalizer<DeleteSampleCommandHandler> _localizer;
        public DeleteSampleCommandHandler(
            ILogger<DeleteSampleCommandHandler> logger, 
            IConfiguration configuration,
            IApplicationDbContext context,
            IStringLocalizer<DeleteSampleCommandHandler> localizer,
             IMapper mapper
            )
        {
        _logger = logger;
        _configuration = configuration;
        _context = context;
            _localizer = localizer;
            _mapper = mapper;
        }
        public async Task<Result<int>> Handle(DeleteSampleCommand request, CancellationToken cancellationToken)
        {
            var items = await _context.Samples.ApplyFilter(request).ToListAsync(cancellationToken);
            foreach (var item in items)
            {
			    // raise a delete domain event
				item.AddDomainEvent(new SampleDeletedEvent(item));
                _context.Samples.Remove(item);
                await deleteSubject(item);
            }
     
        var result = await _context.SaveChangesAsync(cancellationToken);
            return await Result<int>.SuccessAsync(result);
        }

    private async Task deleteSubject(Sample sample)
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
                var result = await faceRecognitionService.Subject.DeleteAsync(new DeleteSubjectRequest() { ActualSubject = sample.Name });
            }
            _logger.LogInformation($"Delete Subject :{sample.Name} ");
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"Delete Subject error:{sample.Name} ");
        }
    }

}

