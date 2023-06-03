// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
using System.ComponentModel;
using CleanArchitecture.Blazor.Application.Features.Images.DTOs;
using CleanArchitecture.Blazor.Application.Features.Images.Caching;
using CleanArchitecture.Blazor.Application.Features.Folders.DTOs;


namespace CleanArchitecture.Blazor.Application.Features.Images.Commands.Update;

public class UpdateImageCommand : IMapFrom<ImageDto>, ICacheInvalidatorRequest<Result<int>>
{
    [Description("Id")]
    public int Id { get; set; }
    [Description("Folder Id")]
    public int FolderId { get; set; }
    [Description("Name")]
    public string Name { get; set; } = String.Empty;
    [Description("Comments")]
    public string? Comments { get; set; }
    [Description("File Size Bytes")]
    public int FileSizeBytes { get; set; }
    [Description("File Creation Date")]
    public DateTime FileCreationDate { get; set; }
    [Description("File Last Mod Date")]
    public DateTime FileLastModDate { get; set; }
    [Description("Recently View Datetime")]
    public DateTime? RecentlyViewDatetime { get; set; }
    [Description("Metadata")]
    public virtual ImageMetaData MetaData { get; set; } = new();
    [Description("Hash")]
    public virtual Hash Hash { get; set; } = new();
    [Description("Tags")]
    // An image can have many tags
    public virtual List<Tag> ImageTags { get; init; } = new();
    [Description("Classification")]
    public virtual List<ImageClassification> Classification { get; init; } = new();
    [Description("Recognized Objects")]
    public virtual List<ImageObject> ImageObjects { get; init; } = new();
    public FolderDto Folder { get; set; } = null!;

    public string CacheKey => ImageCacheKey.GetAllCacheKey;
    public CancellationTokenSource? SharedExpiryTokenSource => ImageCacheKey.SharedExpiryTokenSource();
}

public class UpdateImageCommandHandler : IRequestHandler<UpdateImageCommand, Result<int>>
{
    private readonly IApplicationDbContext _context;
    private readonly IMapper _mapper;
    private readonly IStringLocalizer<UpdateImageCommandHandler> _localizer;
    public UpdateImageCommandHandler(
        IApplicationDbContext context,
        IStringLocalizer<UpdateImageCommandHandler> localizer,
         IMapper mapper
        )
    {
        _context = context;
        _localizer = localizer;
        _mapper = mapper;
    }
    public async Task<Result<int>> Handle(UpdateImageCommand request, CancellationToken cancellationToken)
    {
        var item = await _context.Images.FindAsync(new object[] { request.Id }, cancellationToken) ?? throw new NotFoundException($"Image with id: [{request.Id}] not found."); ;
        var dto = _mapper.Map<ImageDto>(request);
        item = _mapper.Map(dto, item);
        // raise a update domain event
        item.AddDomainEvent(new ImageUpdatedEvent(item));
        await _context.SaveChangesAsync(cancellationToken);
        return await Result<int>.SuccessAsync(item.Id);
    }
}

