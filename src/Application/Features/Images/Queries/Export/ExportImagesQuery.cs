// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using CleanArchitecture.Blazor.Application.Features.Images.DTOs;
using CleanArchitecture.Blazor.Application.Features.Images.Queries.Pagination;

namespace CleanArchitecture.Blazor.Application.Features.Images.Queries.Export;

public class ExportImagesQuery : OrderableFilterBase, IRequest<Result<byte[]>>
{
        [CompareTo("Name", "Description")] // <-- This filter will be applied to Name or Description.
        [StringFilterOptions(StringFilterOption.Contains)]
        public string? Keyword { get; set; }
        [CompareTo(typeof(SearchImagesWithListView), "Id")]
        public ImageListView ListView { get; set; } = ImageListView.All;
}
    
public class ExportImagesQueryHandler :
         IRequestHandler<ExportImagesQuery, Result<byte[]>>
{
        private readonly IApplicationDbContext _context;
        private readonly IMapper _mapper;
        private readonly IExcelService _excelService;
        private readonly IStringLocalizer<ExportImagesQueryHandler> _localizer;
        private readonly ImageDto _dto = new();
        public ExportImagesQueryHandler(
            IApplicationDbContext context,
            IMapper mapper,
            IExcelService excelService,
            IStringLocalizer<ExportImagesQueryHandler> localizer
            )
        {
            _context = context;
            _mapper = mapper;
            _excelService = excelService;
            _localizer = localizer;
        }

        public async Task<Result<byte[]>> Handle(ExportImagesQuery request, CancellationToken cancellationToken)
        {
            // TODO: Implement ExportImagesQueryHandler method 
            var data = await _context.Images.ApplyOrder(request)
                       .ProjectTo<ImageDto>(_mapper.ConfigurationProvider)
                       .AsNoTracking()
                       .ToListAsync(cancellationToken);
            var result = await _excelService.ExportAsync(data,
                new Dictionary<string, Func<ImageDto, object?>>()
                {
                    // TODO: Define the fields that should be exported, for example:
                    {_localizer[_dto.GetMemberDescription(x=>x.Id)],item => item.Id}, 
{_localizer[_dto.GetMemberDescription(x=>x.FolderId)],item => item.FolderId}, 
{_localizer[_dto.GetMemberDescription(x=>x.Name)],item => item.Name}, 
{_localizer[_dto.GetMemberDescription(x=>x.Comments)],item => item.Comments}, 
{_localizer[_dto.GetMemberDescription(x=>x.FileSizeBytes)],item => item.FileSizeBytes}, 
{_localizer[_dto.GetMemberDescription(x=>x.FileCreationDate)],item => item.FileCreationDate}, 
{_localizer[_dto.GetMemberDescription(x=>x.FileLastModDate)],item => item.FileLastModDate}, 
{_localizer[_dto.GetMemberDescription(x=>x.RecentlyViewDatetime)],item => item.RecentlyViewDatetime}, 

                }
                , _localizer[_dto.GetClassDescription()]);
            return await Result<byte[]>.SuccessAsync(result);;
        }
}
