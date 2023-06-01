// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CleanArchitecture.Blazor.Application.Features.Images.DTOs;
using CleanArchitecture.Blazor.Application.Features.Images.Caching;


namespace CleanArchitecture.Blazor.Application.Features.Images.Commands.Rescan;

    public class RescanImageCommand : FilterBase, ICacheInvalidatorRequest<Result<int>>
    {
      public int[] Id {  get; }
      public int Step { get; set; }
      public string CacheKey => ImageCacheKey.GetAllCacheKey;
      public CancellationTokenSource? SharedExpiryTokenSource => ImageCacheKey.SharedExpiryTokenSource();
      public RescanImageCommand(int[] id,int step)
      {
        Id = id;
        Step = step;
      }
    }

    public class RescanImageCommandHandler : 
                 IRequestHandler<RescanImageCommand, Result<int>>

    {
        private readonly IApplicationDbContext _context;
        private readonly IMapper _mapper;
        private readonly IStringLocalizer<RescanImageCommandHandler> _localizer;
        public RescanImageCommandHandler(
            IApplicationDbContext context,
            IStringLocalizer<RescanImageCommandHandler> localizer,
             IMapper mapper
            )
        {
            _context = context;
            _localizer = localizer;
            _mapper = mapper;
        }
        public async Task<Result<int>> Handle(RescanImageCommand request, CancellationToken cancellationToken)
        {
            
            var result = await _context.Images.Where(x=>request.Id.Contains(x.Id))
                                      .ExecuteUpdateAsync(x=>x.SetProperty(y=>y.DetectObjectStatus,y=>0)
                                                              .SetProperty(y=>y.ObjectDetectLastUpdated,y=>null)
                                                              .SetProperty(y => y.DetectFaceStatus, y => 0)
                                                              .SetProperty(y => y.FaceDetectLastUpdated, y => null)
                                                              .SetProperty(y => y.RecognizeFaceStatus, y => 0)
                                                              .SetProperty(y => y.FaceRecognizeLastUpdated, y => null));
            return await Result<int>.SuccessAsync(result);
        }

    }

