// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CleanArchitecture.Blazor.Application.Features.Samples.DTOs;
using CleanArchitecture.Blazor.Application.Features.Samples.Caching;

namespace CleanArchitecture.Blazor.Application.Features.Samples.Queries.Pagination;

public class SamplesWithPaginationQuery : PaginationFilterBase, ICacheableRequest<PaginatedData<SampleDto>>
{
    [CompareTo("Name", "Description")] // <-- This filter will be applied to Name or Description.
    [StringFilterOptions(StringFilterOption.Contains)]
    public string? Keyword { get; set; }
    [CompareTo(typeof(SearchSamplesWithListView), "Id")]
    public SampleListView ListView { get; set; } = SampleListView.All; //<-- When the user selects a different ListView,
                                                                               // a custom query expression is executed on the filter.
    public override string ToString()
    {
        return $"Listview:{ListView},Search:{Keyword},Sort:{Sort},SortBy:{SortBy},{Page},{PerPage}";
    }
    [IgnoreFilter]
    public string CacheKey => SampleCacheKey.GetPaginationCacheKey($"{this}");
    [IgnoreFilter]
    public MemoryCacheEntryOptions? Options => SampleCacheKey.MemoryCacheEntryOptions;
}
    
public class SamplesWithPaginationQueryHandler :
         IRequestHandler<SamplesWithPaginationQuery, PaginatedData<SampleDto>>
{
        private readonly IApplicationDbContext _context;
        private readonly IMapper _mapper;
        private readonly IStringLocalizer<SamplesWithPaginationQueryHandler> _localizer;

        public SamplesWithPaginationQueryHandler(
            IApplicationDbContext context,
            IMapper mapper,
            IStringLocalizer<SamplesWithPaginationQueryHandler> localizer
            )
        {
            _context = context;
            _mapper = mapper;
            _localizer = localizer;
        }

        public async Task<PaginatedData<SampleDto>> Handle(SamplesWithPaginationQuery request, CancellationToken cancellationToken)
        {
           var data = await _context.Samples.ApplyFilterWithoutPagination(request)
                .ProjectTo<SampleDto>(_mapper.ConfigurationProvider)
                .PaginatedDataAsync(request.Page, request.PerPage);
            return data;
        }
}

public class SamplesPaginationSpecification : Specification<Sample>
{
    public SamplesPaginationSpecification(SamplesWithPaginationQuery query)
    {
        Criteria = q => q.Name != null;
        if (!string.IsNullOrEmpty(query.Keyword))
        {
            And(x => x.Name.Contains(query.Keyword));
        }
       
    }
}
public class SearchSamplesWithListView : FilteringOptionsBaseAttribute
{
    public override Expression BuildExpression(Expression expressionBody, PropertyInfo targetProperty, PropertyInfo filterProperty, object value)
    {
        var today = DateTime.Now.Date;
        var start = Convert.ToDateTime(today.ToString("yyyy-MM-dd",CultureInfo.CurrentCulture) + " 00:00:00", CultureInfo.CurrentCulture);
        var end = Convert.ToDateTime(today.ToString("yyyy-MM-dd",CultureInfo.CurrentCulture) + " 23:59:59", CultureInfo.CurrentCulture);
        var end30 = Convert.ToDateTime(today.AddDays(30).ToString("yyyy-MM-dd", CultureInfo.CurrentCulture) + " 23:59:59", CultureInfo.CurrentCulture);
        var listview = (SampleListView)value;
        return listview switch {
            SampleListView.All => expressionBody,
            SampleListView.CreatedToday => Expression.GreaterThanOrEqual(Expression.Property(expressionBody, "Created"), 
                                                                          Expression.Constant(start, typeof(DateTime?)))
                                            .Combine(Expression.LessThanOrEqual(Expression.Property(expressionBody, "Created"), 
                                                     Expression.Constant(end, typeof(DateTime?))), 
                                                     CombineType.And),
            SampleListView.Created30Days => Expression.GreaterThanOrEqual(Expression.Property(expressionBody, "Created"), 
                                                                          Expression.Constant(start, typeof(DateTime?)))
                                            .Combine(Expression.LessThanOrEqual(Expression.Property(expressionBody, "Created"), 
                                                     Expression.Constant(end30, typeof(DateTime?))), 
                                                     CombineType.And),
            _=> expressionBody
        };
    }
}
public enum SampleListView
{
    [Description("All")]
    All,
    [Description("Created Toady")]
    CreatedToday,
    [Description("Created within the last 30 days")]
    Created30Days
}