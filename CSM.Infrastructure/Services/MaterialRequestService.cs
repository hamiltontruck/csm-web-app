using CSM.Application.Common.Exceptions;
using CSM.Application.Procurement.MaterialRequests;
using CSM.Application.Procurement.MaterialRequests.Dtos;
using CSM.Domain.Constants;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Procurement;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Services;

public sealed class MaterialRequestService : IMaterialRequestService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly NotificationHelper _notificationHelper;

    public MaterialRequestService(
        ApplicationDbContext dbContext,
        NotificationHelper notificationHelper)
    {
        _dbContext = dbContext;
        _notificationHelper = notificationHelper;
    }


    public async Task<IReadOnlyCollection<MaterialRequestResponse>> GetAllAsync(
        Guid currentUserId,
        Guid? constructionSiteId = null,
        Guid? projectId = null,
        MaterialRequestStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanView(actor);

        var query = _dbContext.MaterialRequests
            .AsNoTracking()
            .Include(x => x.ConstructionSite)
            .Include(x => x.Project)
            .Include(x => x.RequestedByUser)

            .Include(x => x.Lines)
                .ThenInclude(x => x.Material)
            .AsQueryable();

        if (!IsSuperAdmin(actor))
        {
            query = query.Where(
                x => x.CompanyId == GetActorCompanyId(actor));
        }

        if (constructionSiteId.HasValue)
        {
            query = query.Where(
                x => x.ConstructionSiteId ==
                     constructionSiteId.Value);
        }

        if (projectId.HasValue)
        {
            query = query.Where(
                x => x.ProjectId == projectId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(
                x => x.Status == status.Value);
        }

        var records = await query
            .OrderByDescending(x => x.RequestDate)
            .ThenBy(x => x.RequestNumber)
            .ToListAsync(cancellationToken);

        return records
            .Select(Map)
            .ToList();
    }

    public async Task<MaterialRequestResponse> GetByIdAsync(
        Guid currentUserId,
        Guid materialRequestId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanView(actor);

        var record = await GetMaterialRequestAsync(
            materialRequestId,
            cancellationToken);

        EnsureCompanyAccess(
            actor,
            record.CompanyId);

        return Map(record);
    }

    public async Task<MaterialRequestResponse> CreateAsync(
        Guid currentUserId,
        CreateMaterialRequestRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanManage(actor);

        ValidateHeader(
            request.RequestNumber,
            request.RequestDate,
            request.RequiredByDate);

        ValidateLines(request.Lines);

        var site = await _dbContext.ConstructionSites
            .SingleOrDefaultAsync(
                x => x.Id == request.ConstructionSiteId,
                cancellationToken)
            ?? throw new MaterialRequestManagementException(
                "Construction site was not found.");

        EnsureCompanyAccess(
            actor,
            site.CompanyId);

        await ValidateProjectAsync(
            site.CompanyId,
            site.Id,
            request.ProjectId,
            cancellationToken);

        var normalizedNumber =
            request.RequestNumber.Trim().ToUpperInvariant();

        var numberExists = await _dbContext.MaterialRequests
            .AnyAsync(
                x =>
                    x.CompanyId == site.CompanyId &&
                    x.RequestNumber.ToUpper() == normalizedNumber,
                cancellationToken);

        if (numberExists)
        {
            throw new MaterialRequestManagementException(
                "A material request with this request number already exists in the company.");
        }

        await ValidateMaterialsAsync(
            site.CompanyId,
            request.Lines,
            cancellationToken);

        var record = new MaterialRequest
        {
            CompanyId = site.CompanyId,
            ConstructionSiteId = site.Id,
            ProjectId = request.ProjectId,
            RequestNumber = normalizedNumber,
            RequestDate = request.RequestDate,
            RequiredByDate = request.RequiredByDate,
            Purpose = Clean(request.Purpose),
            Priority = request.Priority,
            Status = MaterialRequestStatus.Draft,
            RequestedBy = currentUserId
        };

        foreach (var input in request.Lines)
        {
            record.Lines.Add(
                new MaterialRequestLine
                {
                    CompanyId = site.CompanyId,
                    MaterialId = input.MaterialId,
                    RequestedQuantity =
                        input.RequestedQuantity,
                    ApprovedQuantity = 0m,
                    DeliveredQuantity = 0m,
                    Remarks = Clean(input.Remarks)
                });
        }

        _dbContext.MaterialRequests.Add(record);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        await _notificationHelper.SendAsync(
            senderUserId: currentUserId,
            recipientUserId: currentUserId,
            title: "Material Request Created",
            message:
                $"Material Request {record.RequestNumber} has been created successfully.",
            severity: NotificationSeverity.Info,
            sourceEntityType: "MaterialRequest",
            sourceEntityId: record.Id,
            ct: cancellationToken);

        return await GetResponseAsync(
            record.Id,
            cancellationToken);
    }

    public async Task<MaterialRequestResponse> UpdateAsync(
        Guid currentUserId,
        Guid materialRequestId,
        UpdateMaterialRequestRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanManage(actor);

        var record = await _dbContext.MaterialRequests
            .SingleOrDefaultAsync(
                x => x.Id == materialRequestId,
                cancellationToken)
            ?? throw new MaterialRequestManagementException(
                "Material request was not found.");

        EnsureCompanyAccess(
            actor,
            record.CompanyId);

        if (record.Status != MaterialRequestStatus.Draft)
        {
            throw new MaterialRequestManagementException(
                "Only Draft material requests can be updated.");
        }

        ValidateHeader(
            request.RequestNumber,
            request.RequestDate,
            request.RequiredByDate);

        ValidateLines(request.Lines);

        var site = await _dbContext.ConstructionSites
            .SingleOrDefaultAsync(
                x => x.Id == request.ConstructionSiteId,
                cancellationToken)
            ?? throw new MaterialRequestManagementException(
                "Construction site was not found.");

        if (site.CompanyId != record.CompanyId)
        {
            throw new MaterialRequestManagementException(
                "The selected construction site does not belong to this company.");
        }

        await ValidateProjectAsync(
            record.CompanyId,
            site.Id,
            request.ProjectId,
            cancellationToken);

        var normalizedNumber =
            request.RequestNumber.Trim().ToUpperInvariant();

        var duplicate = await _dbContext.MaterialRequests
            .AnyAsync(
                x =>
                    x.Id != record.Id &&
                    x.CompanyId == record.CompanyId &&
                    x.RequestNumber.ToUpper() == normalizedNumber,
                cancellationToken);

        if (duplicate)
        {
            throw new MaterialRequestManagementException(
                "A material request with this request number already exists in the company.");
        }

        await ValidateMaterialsAsync(
            record.CompanyId,
            request.Lines,
            cancellationToken);

        var strategy =
            _dbContext.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(
            async () =>
            {
                await using var transaction =
                    await _dbContext.Database.BeginTransactionAsync(
                        cancellationToken);

                try
                {
                    record.ConstructionSiteId =
                        site.Id;

                    record.ProjectId =
                        request.ProjectId;

                    record.RequestNumber =
                        normalizedNumber;

                    record.RequestDate =
                        request.RequestDate;

                    record.RequiredByDate =
                        request.RequiredByDate;

                    record.Purpose =
                        Clean(request.Purpose);

                    record.Priority =
                        request.Priority;

                    var existingLines =
                        await _dbContext.MaterialRequestLines
                            .IgnoreQueryFilters()
                            .Where(
                                x =>
                                    x.MaterialRequestId ==
                                    record.Id)
                            .ToListAsync(
                                cancellationToken);

                     var requestedMaterialIds =
                         request.Lines
                             .Select(x => x.MaterialId)
                             .ToHashSet();

                     //
                     // 1. Delete lines removed from the Draft.
                     //
                     var removedLines =
                         existingLines
                             .Where(
                                 x =>
                                     !requestedMaterialIds.Contains(
                                         x.MaterialId))
                             .ToList();

                     foreach (var removedLine in removedLines)
                     {
                         if (!removedLine.IsDeleted)
                         {
                             removedLine.IsDeleted =
                                 true;

                             removedLine.DeletedAtUtc =
                                 DateTime.UtcNow;

                             removedLine.DeletedBy =
                                 currentUserId;
                         }
                     }

                     //
                     // 2. Update existing lines or add genuinely new lines.
                     //
                     foreach (var input in request.Lines)
                     {
                         var existingLine =
                             existingLines.SingleOrDefault(
                                 x =>
                                     x.MaterialId ==
                                     input.MaterialId);

                         if (existingLine is not null)
                         {
                             existingLine.IsDeleted =
                                 false;

                             existingLine.DeletedAtUtc =
                                 null;

                             existingLine.DeletedBy =
                                 null;

                             existingLine.RequestedQuantity =
                                 input.RequestedQuantity;

                             existingLine.ApprovedQuantity =
                                 0m;

                             existingLine.DeliveredQuantity =
                                 0m;

                             existingLine.Remarks =
                                 Clean(input.Remarks);

                             continue;
                         }

                         _dbContext.MaterialRequestLines.Add(
                             new MaterialRequestLine
                             {
                                 CompanyId =
                                     record.CompanyId,

                                 MaterialRequestId =
                                     record.Id,

                                 MaterialId =
                                     input.MaterialId,

                                 RequestedQuantity =
                                     input.RequestedQuantity,

                                 ApprovedQuantity =
                                     0m,

                                 DeliveredQuantity =
                                     0m,

                                 Remarks =
                                     Clean(input.Remarks)
                             });
                     }

                    await _dbContext.SaveChangesAsync(
                        cancellationToken);

                    await transaction.CommitAsync(
                        cancellationToken);
                }
                catch
                {
                    await transaction.RollbackAsync(
                        cancellationToken);

                    throw;
                }
            });

        return await GetResponseAsync(
            record.Id,
            cancellationToken);
    }

    public async Task<MaterialRequestResponse> SubmitAsync(
        Guid currentUserId,
        Guid materialRequestId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanManage(actor);

        var record = await GetMaterialRequestAsync(
            materialRequestId,
            cancellationToken);

        EnsureCompanyAccess(
            actor,
            record.CompanyId);

        if (record.Status != MaterialRequestStatus.Draft)
        {
            throw new MaterialRequestManagementException(
                "Only Draft material requests can be submitted.");
        }

        if (record.Lines.Count == 0)
        {
            throw new MaterialRequestManagementException(
                "A material request must contain at least one line before submission.");
        }

        record.Status = MaterialRequestStatus.Submitted;

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Notify all Company Admins in the same company
        var companyAdmins = await _dbContext.Users
            .Include(x => x.UserRoles)
                .ThenInclude(x => x.Role)
            .Where(x =>
                x.CompanyId == record.CompanyId &&
                x.IsActive &&
                x.UserRoles.Any(r =>
                    !r.IsDeleted &&
                    !r.Role.IsDeleted &&
                    r.Role.Name == AppRoles.CompanyAdmin))
            .ToListAsync(cancellationToken);

        foreach (var admin in companyAdmins)
        {
            await _notificationHelper.SendAsync(
                senderUserId: currentUserId,
                recipientUserId: admin.Id,
                title: "Material Request Submitted",
                message:
                    $"Material Request {record.RequestNumber} is awaiting approval.",
                severity: NotificationSeverity.Info,
                sourceEntityType: "MaterialRequest",
                sourceEntityId: record.Id,
                ct: cancellationToken);
        }

        return Map(record);
    }

    public async Task<MaterialRequestResponse> ApproveAsync(
        Guid currentUserId,
        Guid materialRequestId,
        ApproveMaterialRequestRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanApprove(actor);

        var record = await GetMaterialRequestAsync(
            materialRequestId,
            cancellationToken);

        EnsureCompanyAccess(
            actor,
            record.CompanyId);

        if (record.Status != MaterialRequestStatus.Submitted)
        {
            throw new MaterialRequestManagementException(
                "Only Submitted material requests can be approved.");
        }

        if (request.Lines.Count != record.Lines.Count)
        {
            throw new MaterialRequestManagementException(
                "Approval quantities must be supplied for every material request line.");
        }

        var duplicateIds = request.Lines
            .GroupBy(x => x.MaterialRequestLineId)
            .Any(x => x.Count() > 1);

        if (duplicateIds)
        {
            throw new MaterialRequestManagementException(
                "Duplicate material request lines were supplied for approval.");
        }

        foreach (var line in record.Lines)
        {
            var approval = request.Lines
                .SingleOrDefault(
                    x => x.MaterialRequestLineId == line.Id);

            if (approval is null)
            {
                throw new MaterialRequestManagementException(
                    "Approval quantities must be supplied for every material request line.");
            }

            if (approval.ApprovedQuantity < 0m)
            {
                throw new MaterialRequestManagementException(
                    "Approved quantity cannot be negative.");
            }

            if (approval.ApprovedQuantity >
                line.RequestedQuantity)
            {
                throw new MaterialRequestManagementException(
                    "Approved quantity cannot exceed requested quantity.");
            }

            line.ApprovedQuantity =
                approval.ApprovedQuantity;
        }

        var totalApproved = record.Lines
            .Sum(x => x.ApprovedQuantity);

        if (totalApproved <= 0m)
        {
            throw new MaterialRequestManagementException(
                "At least one material request line must have an approved quantity.");
        }

        var fullyApproved = record.Lines.All(
            x => x.ApprovedQuantity ==
                 x.RequestedQuantity);

        record.Status = fullyApproved
            ? MaterialRequestStatus.Approved
            : MaterialRequestStatus.PartiallyApproved;

        record.ApprovedBy = currentUserId;
        record.ApprovedAtUtc = DateTime.UtcNow;
        record.ApprovalRemarks =
            Clean(request.Remarks);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        await _notificationHelper.SendAsync(
            senderUserId: currentUserId,
            recipientUserId: record.RequestedBy,
            title: "Material Request Approved",
            message:
                $"Your Material Request {record.RequestNumber} has been approved.",
            severity: NotificationSeverity.Info,
            sourceEntityType: "MaterialRequest",
            sourceEntityId: record.Id,
            ct: cancellationToken);

        return Map(record);
    }

    public async Task<MaterialRequestResponse> RejectAsync(
        Guid currentUserId,
        Guid materialRequestId,
        MaterialRequestRemarksRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanApprove(actor);

        var record = await GetMaterialRequestAsync(
            materialRequestId,
            cancellationToken);

        EnsureCompanyAccess(
            actor,
            record.CompanyId);

        if (record.Status != MaterialRequestStatus.Submitted)
        {
            throw new MaterialRequestManagementException(
                "Only Submitted material requests can be rejected.");
        }

        var remarks = Clean(request.Remarks);

        if (remarks is null)
        {
            throw new MaterialRequestManagementException(
                "Rejection remarks are required.");
        }

        foreach (var line in record.Lines)
        {
            line.ApprovedQuantity = 0m;
        }

        record.Status =
            MaterialRequestStatus.Rejected;

        record.ApprovedBy = currentUserId;
        record.ApprovedAtUtc = DateTime.UtcNow;
        record.ApprovalRemarks = remarks;

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return Map(record);
    }

    public async Task<MaterialRequestResponse> CancelAsync(
        Guid currentUserId,
        Guid materialRequestId,
        MaterialRequestRemarksRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanManage(actor);

        var record = await GetMaterialRequestAsync(
            materialRequestId,
            cancellationToken);

        EnsureCompanyAccess(
            actor,
            record.CompanyId);

        if (record.Status is
            MaterialRequestStatus.Ordered or
            MaterialRequestStatus.PartiallyDelivered or
            MaterialRequestStatus.Delivered)
        {
            throw new MaterialRequestManagementException(
                "A material request cannot be cancelled after ordering or delivery has started.");
        }

        if (record.Status ==
            MaterialRequestStatus.Cancelled)
        {
            throw new MaterialRequestManagementException(
                "Material request is already cancelled.");
        }

        var linkedPurchaseOrder =
            await _dbContext.PurchaseOrders.AnyAsync(
                x => x.MaterialRequestId == record.Id,
                cancellationToken);

        if (linkedPurchaseOrder)
        {
            throw new MaterialRequestManagementException(
                "A material request linked to a purchase order cannot be cancelled.");
        }

        record.Status =
            MaterialRequestStatus.Cancelled;

        record.ApprovalRemarks =
            Clean(request.Remarks);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return Map(record);
    }

    private async Task<User> GetActorAsync(
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Users
            .Include(x => x.UserRoles)
                .ThenInclude(x => x.Role)
            .SingleOrDefaultAsync(
                x => x.Id == currentUserId,
                cancellationToken)
            ?? throw new MaterialRequestManagementException(
                "Current user was not found.");
    }

    private async Task<MaterialRequest> GetMaterialRequestAsync(
        Guid materialRequestId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.MaterialRequests
            .Include(x => x.ConstructionSite)
            .Include(x => x.Project)
            .Include(x => x.RequestedByUser)
            .Include(x => x.Lines)
                .ThenInclude(x => x.Material)
            .SingleOrDefaultAsync(
                x => x.Id == materialRequestId,
                cancellationToken)
            ?? throw new MaterialRequestManagementException(
                "Material request was not found.");
    }

    private async Task<MaterialRequestResponse> GetResponseAsync(
        Guid materialRequestId,
        CancellationToken cancellationToken)
    {
        var record = await _dbContext.MaterialRequests
            .AsNoTracking()
            .Include(x => x.ConstructionSite)
            .Include(x => x.Project)
            .Include(x => x.RequestedByUser)
            .Include(x => x.Lines)
                .ThenInclude(x => x.Material)
            .SingleAsync(
                x => x.Id == materialRequestId,
                cancellationToken);

        return Map(record);
    }

    private async Task ValidateProjectAsync(
        Guid companyId,
        Guid constructionSiteId,
        Guid? projectId,
        CancellationToken cancellationToken)
    {
        if (!projectId.HasValue)
        {
            return;
        }

        var valid = await _dbContext.Projects
            .AnyAsync(
                x =>
                    x.Id == projectId.Value &&
                    x.CompanyId == companyId &&
                    x.ConstructionSiteId ==
                        constructionSiteId,
                cancellationToken);

        if (!valid)
        {
            throw new MaterialRequestManagementException(
                "The selected project does not belong to the selected construction site and company.");
        }
    }

    private async Task ValidateMaterialsAsync(
        Guid companyId,
        IReadOnlyCollection<MaterialRequestLineInput> lines,
        CancellationToken cancellationToken)
    {
        var materialIds = lines
            .Select(x => x.MaterialId)
            .Distinct()
            .ToList();

        var validMaterialIds =
            await _dbContext.Materials
                .Where(
                    x =>
                        x.CompanyId == companyId &&
                        x.IsActive &&
                        materialIds.Contains(x.Id))
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);

        if (validMaterialIds.Count != materialIds.Count)
        {
            throw new MaterialRequestManagementException(
                "One or more selected materials do not exist, are inactive, or belong to another company.");
        }
    }

    private static void ValidateHeader(
        string requestNumber,
        DateOnly requestDate,
        DateOnly? requiredByDate)
    {
        if (string.IsNullOrWhiteSpace(requestNumber))
        {
            throw new MaterialRequestManagementException(
                "Request number is required.");
        }

        if (requiredByDate.HasValue &&
            requiredByDate.Value < requestDate)
        {
            throw new MaterialRequestManagementException(
                "Required-by date cannot be before the request date.");
        }
    }

    private static void ValidateLines(
        IReadOnlyCollection<MaterialRequestLineInput> lines)
    {
        if (lines.Count == 0)
        {
            throw new MaterialRequestManagementException(
                "At least one material request line is required.");
        }

        if (lines.Any(x => x.MaterialId == Guid.Empty))
        {
            throw new MaterialRequestManagementException(
                "Material is required for every request line.");
        }

        if (lines.Any(x => x.RequestedQuantity <= 0m))
        {
            throw new MaterialRequestManagementException(
                "Requested quantity must be greater than zero.");
        }

        var duplicateMaterial =
            lines.GroupBy(x => x.MaterialId)
                .Any(x => x.Count() > 1);

        if (duplicateMaterial)
        {
            throw new MaterialRequestManagementException(
                "The same material cannot appear more than once in a material request.");
        }
    }

    private static void EnsureCanView(User actor)
    {
        if (IsSuperAdmin(actor) ||
            HasRole(actor, AppRoles.CompanyAdmin) ||
            HasRole(actor, AppRoles.Accountant) ||
            HasRole(actor, AppRoles.SiteManager))
        {
            return;
        }

        throw new MaterialRequestManagementException(
            "You are not authorized to view material requests.");
    }

    private static void EnsureCanManage(
        User actor)
    {
        if (IsSuperAdmin(actor) ||
            HasRole(actor, AppRoles.CompanyAdmin) ||
            HasRole(actor, AppRoles.SiteManager))
        {
            return;
        }

        throw new MaterialRequestManagementException(
            "You are not authorized to manage material requests.");
    }

    private static void EnsureCanApprove(
        User actor)
    {
        if (IsSuperAdmin(actor) ||
            HasRole(actor, AppRoles.CompanyAdmin))
        {
            return;
        }

        throw new MaterialRequestManagementException(
            "You are not authorized to approve material requests.");
    }

    private static void EnsureCompanyAccess(
        User actor,
        Guid companyId)
    {
        if (IsSuperAdmin(actor))
        {
            return;
        }

        if (!actor.CompanyId.HasValue ||
            actor.CompanyId.Value != companyId)
        {
            throw new MaterialRequestManagementException(
                "You cannot access material requests outside your company.");
        }
    }

    private static Guid GetActorCompanyId(
        User actor)
    {
        if (!actor.CompanyId.HasValue)
        {
            throw new MaterialRequestManagementException(
                "Current user is not assigned to a company.");
        }

        return actor.CompanyId.Value;
    }

    private static bool IsSuperAdmin(
        User actor)
    {
        return HasRole(
            actor,
            AppRoles.SuperAdmin);
    }

    private static bool HasRole(
        User actor,
        string roleName)
    {
        return actor.UserRoles.Any(
            x =>
                !x.IsDeleted &&
                !x.Role.IsDeleted &&
                string.Equals(
                    x.Role.Name,
                    roleName,
                    StringComparison.OrdinalIgnoreCase));
    }

    private static string? Clean(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

   private static MaterialRequestResponse Map(MaterialRequest record)
   {
       return new MaterialRequestResponse
       {
           Id = record.Id,
           CompanyId = record.CompanyId,

           ConstructionSiteId = record.ConstructionSiteId,
           SiteName = record.ConstructionSite?.Name ?? string.Empty,

           RequestedByName =
               record.RequestedByUser is null
                   ? string.Empty
                   : $"{record.RequestedByUser.FirstName} {record.RequestedByUser.LastName}".Trim(),

           ProjectId = record.ProjectId,
           ProjectCode = record.Project?.ProjectCode,
           ProjectName = record.Project?.Name,

           RequestNumber = record.RequestNumber,
           RequestDate = record.RequestDate,
           RequiredByDate = record.RequiredByDate,

           Purpose = record.Purpose,
           Priority = record.Priority,
           Status = record.Status,

           RequestedBy = record.RequestedBy,
           ApprovedBy = record.ApprovedBy,
           ApprovedAtUtc = record.ApprovedAtUtc,
           ApprovalRemarks = record.ApprovalRemarks,

           CreatedAtUtc = record.CreatedAtUtc,
           UpdatedAtUtc = record.UpdatedAtUtc,

           Lines = record.Lines.Select(line => new MaterialRequestLineResponse
           {
               Id = line.Id,
               MaterialId = line.MaterialId,
               MaterialCode = line.Material?.MaterialCode ?? string.Empty,
               MaterialName = line.Material?.Name ?? string.Empty,
               UnitOfMeasure = line.Material?.UnitOfMeasure ?? string.Empty,
               RequestedQuantity = line.RequestedQuantity,
               ApprovedQuantity = line.ApprovedQuantity,
               DeliveredQuantity = line.DeliveredQuantity,
               Remarks = line.Remarks
           }).ToList()
       };
   }
}

