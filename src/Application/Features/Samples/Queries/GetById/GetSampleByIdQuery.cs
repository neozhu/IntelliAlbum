// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CleanArchitecture.Blazor.Application.Features.Samples.DTOs;
using CleanArchitecture.Blazor.Application.Features.Samples.Caching;

namespace CleanArchitecture.Blazor.Application.Features.Samples.Queries.GetById;

    public class GetSampleByIdQuery :FilterBase, ICacheableRequest<SampleDto>
    {
       [OperatorComparison(OperatorType.Equal)]
       public required int Id { get; set; }
       [IgnoreFilter]
       public string CacheKey => SampleCacheKey.GetByIdCacheKey($"{Id}");
       [IgnoreFilter]
       public MemoryCacheEntryOptions? Options => SampleCacheKey.MemoryCacheEntryOptions;
    }
    
    public class GetSampleByIdQueryHandler :
         IRequestHandler<GetSampleByIdQuery, SampleDto>
    {
        private readonly IApplicationDbContext _context;
        private readonly IMapper _mapper;
        private readonly IStringLocalizer<GetSampleByIdQueryHandler> _localizer;

        public GetSampleByIdQueryHandler(
            IApplicationDbContext context,
            IMapper mapper,
            IStringLocalizer<GetSampleByIdQueryHandler> localizer
            )
        {
            _context = context;
            _mapper = mapper;
            _localizer = localizer;
        }

        public async Task<SampleDto> Handle(GetSampleByIdQuery request, CancellationToken cancellationToken)
        {

            var data = await _context.Samples.ApplyFilter(request)
                         .ProjectTo<SampleDto>(_mapper.ConfigurationProvider)
                         .FirstAsync(cancellationToken) ?? throw new NotFoundException($"Sample with id: [{request.Id}] not found.");;
            return data;
        }
    }


