using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KonkoorHelp.Api.Data;
using KonkoorHelp.Api.Services.Gamification;
using Microsoft.EntityFrameworkCore;

namespace KonkoorHelp.Api.Services.Learning;

/// <summary>
/// Learning Journey: bootstrap from mastery subjects/topics, evidence-based unlocks,
/// achievement cosmetic hooks. Rejects XP / study-minutes / streak-only unlocks.
/// </summary>
public sealed class JourneyService(AppDbContext db, OutboxPublisher outbox)
{
    public const string BootstrapCodePrefix = "bootstrap";

    public async Task<object> GetMeAsync(Guid workspaceId, int userId, CancellationToken cancellationToken)
    {
        var journey = await EnsureBootstrapAsync(workspaceId, userId, cancellationToken);
        var progress = await EnsureProgressAsync(workspaceId, userId, journey.Id, cancellationToken);
        await EvaluateUnlocksAsync(workspaceId, userId, journey, progress, cancellationToken);

        progress = await db.LearnerJourneyProgress.AsNoTracking()
            .FirstAsync(p => p.Id == progress.Id && p.WorkspaceId == workspaceId && p.UserId == userId, cancellationToken);
        journey = await LoadJourneyGraphAsync(journey.Id, workspaceId, cancellationToken);

        var unlocked = ParseGuidSet(progress.UnlockedStageIdsJson);
        var completed = ParseGuidSet(progress.CompletedStageIdsJson);
        var evidence = ParseEvidence(progress.EvidenceJson);

        return new
        {
            journeyId = journey.Id,
            code = journey.Code,
            title = journey.Title,
            locale = journey.Locale,
            version = journey.Version,
            isBootstrap = journey.IsBootstrap,
            currentStageId = progress.CurrentStageId,
            unlockPolicy =
                "Evidence from mastery, assessments, and SRS only. XP, study minutes, and login streak alone never unlock stages.",
            regions = journey.Regions.OrderBy(r => r.SortOrder).Select(region => new
            {
                id = region.Id,
                title = region.Title,
                subjectKey = region.SubjectKey,
                visualThemeKey = region.VisualThemeKey,
                stages = region.Stages.OrderBy(s => s.SortOrder).Select(stage =>
                {
                    var state = completed.Contains(stage.Id) ? "complete"
                        : unlocked.Contains(stage.Id) ? "available"
                        : "locked";
                    return new
                    {
                        id = stage.Id,
                        title = stage.Title,
                        narrativeBlurb = stage.NarrativeBlurb,
                        sortOrder = stage.SortOrder,
                        state,
                        unlockPriorStageId = stage.UnlockPriorStageId,
                        requirements = ParseRequirements(stage.RequirementsJson),
                        rewards = ParseRewards(stage.RewardsJson),
                        evidence = evidence.Where(e => e.StageId == stage.Id).Select(e => new
                        {
                            type = e.Type,
                            refId = e.RefId,
                            detail = e.Detail,
                            at = e.At,
                        }).ToList(),
                    };
                }).ToList(),
            }).ToList(),
            summary = new
            {
                unlocked = unlocked.Count,
                completed = completed.Count,
                locked = journey.Regions.SelectMany(r => r.Stages).Count(s => !unlocked.Contains(s.Id) && !completed.Contains(s.Id)),
            },
        };
    }

    public async Task<object> EvaluateAsync(Guid workspaceId, int userId, CancellationToken cancellationToken)
    {
        var journey = await EnsureBootstrapAsync(workspaceId, userId, cancellationToken);
        var progress = await EnsureProgressAsync(workspaceId, userId, journey.Id, cancellationToken);
        var newly = await EvaluateUnlocksAsync(workspaceId, userId, journey, progress, cancellationToken);
        return new
        {
            newlyUnlockedStageIds = newly,
            map = await GetMeAsync(workspaceId, userId, cancellationToken),
        };
    }

    public async Task<JourneyDefinition> EnsureBootstrapAsync(Guid workspaceId, int userId, CancellationToken cancellationToken)
    {
        var code = $"{BootstrapCodePrefix}:{workspaceId:N}";
        var existing = await db.JourneyDefinitions
            .Include(j => j.Regions).ThenInclude(r => r.Stages)
            .FirstOrDefaultAsync(j => j.WorkspaceId == workspaceId && j.Code == code, cancellationToken);
        if (existing is not null) return existing;

        var subjects = await db.MasteryNodes.AsNoTracking()
            .Where(n => n.WorkspaceId == workspaceId && n.UserId == userId && n.Subject != "")
            .Select(n => n.Subject)
            .Distinct()
            .OrderBy(s => s)
            .ToListAsync(cancellationToken);

        if (subjects.Count == 0)
        {
            subjects = await db.SpacedRepetitionCards.AsNoTracking()
                .Where(c => c.WorkspaceId == workspaceId && c.UserId == userId && c.Subject != "")
                .Select(c => c.Subject)
                .Distinct()
                .OrderBy(s => s)
                .Take(6)
                .ToListAsync(cancellationToken);
        }

        if (subjects.Count == 0)
            subjects = ["عمومی"];

        var now = DateTime.UtcNow;
        var journeyId = DeterministicId($"journey:{code}");
        var journey = new JourneyDefinition
        {
            Id = journeyId,
            WorkspaceId = workspaceId,
            Code = code,
            Title = "سفر یادگیری من",
            Locale = "fa-IR",
            Version = 1,
            IsBootstrap = true,
            CreatedAt = now,
            UpdatedAt = now,
        };

        var regionOrder = 0;
        foreach (var subject in subjects.Take(8))
        {
            var regionId = DeterministicId($"region:{code}:{subject}");
            var region = new JourneyRegion
            {
                Id = regionId,
                JourneyId = journeyId,
                SortOrder = regionOrder++,
                Title = subject,
                SubjectKey = subject,
                VisualThemeKey = ThemeFor(subject),
            };

            var topics = await db.MasteryNodes.AsNoTracking()
                .Where(n => n.WorkspaceId == workspaceId && n.UserId == userId && n.Subject == subject && n.Topic != null && n.Topic != "")
                .Select(n => n.Topic!)
                .Distinct()
                .OrderBy(t => t)
                .Take(5)
                .ToListAsync(cancellationToken);
            if (topics.Count == 0) topics = ["شروع مسیر"];

            Guid? prior = null;
            var stageOrder = 0;
            foreach (var topic in topics)
            {
                var stageId = DeterministicId($"stage:{code}:{subject}:{topic}:{stageOrder}");
                var alwaysOpen = stageOrder == 0;
                var req = new JourneyStageRequirements
                {
                    AlwaysOpen = alwaysOpen,
                    Subject = subject,
                    Topic = topic == "شروع مسیر" ? null : topic,
                    MinMasteryLevel = alwaysOpen ? null : (int)MasteryLevel.Developing,
                    MinScoreEma = alwaysOpen ? null : 0.45,
                    MinAssessmentPercent = stageOrder >= 2 ? 70 : null,
                    MinSrsCorrectReviews = stageOrder >= 3 ? 3 : null,
                };
                var rewards = new JourneyStageRewards
                {
                    Coins = alwaysOpen ? null : 2 + stageOrder,
                    CosmeticKey = stageOrder >= 2 ? $"journey.{Sanitize(subject)}.{stageOrder}" : null,
                };
                region.Stages.Add(new JourneyStage
                {
                    Id = stageId,
                    RegionId = regionId,
                    SortOrder = stageOrder,
                    Title = topic,
                    NarrativeBlurb = alwaysOpen
                        ? "اولین ایستگاه این منطقه."
                        : "باز شدن با شواهد تسلط/آزمون/مرور — نه XP یا زمان خام.",
                    RequirementsJson = JsonSerializer.Serialize(req, JourneyJson.Options),
                    UnlockPriorStageId = prior,
                    RewardsJson = JsonSerializer.Serialize(rewards, JourneyJson.Options),
                });
                prior = stageId;
                stageOrder++;
            }

            journey.Regions.Add(region);
        }

        db.JourneyDefinitions.Add(journey);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            return await db.JourneyDefinitions
                .Include(j => j.Regions).ThenInclude(r => r.Stages)
                .FirstAsync(j => j.WorkspaceId == workspaceId && j.Code == code, cancellationToken);
        }

        return journey;
    }

    public async Task<List<Guid>> EvaluateUnlocksAsync(
        Guid workspaceId,
        int userId,
        JourneyDefinition journey,
        LearnerJourneyProgress progress,
        CancellationToken cancellationToken)
    {
        var unlocked = ParseGuidSet(progress.UnlockedStageIdsJson);
        var completed = ParseGuidSet(progress.CompletedStageIdsJson);
        var evidence = ParseEvidence(progress.EvidenceJson);
        var newly = new List<Guid>();
        var stages = journey.Regions
            .OrderBy(r => r.SortOrder)
            .SelectMany(r => r.Stages.OrderBy(s => s.SortOrder))
            .ToList();

        foreach (var stage in stages)
        {
            var req = ParseRequirements(stage.RequirementsJson);
            if (!req.AlwaysOpen || !unlocked.Add(stage.Id)) continue;
            newly.Add(stage.Id);
            completed.Add(stage.Id);
            evidence.Add(new JourneyEvidenceRef
            {
                StageId = stage.Id,
                Type = "bootstrap",
                RefId = stage.Id.ToString("N"),
                Detail = "ایستگاه آغازین مسیر",
                At = DateTime.UtcNow,
            });
        }

        var mastery = await db.MasteryNodes.AsNoTracking()
            .Where(n => n.WorkspaceId == workspaceId && n.UserId == userId)
            .ToListAsync(cancellationToken);
        var assessments = await db.AssessmentAttempts.AsNoTracking()
            .Include(a => a.Assessment)
            .Where(a => a.WorkspaceId == workspaceId && a.UserId == userId && a.Status == AttemptStatus.Submitted)
            .OrderByDescending(a => a.SubmittedAt)
            .Take(80)
            .ToListAsync(cancellationToken);
        var srs = await (
            from review in db.SpacedRepetitionReviews.AsNoTracking()
            join card in db.SpacedRepetitionCards.AsNoTracking() on review.CardId equals card.Id
            where review.WorkspaceId == workspaceId && review.UserId == userId
                && (review.Outcome == ReviewOutcome.Good || review.Outcome == ReviewOutcome.Easy)
            select new SrsHit(review.Id, card.Subject, card.Topic, review.ReviewedAt)
        ).ToListAsync(cancellationToken);

        var changed = newly.Count > 0;
        var guard = 0;
        while (guard++ < 32)
        {
            var progressed = false;
            foreach (var stage in stages)
            {
                if (unlocked.Contains(stage.Id)) continue;
                if (stage.UnlockPriorStageId is Guid prior && !unlocked.Contains(prior) && !completed.Contains(prior))
                    continue;
                if (ContainsRejectedSignal(stage.RequirementsJson)) continue;

                var req = ParseRequirements(stage.RequirementsJson);
                if (!TryEvaluate(stage, req, mastery, assessments, srs, out var matched)) continue;

                unlocked.Add(stage.Id);
                completed.Add(stage.Id);
                newly.Add(stage.Id);
                evidence.AddRange(matched);
                changed = true;
                progressed = true;

                var rewards = ParseRewards(stage.RewardsJson);
                await ApplyCosmeticHookAsync(workspaceId, userId, stage, rewards, cancellationToken);
                await outbox.PublishAsync(
                    workspaceId,
                    userId,
                    DomainEventTypes.JourneyStageUnlocked,
                    $"journey-stage:{stage.Id:N}:{userId}",
                    new
                    {
                        journeyId = journey.Id,
                        stageId = stage.Id,
                        title = stage.Title,
                        cosmeticKey = rewards.CosmeticKey,
                        evidence = matched.Select(m => new { m.Type, m.RefId, m.Detail }),
                    },
                    cancellationToken);
            }

            if (!progressed) break;
        }

        if (changed)
        {
            progress.UnlockedStageIdsJson = JsonSerializer.Serialize(unlocked.ToList(), JourneyJson.Options);
            progress.CompletedStageIdsJson = JsonSerializer.Serialize(completed.ToList(), JourneyJson.Options);
            progress.EvidenceJson = JsonSerializer.Serialize(evidence.TakeLast(200).ToList(), JourneyJson.Options);
            progress.CurrentStageId = stages
                .Where(s => unlocked.Contains(s.Id) && !completed.Contains(s.Id))
                .Select(s => (Guid?)s.Id)
                .FirstOrDefault()
                ?? stages.LastOrDefault(s => completed.Contains(s.Id))?.Id;
            progress.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }

        return newly.Distinct().ToList();
    }

    private async Task<LearnerJourneyProgress> EnsureProgressAsync(
        Guid workspaceId, int userId, Guid journeyId, CancellationToken cancellationToken)
    {
        var row = await db.LearnerJourneyProgress.FirstOrDefaultAsync(
            p => p.WorkspaceId == workspaceId && p.UserId == userId && p.JourneyId == journeyId,
            cancellationToken);
        if (row is not null) return row;

        row = new LearnerJourneyProgress
        {
            Id = DeterministicId($"progress:{workspaceId:N}:{userId}:{journeyId:N}"),
            WorkspaceId = workspaceId,
            UserId = userId,
            JourneyId = journeyId,
            UnlockedStageIdsJson = "[]",
            CompletedStageIdsJson = "[]",
            EvidenceJson = "[]",
            UpdatedAt = DateTime.UtcNow,
        };
        db.LearnerJourneyProgress.Add(row);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            return await db.LearnerJourneyProgress.FirstAsync(
                p => p.WorkspaceId == workspaceId && p.UserId == userId && p.JourneyId == journeyId,
                cancellationToken);
        }

        return row;
    }

    private async Task<JourneyDefinition> LoadJourneyGraphAsync(
        Guid journeyId,
        Guid workspaceId,
        CancellationToken cancellationToken) =>
        await db.JourneyDefinitions.AsNoTracking()
            .Include(j => j.Regions).ThenInclude(r => r.Stages)
            .FirstAsync(
                j => j.Id == journeyId && (j.WorkspaceId == null || j.WorkspaceId == workspaceId),
                cancellationToken);

    private async Task ApplyCosmeticHookAsync(
        Guid workspaceId,
        int userId,
        JourneyStage stage,
        JourneyStageRewards rewards,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rewards.CosmeticKey) && (rewards.Coins is null or <= 0))
            return;

        db.GameNotifications.Add(new GameNotification
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            UserId = userId,
            Kind = GameNotificationKind.QuestCompleted,
            Title = "ایستگاه سفر باز شد",
            Body = stage.Title,
            PayloadJson = JsonSerializer.Serialize(new
            {
                stageId = stage.Id,
                cosmeticKey = rewards.CosmeticKey,
                coins = rewards.Coins,
                achievementLinked = true,
            }, JourneyJson.Options),
            CreatedAt = DateTime.UtcNow,
        });
        await Task.CompletedTask;
    }

    private static bool TryEvaluate(
        JourneyStage stage,
        JourneyStageRequirements req,
        List<MasteryNode> mastery,
        List<AssessmentAttempt> assessments,
        List<SrsHit> srs,
        out List<JourneyEvidenceRef> matched)
    {
        matched = [];
        if (req.AlwaysOpen) return true;

        var subject = req.Subject ?? "";
        var topic = req.Topic;
        var nodes = mastery.Where(n =>
                (string.IsNullOrEmpty(subject) || string.Equals(n.Subject, subject, StringComparison.OrdinalIgnoreCase))
                && (string.IsNullOrEmpty(topic) || string.Equals(n.Topic, topic, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        var masteryOk = req.MinMasteryLevel is null;
        if (req.MinMasteryLevel is int minLevel)
        {
            var node = nodes
                .Where(n => (int)n.Level >= minLevel && (req.MinScoreEma is null || n.ScoreEma >= req.MinScoreEma))
                .OrderByDescending(n => n.Level).ThenByDescending(n => n.ScoreEma)
                .FirstOrDefault();
            masteryOk = node is not null;
            if (node is not null)
            {
                matched.Add(new JourneyEvidenceRef
                {
                    StageId = stage.Id,
                    Type = "MasteryNode",
                    RefId = node.Id.ToString("N"),
                    Detail = $"تسلط {node.Subject}/{(node.Topic ?? "—")} سطح {node.Level} (EMA {node.ScoreEma:0.00})",
                    At = node.LastEvidenceAt ?? node.UpdatedAt,
                });
            }
        }

        var assessmentOk = req.MinAssessmentPercent is null;
        if (req.MinAssessmentPercent is double minPct)
        {
            var hit = assessments.FirstOrDefault(a =>
                a.Percent >= minPct
                && (string.IsNullOrEmpty(subject)
                    || string.Equals(a.Assessment.Subject, subject, StringComparison.OrdinalIgnoreCase)));
            assessmentOk = hit is not null;
            if (hit is not null)
            {
                matched.Add(new JourneyEvidenceRef
                {
                    StageId = stage.Id,
                    Type = "AssessmentAttempt",
                    RefId = hit.Id.ToString("N"),
                    Detail = $"آزمون «{hit.Assessment.Title}» با {hit.Percent:0.#}%",
                    At = hit.SubmittedAt ?? hit.StartedAt,
                });
            }
        }

        var srsOk = req.MinSrsCorrectReviews is null;
        if (req.MinSrsCorrectReviews is int minReviews)
        {
            var scoped = srs.Where(r =>
                    (string.IsNullOrEmpty(subject) || string.Equals(r.Subject, subject, StringComparison.OrdinalIgnoreCase))
                    && (string.IsNullOrEmpty(topic) || string.Equals(r.Topic, topic, StringComparison.OrdinalIgnoreCase)))
                .Take(minReviews)
                .ToList();
            srsOk = scoped.Count >= minReviews;
            foreach (var row in scoped)
            {
                matched.Add(new JourneyEvidenceRef
                {
                    StageId = stage.Id,
                    Type = "SpacedRepetitionReview",
                    RefId = row.Id.ToString("N"),
                    Detail = $"مرور موفق {row.Subject}/{row.Topic}",
                    At = row.ReviewedAt,
                });
            }
        }

        var anyConfigured = req.MinMasteryLevel is not null
            || req.MinAssessmentPercent is not null
            || req.MinSrsCorrectReviews is not null;
        if (!anyConfigured) return false;
        return (req.MinMasteryLevel is null || masteryOk)
            && (req.MinAssessmentPercent is null || assessmentOk)
            && (req.MinSrsCorrectReviews is null || srsOk)
            && matched.Count > 0;
    }

    /// <summary>
    /// Blocks requirement payloads that rely only on XP / time / streak.
    /// Qualifying evidence keys (mastery / assessment / SRS) must still be evaluated by TryEvaluate.
    /// </summary>
    private static bool ContainsRejectedSignal(string requirementsJson)
    {
        var raw = requirementsJson.ToLowerInvariant();
        var hasRejected = raw.Contains("\"xp\"")
            || raw.Contains("study_minutes")
            || raw.Contains("login_streak")
            || raw.Contains("elapsed_time")
            || raw.Contains("\"streak\"");
        if (!hasRejected) return false;

        var hasQualifying = raw.Contains("minmasterylevel")
            || raw.Contains("minscoreema")
            || raw.Contains("minassessmentpercent")
            || raw.Contains("minsrscorrectreviews")
            || raw.Contains("alwaysopen");
        return !hasQualifying;
    }

    private static JourneyStageRequirements ParseRequirements(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<JourneyStageRequirements>(json, JourneyJson.Options) ?? new();
        }
        catch (JsonException)
        {
            return new();
        }
    }

    private static JourneyStageRewards ParseRewards(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<JourneyStageRewards>(json, JourneyJson.Options) ?? new();
        }
        catch (JsonException)
        {
            return new();
        }
    }

    private static HashSet<Guid> ParseGuidSet(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<Guid>>(json, JourneyJson.Options)?.ToHashSet() ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static List<JourneyEvidenceRef> ParseEvidence(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<JourneyEvidenceRef>>(json, JourneyJson.Options) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static Guid DeterministicId(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        Span<byte> bytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(bytes);
        bytes[7] = (byte)((bytes[7] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes);
    }

    private static string ThemeFor(string subject) =>
        subject.GetHashCode(StringComparison.Ordinal) switch
        {
            var h when Math.Abs(h) % 3 == 0 => "ember",
            var h when Math.Abs(h) % 3 == 1 => "cedar",
            _ => "ink",
        };

    private static string Sanitize(string value)
    {
        var chars = value.Where(char.IsLetterOrDigit).Take(24).ToArray();
        return chars.Length == 0 ? "topic" : new string(chars).ToLowerInvariant();
    }

    private readonly record struct SrsHit(Guid Id, string Subject, string Topic, DateTime ReviewedAt);
}
