// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CleanArchitecture.Blazor.Application.Features.Images.DTOs;
using CleanArchitecture.Blazor.Application.Features.Images.Caching;
using Image = CleanArchitecture.Blazor.Domain.Entities.Image;

namespace CleanArchitecture.Blazor.Application.Features.Images.Queries.Pagination;

public class ImagesWithPaginationQuery : PaginationFilterBase, ICacheableRequest<PaginatedData<ImageDto>>
{
    [CompareTo("Name", "Comments","Keywords")] // <-- This filter will be applied to Name or Description.
    [StringFilterOptions(StringFilterOption.Contains)]
    public string? Keyword { get; set; }
    [Description("Search for creation date")]
    [CompareTo("FileCreationDate")]
    public Range<DateTime>? FileCreationDate { get; set; }
    [Description("Search for recently view date")]
    [CompareTo("RecentlyViewDatetime")]
    public Range<DateTime>? RecentlyViewDatetime { get; set; }
    [Description("Search for folder")]
    [CompareTo("FolderId")]
    public int? FolderId { get; set; }

    [CompareTo(typeof(SearchImagesWithListView), "Id")]
    public ImageListView ListView { get; set; } = ImageListView.All; //<-- When the user selects a different ListView,
                                                                               // a custom query expression is executed on the filter.
    public override string ToString()
    {
        return $"Listview:{ListView},Search:{Keyword},Sort:{Sort},SortBy:{SortBy},{Page},{PerPage},{FolderId},{FileCreationDate?.ToString()},{RecentlyViewDatetime?.ToString()}";
    }
    [IgnoreFilter]
    public string CacheKey => ImageCacheKey.GetPaginationCacheKey($"{this}");
    [IgnoreFilter]
    public MemoryCacheEntryOptions? Options => ImageCacheKey.MemoryCacheEntryOptions;
}
    
public class ImagesWithPaginationQueryHandler :
         IRequestHandler<ImagesWithPaginationQuery, PaginatedData<ImageDto>>
{
        private readonly IApplicationDbContext _context;
        private readonly IMapper _mapper;
        private readonly IStringLocalizer<ImagesWithPaginationQueryHandler> _localizer;

        public ImagesWithPaginationQueryHandler(
            IApplicationDbContext context,
            IMapper mapper,
            IStringLocalizer<ImagesWithPaginationQueryHandler> localizer
            )
        {
            _context = context;
            _mapper = mapper;
            _localizer = localizer;
        }

        public async Task<PaginatedData<ImageDto>> Handle(ImagesWithPaginationQuery request, CancellationToken cancellationToken)
        {
           // TODO: Implement ImagesWithPaginationQueryHandler method 
           var data = await _context.Images.ApplyFilterWithoutPagination(request)
                .ProjectTo<ImageDto>(_mapper.ConfigurationProvider)
                .PaginatedDataAsync(request.Page, request.PerPage);
            return data;
        }
}

public class ImagesPaginationSpecification : Specification<Image>
{
    public ImagesPaginationSpecification(ImagesWithPaginationQuery query)
    {
        Criteria = q => q.Name != null;
        if (!string.IsNullOrEmpty(query.Keyword))
        {
            And(x => x.Name.Contains(query.Keyword));
        }
       
    }
}
public class SearchImagesWithListView : FilteringOptionsBaseAttribute
{
    public override Expression BuildExpression(Expression expressionBody, PropertyInfo targetProperty, PropertyInfo filterProperty, object value)
    {
        var today = DateTime.Now.Date;
        var start = Convert.ToDateTime(today.ToString("yyyy-MM-dd",CultureInfo.CurrentCulture) + " 00:00:00", CultureInfo.CurrentCulture);
        var end = Convert.ToDateTime(today.ToString("yyyy-MM-dd",CultureInfo.CurrentCulture) + " 23:59:59", CultureInfo.CurrentCulture);
        var end30 = Convert.ToDateTime(today.AddDays(30).ToString("yyyy-MM-dd", CultureInfo.CurrentCulture) + " 23:59:59", CultureInfo.CurrentCulture);
        var listview = (ImageListView)value;
        return listview switch {
            ImageListView.All => expressionBody,
            ImageListView.DetectObjectError => Expression.Equal(Expression.Property(expressionBody, "DetectObjectStatus"), Expression.Constant(3, typeof(int))),
            ImageListView.DetectFaceError => Expression.Equal(Expression.Property(expressionBody, "DetectFaceStatus"), Expression.Constant(3, typeof(int))),
            ImageListView.RecognizeFaceError => Expression.Equal(Expression.Property(expressionBody, "RecognizeFaceStatus"), Expression.Constant(3, typeof(int))),
            ImageListView.CreatedToday => Expression.GreaterThanOrEqual(Expression.Property(expressionBody, "Created"),
                                                                          Expression.Constant(start, typeof(DateTime?)))
                                            .Combine(Expression.LessThanOrEqual(Expression.Property(expressionBody, "Created"),
                                                     Expression.Constant(end, typeof(DateTime?))),
                                                     CombineType.And),
            ImageListView.Created30Days => Expression.GreaterThanOrEqual(Expression.Property(expressionBody, "Created"),
                                                                          Expression.Constant(start, typeof(DateTime?)))
                                            .Combine(Expression.LessThanOrEqual(Expression.Property(expressionBody, "Created"),
                                                     Expression.Constant(end30, typeof(DateTime?))),
                                                     CombineType.And),
            _ => expressionBody
        }; ;
    }
}
public enum ImageListView
{
    [Description("All")]
    All,
    [Description("Created Toady")]
    CreatedToday,
    [Description("Detect Object Error")]
    DetectObjectError,
    [Description("Detect Face Error")]
    DetectFaceError,
    [Description("Recognize Face Error")]
    RecognizeFaceError,
    [Description("Created within the last 30 days")]
    Created30Days
}