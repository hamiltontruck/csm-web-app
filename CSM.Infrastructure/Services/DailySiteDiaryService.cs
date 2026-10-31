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
        Guid currentUserId,
        Guid? projectId,
        Guid? siteId,
        CancellationToken cancellationToken = default)
    {
        var access = await ProjectChildAccess.ForAsync(_context, currentUserId, false, true, cancellationToken);
        var query = access.Diaries
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
        var access = await ProjectChildAccess.ForAsync(_context, currentUserId, false, true, cancellationToken);
        return await access.Diaries
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
        var access = await ProjectChildAccess.ForAsync(_context, currentUserId, true, true, cancellationToken);
        var company = await access.DiaryParentAsync(request.SiteId, request.ProjectId, cancellationToken);
        var diary = new DailySiteDiary
        {
            CompanyId = company,
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
        Guid currentUserId,
        Guid diaryId,
        UpdateDailySiteDiaryRequest request,
        CancellationToken cancellationToken = default)
    {
        var access = await ProjectChildAccess.ForAsync(_context, currentUserId, true, true, cancellationToken);
        var diary = await access.Diaries
            .FirstOrDefaultAsync(x => x.Id == diaryId, cancellationToken);

        if (diary is null)
            return null;

        var company = await access.DiaryParentAsync(request.SiteId, request.ProjectId, cancellationToken);
        if (company != diary.CompanyId) throw new ArgumentException("Diary company cannot change.");
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

        return await GetByIdAsync(currentUserId, diaryId, cancellationToken);
    }

   public async Task<bool> DeleteAsync(
       Guid currentUserId,
       Guid diaryId,
       CancellationToken cancellationToken = default)
   {
       var access = await ProjectChildAccess.ForAsync(_context, currentUserId, true, true, cancellationToken);
       var diary = await access.Diaries.SingleOrDefaultAsync(x => x.Id == diaryId, cancellationToken);
       if (diary is null) return false;
       _context.DailySiteDiaries.Remove(diary);
       await _context.SaveChangesAsync(cancellationToken);
       return true;
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