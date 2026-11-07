using KonkoorHelp.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace KonkoorHelp.Api.Services.Schools;

public sealed class SchoolService(AppDbContext db, SchoolAccess access)
{
    public async Task<SchoolRegistrationResult> RegisterAsync(int ownerUserId, string name, string? city, string? description, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("نام مدرسه لازم است.");

        var now = DateTime.UtcNow;
        var schoolId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var slug = MakeSlug(name);

        var school = new School
        {
            Id = schoolId,
            Name = name.Trim(),
            Slug = slug,
            Description = description?.Trim() ?? "",
            City = city?.Trim() ?? "",
            Address = "",
            Phone = "",
            OwnerUserId = ownerUserId,
            WorkspaceId = workspaceId,
            CreatedAt = now,
            UpdatedAt = now,
        };

        var workspace = new Workspace
        {
            Id = workspaceId,
            Type = WorkspaceType.School,
            Name = school.Name,
            Slug = slug,
            SchoolId = schoolId,
            OwnerUserId = ownerUserId,
            Status = WorkspaceStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
        };

        db.Schools.Add(school);
        db.Workspaces.Add(workspace);
        db.WorkspaceMembers.Add(new WorkspaceMember
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            UserId = ownerUserId,
            Role = WorkspaceMemberRole.SchoolOwner,
            Status = WorkspaceMemberStatus.Active,
            JoinedAt = now,
            CreatedAt = now,
            UpdatedAt = now,
        });
        db.WorkspaceSettings.Add(new WorkspaceSettings
        {
            WorkspaceId = workspaceId,
            Data = "{}",
            LocaleDefault = "fa-IR",
            TimeZone = "Asia/Tehran",
            FeatureFlags = """{"gamification":true,"school":true}""",
            UpdatedAt = now,
        });
        db.WorkspaceSubscriptions.Add(new WorkspaceSubscription
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            PlanCode = "school-starter",
            Status = SubscriptionStatus.Trial,
            SeatLimit = 200,
            StartsAt = now,
            CreatedAt = now,
            UpdatedAt = now,
        });
        db.SchoolSettingsExtras.Add(new SchoolSettingsExtras
        {
            WorkspaceId = workspaceId,
            LeaderboardsVisible = true,
            AllowTeacherBadges = true,
            AllowParentReadOnly = true,
            AnalyticsDefaultsJson = "{}",
            UpdatedAt = now,
        });

        var year = new AcademicYear
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            Name = $"{now.Year}-{now.Year + 1}",
            StartsOn = new DateOnly(now.Year, 9, 1),
            EndsOn = new DateOnly(now.Year + 1, 6, 30),
            IsCurrent = true,
            CreatedAt = now,
        };
        db.AcademicYears.Add(year);
        await db.SaveChangesAsync(cancellationToken);

        return new SchoolRegistrationResult(school.Id, workspaceId, school.Name, school.Slug ?? "", year.Id);
    }

    public async Task<object> GetProfileAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        await access.EnsureSchoolWorkspaceAsync(workspaceId, cancellationToken);
        var school = await db.Schools.AsNoTracking().FirstOrDefaultAsync(item => item.WorkspaceId == workspaceId && item.DeletedAt == null, cancellationToken)
            ?? throw new InvalidOperationException("مدرسه پیدا نشد.");
        var extras = await db.SchoolSettingsExtras.AsNoTracking().FirstOrDefaultAsync(item => item.WorkspaceId == workspaceId, cancellationToken);
        return new
        {
            id = school.Id,
            workspaceId = school.WorkspaceId,
            name = school.Name,
            slug = school.Slug,
            description = school.Description,
            city = school.City,
            address = school.Address,
            phone = school.Phone,
            leaderboardsVisible = extras?.LeaderboardsVisible ?? true,
            allowTeacherBadges = extras?.AllowTeacherBadges ?? true,
            allowParentReadOnly = extras?.AllowParentReadOnly ?? true,
        };
    }

    public async Task<object> UpdateProfileAsync(
        Guid workspaceId,
        string? name,
        string? description,
        string? city,
        string? address,
        string? phone,
        bool? leaderboardsVisible,
        bool? allowTeacherBadges,
        CancellationToken cancellationToken)
    {
        await access.EnsureSchoolWorkspaceAsync(workspaceId, cancellationToken);
        var school = await db.Schools.FirstOrDefaultAsync(item => item.WorkspaceId == workspaceId && item.DeletedAt == null, cancellationToken)
            ?? throw new InvalidOperationException("مدرسه پیدا نشد.");
        if (!string.IsNullOrWhiteSpace(name))
        {
            school.Name = name.Trim();
            var ws = await db.Workspaces.FirstAsync(item => item.Id == workspaceId, cancellationToken);
            ws.Name = school.Name;
            ws.UpdatedAt = DateTime.UtcNow;
        }

        if (description is not null) school.Description = description.Trim();
        if (city is not null) school.City = city.Trim();
        if (address is not null) school.Address = address.Trim();
        if (phone is not null) school.Phone = phone.Trim();
        school.UpdatedAt = DateTime.UtcNow;

        var extras = await db.SchoolSettingsExtras.FirstOrDefaultAsync(item => item.WorkspaceId == workspaceId, cancellationToken);
        if (extras is null)
        {
            extras = new SchoolSettingsExtras { WorkspaceId = workspaceId, AnalyticsDefaultsJson = "{}", UpdatedAt = DateTime.UtcNow };
            db.SchoolSettingsExtras.Add(extras);
        }

        if (leaderboardsVisible is bool lv) extras.LeaderboardsVisible = lv;
        if (allowTeacherBadges is bool ab) extras.AllowTeacherBadges = ab;
        extras.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return await GetProfileAsync(workspaceId, cancellationToken);
    }

    public async Task<object> ListMembersAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        await access.EnsureSchoolWorkspaceAsync(workspaceId, cancellationToken);
        var items = await db.WorkspaceMembers.AsNoTracking()
            .Where(item => item.WorkspaceId == workspaceId && item.DeletedAt == null && item.Status == WorkspaceMemberStatus.Active)
            .Join(db.Users.AsNoTracking(), m => m.UserId, u => u.Id, (m, u) => new
            {
                userId = m.UserId,
                displayName = u.DisplayName ?? u.Username,
                role = m.Role.ToString(),
                joinedAt = m.JoinedAt,
            })
            .OrderBy(item => item.role)
            .ThenBy(item => item.displayName)
            .ToListAsync(cancellationToken);
        return new { items };
    }

    public async Task<object> InviteMemberAsync(
        Guid workspaceId,
        int actorUserId,
        int userId,
        WorkspaceMemberRole role,
        CancellationToken cancellationToken)
    {
        await access.EnsureSchoolWorkspaceAsync(workspaceId, cancellationToken);
        if (!await db.Users.AnyAsync(item => item.Id == userId && item.DeletedAt == null, cancellationToken))
            throw new InvalidOperationException("کاربر پیدا نشد.");

        if (role == WorkspaceMemberRole.SchoolOwner)
            throw new InvalidOperationException("نقش مالک مدرسه فقط از طریق انتقال مالکیت قابل تنظیم است.");

        var existing = await db.WorkspaceMembers.FirstOrDefaultAsync(
            item => item.WorkspaceId == workspaceId && item.UserId == userId && item.DeletedAt == null,
            cancellationToken);
        var now = DateTime.UtcNow;
        if (existing is null)
        {
            db.WorkspaceMembers.Add(new WorkspaceMember
            {
                Id = Guid.NewGuid(),
                WorkspaceId = workspaceId,
                UserId = userId,
                Role = role,
                Status = WorkspaceMemberStatus.Active,
                InvitedByUserId = actorUserId,
                JoinedAt = now,
                CreatedAt = now,
                UpdatedAt = now,
            });
        }
        else
        {
            if (existing.Role == WorkspaceMemberRole.SchoolOwner && role != WorkspaceMemberRole.SchoolOwner)
            {
                var owners = await db.WorkspaceMembers.CountAsync(
                    item => item.WorkspaceId == workspaceId
                        && item.DeletedAt == null
                        && item.Status == WorkspaceMemberStatus.Active
                        && item.Role == WorkspaceMemberRole.SchoolOwner,
                    cancellationToken);
                if (owners <= 1)
                    throw new InvalidOperationException("نمی‌توان آخرین مالک مدرسه را تنزل داد.");
            }

            existing.Role = role;
            existing.Status = WorkspaceMemberStatus.Active;
            existing.UpdatedAt = now;
            existing.InvitedByUserId = actorUserId;
        }

        await db.SaveChangesAsync(cancellationToken);
        return new { ok = true, userId, role = role.ToString() };
    }

    public async Task<object> CreateAcademicYearAsync(Guid workspaceId, string name, DateOnly startsOn, DateOnly endsOn, bool isCurrent, CancellationToken cancellationToken)
    {
        await access.EnsureSchoolWorkspaceAsync(workspaceId, cancellationToken);
        if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("نام سال تحصیلی لازم است.");
        if (endsOn <= startsOn) throw new InvalidOperationException("بازه سال تحصیلی نامعتبر است.");

        if (isCurrent)
        {
            var currents = await db.AcademicYears.Where(item => item.WorkspaceId == workspaceId && item.IsCurrent).ToListAsync(cancellationToken);
            foreach (var row in currents) row.IsCurrent = false;
        }

        var year = new AcademicYear
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            Name = name.Trim(),
            StartsOn = startsOn,
            EndsOn = endsOn,
            IsCurrent = isCurrent,
            CreatedAt = DateTime.UtcNow,
        };
        db.AcademicYears.Add(year);
        await db.SaveChangesAsync(cancellationToken);
        return new { id = year.Id, name = year.Name, isCurrent = year.IsCurrent };
    }

    public async Task<object> ListAcademicYearsAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        await access.EnsureSchoolWorkspaceAsync(workspaceId, cancellationToken);
        var items = await db.AcademicYears.AsNoTracking()
            .Where(item => item.WorkspaceId == workspaceId)
            .OrderByDescending(item => item.StartsOn)
            .Select(item => new { id = item.Id, name = item.Name, startsOn = item.StartsOn, endsOn = item.EndsOn, isCurrent = item.IsCurrent })
            .ToListAsync(cancellationToken);
        return new { items };
    }

    public async Task<object> CreateGradeAsync(Guid workspaceId, string name, int sortOrder, CancellationToken cancellationToken)
    {
        await access.EnsureSchoolWorkspaceAsync(workspaceId, cancellationToken);
        if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("نام پایه لازم است.");
        var grade = new GradeLevel
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            Name = name.Trim(),
            SortOrder = sortOrder,
            CreatedAt = DateTime.UtcNow,
        };
        db.GradeLevels.Add(grade);
        await db.SaveChangesAsync(cancellationToken);
        return new { id = grade.Id, name = grade.Name, sortOrder = grade.SortOrder };
    }

    public async Task<object> ListGradesAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        await access.EnsureSchoolWorkspaceAsync(workspaceId, cancellationToken);
        var items = await db.GradeLevels.AsNoTracking()
            .Where(item => item.WorkspaceId == workspaceId)
            .OrderBy(item => item.SortOrder)
            .Select(item => new { id = item.Id, name = item.Name, sortOrder = item.SortOrder })
            .ToListAsync(cancellationToken);
        return new { items };
    }

    public async Task<object> CreateSubjectAsync(Guid workspaceId, string name, string? code, CancellationToken cancellationToken)
    {
        await access.EnsureSchoolWorkspaceAsync(workspaceId, cancellationToken);
        if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("نام درس لازم است.");
        var subject = new SchoolSubject
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            Name = name.Trim(),
            Code = string.IsNullOrWhiteSpace(code) ? name.Trim()[..Math.Min(8, name.Trim().Length)] : code.Trim(),
            Active = true,
            CreatedAt = DateTime.UtcNow,
        };
        db.SchoolSubjects.Add(subject);
        await db.SaveChangesAsync(cancellationToken);
        return new { id = subject.Id, name = subject.Name, code = subject.Code };
    }

    public async Task<object> ListSubjectsAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        await access.EnsureSchoolWorkspaceAsync(workspaceId, cancellationToken);
        var items = await db.SchoolSubjects.AsNoTracking()
            .Where(item => item.WorkspaceId == workspaceId && item.Active)
            .OrderBy(item => item.Name)
            .Select(item => new { id = item.Id, name = item.Name, code = item.Code })
            .ToListAsync(cancellationToken);
        return new { items };
    }

    public async Task<object> CreateClassAsync(
        Guid workspaceId,
        int actorUserId,
        string name,
        Guid? academicYearId,
        Guid? gradeId,
        CancellationToken cancellationToken)
    {
        await access.EnsureSchoolWorkspaceAsync(workspaceId, cancellationToken);
        if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("نام کلاس لازم است.");
        if (academicYearId is Guid yid && !await db.AcademicYears.AnyAsync(item => item.Id == yid && item.WorkspaceId == workspaceId, cancellationToken))
            throw new InvalidOperationException("سال تحصیلی نامعتبر است.");
        if (gradeId is Guid gid && !await db.GradeLevels.AnyAsync(item => item.Id == gid && item.WorkspaceId == workspaceId, cancellationToken))
            throw new InvalidOperationException("پایه نامعتبر است.");

        var group = new ClassGroup
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            AcademicYearId = academicYearId,
            GradeId = gradeId,
            Name = name.Trim(),
            CreatedByUserId = actorUserId,
            CreatedAt = DateTime.UtcNow,
        };
        db.Classes.Add(group);
        db.ClassMembers.Add(new ClassMember
        {
            Id = Guid.NewGuid(),
            ClassId = group.Id,
            UserId = actorUserId,
            Role = ClassMemberRole.Teacher,
            JoinedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(cancellationToken);
        return new { id = group.Id, name = group.Name, academicYearId, gradeId };
    }

    public async Task<object> ListClassesAsync(Guid workspaceId, Guid? academicYearId, Guid? gradeId, CancellationToken cancellationToken)
    {
        await access.EnsureSchoolWorkspaceAsync(workspaceId, cancellationToken);
        var query = db.Classes.AsNoTracking().Where(item => item.WorkspaceId == workspaceId && item.DeletedAt == null);
        if (academicYearId is Guid y) query = query.Where(item => item.AcademicYearId == y);
        if (gradeId is Guid g) query = query.Where(item => item.GradeId == g);
        var items = await query.OrderBy(item => item.Name)
            .Select(item => new
            {
                id = item.Id,
                name = item.Name,
                academicYearId = item.AcademicYearId,
                gradeId = item.GradeId,
                memberCount = item.Members.Count,
                createdAt = item.CreatedAt,
            })
            .ToListAsync(cancellationToken);
        return new { items };
    }

    private static string MakeSlug(string name)
    {
        var raw = new string(name.Trim().ToLowerInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray());
        while (raw.Contains("--", StringComparison.Ordinal)) raw = raw.Replace("--", "-", StringComparison.Ordinal);
        raw = raw.Trim('-');
        if (string.IsNullOrWhiteSpace(raw)) raw = "school";
        return $"{raw}-{Guid.NewGuid().ToString("N")[..6]}";
    }
}

public sealed record SchoolRegistrationResult(
    Guid SchoolId,
    Guid WorkspaceId,
    string Name,
    string Slug,
    Guid AcademicYearId);
