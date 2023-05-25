// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using CleanArchitecture.Blazor.Application.Features.Samples.DTOs;
using CleanArchitecture.Blazor.Application.Features.Samples.Queries.Pagination;

namespace CleanArchitecture.Blazor.Application.Features.Samples.Queries.Export;

public class ExportSamplesQuery : OrderableFilterBase, IRequest<Result<byte[]>>
{
    [CompareTo("Name", "Description")] // <-- This filter will be applied to Name or Description.
    [StringFilterOptions(StringFilterOption.Contains)]
    public string? Keyword { get; set; }
    [CompareTo(typeof(SearchSamplesWithListView), "Id")]
    public SampleListView ListView { get; set; } = SampleListView.All;
}

public class ExportSamplesQueryHandler :
         IRequestHandler<ExportSamplesQuery, Result<byte[]>>
{
    private readonly IApplicationDbContext _context;
    private readonly IMapper _mapper;
    private readonly IExcelService _excelService;
    private readonly IStringLocalizer<ExportSamplesQueryHandler> _localizer;
    private readonly SampleDto _dto = new();
    public ExportSamplesQueryHandler(
        IApplicationDbContext context,
        IMapper mapper,
        IExcelService excelService,
        IStringLocalizer<ExportSamplesQueryHandler> localizer
        )
    {
        _context = context;
        _mapper = mapper;
        _excelService = excelService;
        _localizer = localizer;
    }

    public async Task<Result<byte[]>> Handle(ExportSamplesQuery request, CancellationToken cancellationToken)
    {
        // TODO: Implement ExportSamplesQueryHandler method 
        var data = await _context.Samples.ApplyOrder(request)
                   .ProjectTo<SampleDto>(_mapper.ConfigurationProvider)
                   .AsNoTracking()
                   .ToListAsync(cancellationToken);
        var result = await _excelService.ExportAsync(data,
            new Dictionary<string, Func<SampleDto, object?>>()
            {
                    // TODO: Define the fields that should be exported, for example:
                    {_localizer[_dto.GetMemberDescription(x=>x.Id)],item => item.Id},
{_localizer[_dto.GetMemberDescription(x=>x.Name)],item => item.Name},
{_localizer[_dto.GetMemberDescription(x=>x.Description)],item => item.Description},
{_localizer[_dto.GetMemberDescription(x=>x.SampleImages)],item =>JsonSerializer.Serialize(item.SampleImages)},
{_localizer[_dto.GetMemberDescription(x=>x.Threshold)],item => item.Threshold},
{_localizer[_dto.GetMemberDescription(x=>x.Result)],item => item.Result},

            }
            , _localizer[_dto.GetClassDescription()]);
        return await Result<byte[]>.SuccessAsync(result); ;
    }
}
