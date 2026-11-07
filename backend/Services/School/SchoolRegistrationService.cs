using KonkoorHelp.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace KonkoorHelp.Api.Services.Schools;

public sealed class SchoolRegistrationService(AppDbContext db, SchoolService schools)
{
    public async Task<object> CreateRequestAsync(
        int requesterUserId,
        string name,
        string? city,
        string? description,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("نام مدرسه لازم است.");

        var hasPending = await db.SchoolRegistrationRequests.AnyAsync(
            item => item.RequesterUserId == requesterUserId
                && item.Status == SchoolRegistrationRequestStatus.Pending,
            cancellationToken);
        if (hasPending)
            throw new InvalidOperationException("یک درخواست در انتظار تأیید داری. تا بررسی آن صبر کن.");

        var now = DateTime.UtcNow;
        var request = new SchoolRegistrationRequest
        {
            Id = Guid.NewGuid(),
            RequesterUserId = requesterUserId,
            Name = name.Trim(),
            City = city?.Trim() ?? "",
            Description = description?.Trim() ?? "",
            Status = SchoolRegistrationRequestStatus.Pending,
            Notes = "",
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.SchoolRegistrationRequests.Add(request);
        await db.SaveChangesAsync(cancellationToken);
        return Map(request);
    }

    public async Task<object> ListMineAsync(int requesterUserId, CancellationToken cancellationToken)
    {
        var items = await db.SchoolRegistrationRequests.AsNoTracking()
            .Where(item => item.RequesterUserId == requesterUserId)
            .OrderByDescending(item => item.CreatedAt)
            .Take(50)
            .ToListAsync(cancellationToken);
        return new { items = items.Select(Map).ToList() };
    }

    public async Task<object> ListPendingAsync(CancellationToken cancellationToken)
    {
        var items = await db.SchoolRegistrationRequests.AsNoTracking()
            .Where(item => item.Status == SchoolRegistrationRequestStatus.Pending)
            .OrderBy(item => item.CreatedAt)
            .Take(100)
            .ToListAsync(cancellationToken);

        var userIds = items.Select(item => item.RequesterUserId).Distinct().ToList();
        var users = await db.Users.AsNoTracking()
            .Where(item => userIds.Contains(item.Id))
            .Select(item => new { item.Id, item.Username, item.DisplayName })
            .ToDictionaryAsync(item => item.Id, cancellationToken);

        return new
        {
            items = items.Select(item =>
            {
                users.TryGetValue(item.RequesterUserId, out var user);
                return new
                {
                    id = item.Id,
                    name = item.Name,
                    city = item.City,
                    description = item.Description,
                    status = item.Status.ToString(),
                    createdAt = item.CreatedAt,
                    requesterUserId = item.RequesterUserId,
                    requesterUsername = user?.Username,
                    requesterDisplayName = user?.DisplayName,
                };
            }).ToList(),
        };
    }

    public async Task<object> ApproveAsync(Guid requestId, int reviewerUserId, CancellationToken cancellationToken)
    {
        var request = await db.SchoolRegistrationRequests
            .FirstOrDefaultAsync(item => item.Id == requestId, cancellationToken)
            ?? throw new InvalidOperationException("درخواست پیدا نشد.");
        if (request.Status != SchoolRegistrationRequestStatus.Pending)
            throw new InvalidOperationException("این درخواست دیگر در انتظار نیست.");

        var created = await schools.RegisterAsync(
            request.RequesterUserId,
            request.Name,
            request.City,
            request.Description,
            cancellationToken);

        request.Status = SchoolRegistrationRequestStatus.Approved;
        request.ReviewerUserId = reviewerUserId;
        request.ReviewedAt = DateTime.UtcNow;
        request.UpdatedAt = DateTime.UtcNow;
        request.CreatedSchoolId = created.SchoolId;
        request.CreatedWorkspaceId = created.WorkspaceId;
        await db.SaveChangesAsync(cancellationToken);

        return new
        {
            ok = true,
            request = Map(request),
            school = new
            {
                schoolId = created.SchoolId,
                workspaceId = created.WorkspaceId,
                name = created.Name,
                slug = created.Slug,
                academicYearId = created.AcademicYearId,
            },
        };
    }

    public async Task<object> RejectAsync(Guid requestId, int reviewerUserId, string? notes, CancellationToken cancellationToken)
    {
        var request = await db.SchoolRegistrationRequests
            .FirstOrDefaultAsync(item => item.Id == requestId, cancellationToken)
            ?? throw new InvalidOperationException("درخواست پیدا نشد.");
        if (request.Status != SchoolRegistrationRequestStatus.Pending)
            throw new InvalidOperationException("این درخواست دیگر در انتظار نیست.");

        request.Status = SchoolRegistrationRequestStatus.Rejected;
        request.ReviewerUserId = reviewerUserId;
        request.ReviewedAt = DateTime.UtcNow;
        request.UpdatedAt = DateTime.UtcNow;
        request.Notes = notes?.Trim() ?? "";
        await db.SaveChangesAsync(cancellationToken);
        return new { ok = true, request = Map(request) };
    }

    private static object Map(SchoolRegistrationRequest item) => new
    {
        id = item.Id,
        name = item.Name,
        city = item.City,
        description = item.Description,
        status = item.Status.ToString(),
        notes = item.Notes,
        createdAt = item.CreatedAt,
        reviewedAt = item.ReviewedAt,
        createdSchoolId = item.CreatedSchoolId,
        createdWorkspaceId = item.CreatedWorkspaceId,
    };
}
