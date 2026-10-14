using CSM.Application.Projects.DailySiteDiaries;
using CSM.Application.Projects.DailySiteDiaries.Dtos;
using CSM.Domain.Entities.Projects;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Services;

public sealed class DailySiteDiaryService : IDailySiteDiaryService
{
    private readonly ApplicationDbContext _context;

    public DailySiteDiaryService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyCollection<DailySiteDiaryResponse>> GetAllAsync(
        Guid tenantId,
        Guid? projectId,
        Guid? siteId,
        CancellationToken cancellationToken = default)
    {
        var query = _context.DailySiteDiaries
            .AsNoTracking()
            .AsQueryable();

        if (projectId.HasValue)
        {
            query = query.Where(x => x.ProjectId == projectId.Value);
        }

        if (siteId.HasValue)
        {
            query = query.Where(x => x.SiteId == siteId.Value);
        }

        var diaries = await query
            .OrderByDescending(x => x.DiaryDate)
            .ToListAsync(cancellationToken);

        return diaries
            .Select(MapToResponse.Compile())
            .ToList();
    }

    public async Task<DailySiteDiaryResponse?> GetByIdAsync(
        Guid currentUserId,
        Guid diaryId,
        CancellationToken cancellationToken = default)
    {
        return await _context.DailySiteDiaries
            .AsNoTracking()
            .Where(x => x.Id == diaryId)
            .Select(MapToResponse)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<DailySiteDiaryResponse> CreateAsync(
        Guid currentUserId,
        CreateDailySiteDiaryRequest request,
        CancellationToken cancellationToken = default)
    {
        var diary = new DailySiteDiary
        {
            CompanyId = Guid.Empty,              // replace with current tenant later
            SiteId = request.SiteId,
            ProjectId = request.ProjectId,
            DiaryDate = request.DiaryDate,
            Weather = request.Weather,
            Temperature = request.Temperature,
            WorkforceCount = request.WorkforceCount,
            EquipmentInUse = request.EquipmentInUse,
            WorkPerformed = request.WorkPerformed,
            DelayReason = request.DelayReason,
            SafetyObservation = request.SafetyObservation
        };

        _context.DailySiteDiaries.Add(diary);
        await _context.SaveChangesAsync(cancellationToken);

        return MapToResponse.Compile()(diary);
    }

    public async Task<DailySiteDiaryResponse?> UpdateAsync(
        Guid tenantId,
        Guid diaryId,
        UpdateDailySiteDiaryRequest request,
        CancellationToken cancellationToken = default)
    {
        var diary = await _context.DailySiteDiaries
            .FirstOrDefaultAsync(x => x.Id == diaryId, cancellationToken);

        if (diary is null)
            return null;

        diary.SiteId = request.SiteId;
        diary.ProjectId = request.ProjectId;
        diary.DiaryDate = request.DiaryDate;
        diary.Weather = request.Weather;
        diary.Temperature = request.Temperature;
        diary.WorkforceCount = request.WorkforceCount;
        diary.EquipmentInUse = request.EquipmentInUse;
        diary.WorkPerformed = request.WorkPerformed;
        diary.DelayReason = request.DelayReason;
        diary.SafetyObservation = request.SafetyObservation;

        await _context.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(tenantId, diaryId, cancellationToken);
    }

   public async Task<bool> DeleteAsync(
       Guid tenantId,
       Guid diaryId,
       CancellationToken cancellationToken = default)
   {
       var rows = await _context.DailySiteDiaries
           .Where(x => x.Id == diaryId)
           .ExecuteDeleteAsync(cancellationToken);

       return rows > 0;
   }

    private static readonly System.Linq.Expressions.Expression<
        Func<DailySiteDiary, DailySiteDiaryResponse>> MapToResponse =
        diary => new DailySiteDiaryResponse
        {
            Id = diary.Id,
            TenantId = diary.CompanyId,
            SiteId = diary.SiteId,
            ProjectId = diary.ProjectId,
            DiaryDate = diary.DiaryDate,
            Weather = diary.Weather,
            Temperature = diary.Temperature,
            WorkforceCount = diary.WorkforceCount,
            EquipmentInUse = diary.EquipmentInUse,
            WorkPerformed = diary.WorkPerformed,
            DelayReason = diary.DelayReason,
            SafetyObservation = diary.SafetyObservation
        };
}