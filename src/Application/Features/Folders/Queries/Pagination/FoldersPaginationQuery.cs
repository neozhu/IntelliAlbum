// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CleanArchitecture.Blazor.Application.Features.Folders.DTOs;
using CleanArchitecture.Blazor.Application.Features.Folders.Caching;

namespace CleanArchitecture.Blazor.Application.Features.Folders.Queries.Pagination;

public class FoldersWithPaginationQuery : PaginationFilterBase, ICacheableRequest<PaginatedData<FolderDto>>
{
    [CompareTo("Name", "Description")] // <-- This filter will be applied to Name or Description.
    [StringFilterOptions(StringFilterOption.Contains)]
    public string? Keyword { get; set; }
    [CompareTo(typeof(SearchFoldersWithListView), "Id")]
    public FolderListView ListView { get; set; } = FolderListView.All; 
    public override string ToString()
    {
        return $"Listview:{ListView},Search:{Keyword},Sort:{Sort},SortBy:{SortBy},{Page},{PerPage}";
    }
    [IgnoreFilter]
    public string CacheKey => FolderCacheKey.GetPaginationCacheKey($"{this}");
    [IgnoreFilter]
    public MemoryCacheEntryOptions? Options => FolderCacheKey.MemoryCacheEntryOptions;
}
    
public class FoldersWithPaginationQueryHandler :
         IRequestHandler<FoldersWithPaginationQuery, PaginatedData<FolderDto>>
{
        private readonly IApplicationDbContext _context;
        private readonly IMapper _mapper;
        private readonly IStringLocalizer<FoldersWithPaginationQueryHandler> _localizer;

        public FoldersWithPaginationQueryHandler(
            IApplicationDbContext context,
            IMapper mapper,
            IStringLocalizer<FoldersWithPaginationQueryHandler> localizer
            )
        {
            _context = context;
            _mapper = mapper;
            _localizer = localizer;
        }

        public async Task<PaginatedData<FolderDto>> Handle(FoldersWithPaginationQuery request, CancellationToken cancellationToken)
        {
           var data = await _context.Folders.ApplyFilterWithoutPagination(request)
                .ProjectTo<FolderDto>(_mapper.ConfigurationProvider)
                .PaginatedDataAsync(request.Page, request.PerPage);
            return data;
        }
}

public class FoldersPaginationSpecification : Specification<Folder>
{
    public FoldersPaginationSpecification(FoldersWithPaginationQuery query)
    {
        Criteria = q => q.Name != null;
        if (!string.IsNullOrEmpty(query.Keyword))
        {
            And(x => x.Name.Contains(query.Keyword));
        }
       
    }
}
public class SearchFoldersWithListView : FilteringOptionsBaseAttribute
{
    public override Expression BuildExpression(Expression expressionBody, PropertyInfo targetProperty, PropertyInfo filterProperty, object value)
    {
        var today = DateTime.Now.Date;
        var start = Convert.ToDateTime(today.ToString("yyyy-MM-dd",CultureInfo.CurrentCulture) + " 00:00:00", CultureInfo.CurrentCulture);
        var end = Convert.ToDateTime(today.ToString("yyyy-MM-dd",CultureInfo.CurrentCulture) + " 23:59:59", CultureInfo.CurrentCulture);
        var end30 = Convert.ToDateTime(today.AddDays(30).ToString("yyyy-MM-dd", CultureInfo.CurrentCulture) + " 23:59:59", CultureInfo.CurrentCulture);
        var listview = (FolderListView)value;
        return listview switch {
            FolderListView.All => expressionBody,
            FolderListView.CreatedToday => Expression.GreaterThanOrEqual(Expression.Property(expressionBody, "Created"), 
                                                                          Expression.Constant(start, typeof(DateTime?)))
                                            .Combine(Expression.LessThanOrEqual(Expression.Property(expressionBody, "Created"), 
                                                     Expression.Constant(end, typeof(DateTime?))), 
                                                     CombineType.And),
            FolderListView.Created30Days => Expression.GreaterThanOrEqual(Expression.Property(expressionBody, "Created"), 
                                                                          Expression.Constant(start, typeof(DateTime?)))
                                            .Combine(Expression.LessThanOrEqual(Expression.Property(expressionBody, "Created"), 
                                                     Expression.Constant(end30, typeof(DateTime?))), 
                                                     CombineType.And),
            _=> expressionBody
        };
    }
}
public enum FolderListView
{
    [Description("All")]
    All,
    [Description("Created Toady")]
    CreatedToday,
    [Description("Created within the last 30 days")]
    Created30Days
}