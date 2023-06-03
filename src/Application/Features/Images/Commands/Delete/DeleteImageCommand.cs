// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CleanArchitecture.Blazor.Application.Features.Images.DTOs;
using CleanArchitecture.Blazor.Application.Features.Images.Caching;


namespace CleanArchitecture.Blazor.Application.Features.Images.Commands.Delete;

    public class DeleteImageCommand: FilterBase, ICacheInvalidatorRequest<Result<int>>
    {
      [ArraySearchFilter()]
      public int[] Id {  get; }
      public string CacheKey => ImageCacheKey.GetAllCacheKey;
      public CancellationTokenSource? SharedExpiryTokenSource => ImageCacheKey.SharedExpiryTokenSource();
      public DeleteImageCommand(int[] id)
      {
        Id = id;
      }
    }

    public class DeleteImageCommandHandler : 
                 IRequestHandler<DeleteImageCommand, Result<int>>

    {
        private readonly IApplicationDbContext _context;
        private readonly IMapper _mapper;
        private readonly IStringLocalizer<DeleteImageCommandHandler> _localizer;
        public DeleteImageCommandHandler(
            IApplicationDbContext context,
            IStringLocalizer<DeleteImageCommandHandler> localizer,
             IMapper mapper
            )
        {
            _context = context;
            _localizer = localizer;
            _mapper = mapper;
        }
        public async Task<Result<int>> Handle(DeleteImageCommand request, CancellationToken cancellationToken)
        {
            var items = await _context.Images.ApplyFilter(request).ToListAsync(cancellationToken);
            foreach (var item in items)
            {
			    // raise a delete domain event
				item.AddDomainEvent(new ImageDeletedEvent(item));
                _context.Images.Remove(item);
            }
            var result = await _context.SaveChangesAsync(cancellationToken);
            return await Result<int>.SuccessAsync(result);
        }

    }

