// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CleanArchitecture.Blazor.Application.Features.Samples.DTOs;
using CleanArchitecture.Blazor.Application.Features.Samples.Caching;
using Microsoft.AspNetCore.Components.Forms;

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
    private readonly IApplicationDbContext _context;
    private readonly IMapper _mapper;
    private readonly IStringLocalizer<AddEditSampleCommandHandler> _localizer;
    public AddEditSampleCommandHandler(
        IApplicationDbContext context,
        IStringLocalizer<AddEditSampleCommandHandler> localizer,
        IMapper mapper
        )
    {
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
            _context.Samples.Add(item);
            await _context.SaveChangesAsync(cancellationToken);
            return await Result<int>.SuccessAsync(item.Id);
        }

    }
}

