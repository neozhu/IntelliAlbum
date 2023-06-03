// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CleanArchitecture.Blazor.Application.Features.Images.DTOs;
using CleanArchitecture.Blazor.Application.Features.Images.Caching;

namespace CleanArchitecture.Blazor.Application.Features.Images.Queries.GetById;

    public class GetImageByIdQuery :FilterBase, ICacheableRequest<ImageDto>
    {
       [OperatorComparison(OperatorType.Equal)]
       public required int Id { get; set; }
       [IgnoreFilter]
       public string CacheKey => ImageCacheKey.GetByIdCacheKey($"{Id}");
       [IgnoreFilter]
       public MemoryCacheEntryOptions? Options => ImageCacheKey.MemoryCacheEntryOptions;
    }
    
    public class GetImageByIdQueryHandler :
         IRequestHandler<GetImageByIdQuery, ImageDto>
    {
        private readonly IApplicationDbContext _context;
        private readonly IMapper _mapper;
        private readonly IStringLocalizer<GetImageByIdQueryHandler> _localizer;

        public GetImageByIdQueryHandler(
            IApplicationDbContext context,
            IMapper mapper,
            IStringLocalizer<GetImageByIdQueryHandler> localizer
            )
        {
            _context = context;
            _mapper = mapper;
            _localizer = localizer;
        }

        public async Task<ImageDto> Handle(GetImageByIdQuery request, CancellationToken cancellationToken)
        {
            var data = await _context.Images.ApplyFilter(request)
                         .ProjectTo<ImageDto>(_mapper.ConfigurationProvider)
                         .FirstOrDefaultAsync(cancellationToken) ?? throw new NotFoundException($"Image with id: [{request.Id}] not found.");;
            return data;
        }
    }


