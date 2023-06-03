// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using CleanArchitecture.Blazor.Application.Features.Images.Caching;

namespace CleanArchitecture.Blazor.Application.Features.Samples.Commands.ReScan;
public class ReScanRecognitionCommand : IRequest<Result<int>>
{

}

public class ReScanRecognitionCommandHandler :
             IRequestHandler<ReScanRecognitionCommand, Result<int>>

{
    private readonly ILogger<ReScanRecognitionCommandHandler> _logger;

    private readonly IApplicationDbContext _context;

    private readonly IStringLocalizer<ReScanRecognitionCommandHandler> _localizer;
    public ReScanRecognitionCommandHandler(
        ILogger<ReScanRecognitionCommandHandler> logger,
        IApplicationDbContext context,
        IStringLocalizer<ReScanRecognitionCommandHandler> localizer
        )
    {
        _logger = logger;
        _context = context;
        _localizer = localizer;
    }
    public async Task<Result<int>> Handle(ReScanRecognitionCommand request, CancellationToken cancellationToken)
    {
        var result = await _context.Images.ExecuteUpdateAsync(x => x.SetProperty(y => y.RecognizeFaceStatus, y => 0).SetProperty(y => y.FaceRecognizeLastUpdated, y => null));
        ImageCacheKey.Refresh();
        return await Result<int>.SuccessAsync(result);
    }



}

