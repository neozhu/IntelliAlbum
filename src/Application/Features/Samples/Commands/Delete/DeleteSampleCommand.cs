// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CleanArchitecture.Blazor.Application.Features.Samples.DTOs;
using CleanArchitecture.Blazor.Application.Features.Samples.Caching;


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
        private readonly IApplicationDbContext _context;
        private readonly IMapper _mapper;
        private readonly IStringLocalizer<DeleteSampleCommandHandler> _localizer;
        public DeleteSampleCommandHandler(
            IApplicationDbContext context,
            IStringLocalizer<DeleteSampleCommandHandler> localizer,
             IMapper mapper
            )
        {
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
            }
            var result = await _context.SaveChangesAsync(cancellationToken);
            return await Result<int>.SuccessAsync(result);
        }

    }

