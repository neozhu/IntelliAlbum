// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CleanArchitecture.Blazor.Application.Features.Images.DTOs;
using CleanArchitecture.Blazor.Application.Features.Images.Caching;

namespace CleanArchitecture.Blazor.Application.Features.Images.Queries.GetAll;

    public class GetAllImagesQuery : ICacheableRequest<IEnumerable<ImageDto>>
    {
       [IgnoreFilter]
       public string CacheKey => ImageCacheKey.GetAllCacheKey;
       [IgnoreFilter]
       public MemoryCacheEntryOptions? Options => ImageCacheKey.MemoryCacheEntryOptions;
    }
    
    public class GetAllImagesQueryHandler :
         IRequestHandler<GetAllImagesQuery, IEnumerable<ImageDto>>
    {
        private readonly IApplicationDbContext _context;
        private readonly IMapper _mapper;
        private readonly IStringLocalizer<GetAllImagesQueryHandler> _localizer;

        public GetAllImagesQueryHandler(
            IApplicationDbContext context,
            IMapper mapper,
            IStringLocalizer<GetAllImagesQueryHandler> localizer
            )
        {
            _context = context;
            _mapper = mapper;
            _localizer = localizer;
        }

        public async Task<IEnumerable<ImageDto>> Handle(GetAllImagesQuery request, CancellationToken cancellationToken)
        {
            // TODO: Implement GetAllImagesQueryHandler method 
            var data = await _context.Images
                         .ProjectTo<ImageDto>(_mapper.ConfigurationProvider)
                         .AsNoTracking()
                         .ToListAsync(cancellationToken);
            return data;
        }
    }


