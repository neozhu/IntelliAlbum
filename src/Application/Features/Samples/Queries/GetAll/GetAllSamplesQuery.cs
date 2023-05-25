// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CleanArchitecture.Blazor.Application.Features.Samples.DTOs;
using CleanArchitecture.Blazor.Application.Features.Samples.Caching;

namespace CleanArchitecture.Blazor.Application.Features.Samples.Queries.GetAll;

    public class GetAllSamplesQuery : ICacheableRequest<IEnumerable<SampleDto>>
    {
       [IgnoreFilter]
       public string CacheKey => SampleCacheKey.GetAllCacheKey;
       [IgnoreFilter]
       public MemoryCacheEntryOptions? Options => SampleCacheKey.MemoryCacheEntryOptions;
    }
    
    public class GetAllSamplesQueryHandler :
         IRequestHandler<GetAllSamplesQuery, IEnumerable<SampleDto>>
    {
        private readonly IApplicationDbContext _context;
        private readonly IMapper _mapper;
        private readonly IStringLocalizer<GetAllSamplesQueryHandler> _localizer;

        public GetAllSamplesQueryHandler(
            IApplicationDbContext context,
            IMapper mapper,
            IStringLocalizer<GetAllSamplesQueryHandler> localizer
            )
        {
            _context = context;
            _mapper = mapper;
            _localizer = localizer;
        }

        public async Task<IEnumerable<SampleDto>> Handle(GetAllSamplesQuery request, CancellationToken cancellationToken)
        {

            var data = await _context.Samples
                         .ProjectTo<SampleDto>(_mapper.ConfigurationProvider)
                         .AsNoTracking()
                         .ToListAsync(cancellationToken);
            return data;
        }
    }


