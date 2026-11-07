using Microsoft.EntityFrameworkCore;

namespace KonkoorHelp.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<UserAccount> Users => Set<UserAccount>();
    public DbSet<AuthSession> AuthSessions => Set<AuthSession>();
    public DbSet<UserSettings> Settings => Set<UserSettings>();
    public DbSet<StudyRecord> StudyRecords => Set<StudyRecord>();
    public DbSet<NoteDocument> Notes => Set<NoteDocument>();
    public DbSet<FolderDocument> Folders => Set<FolderDocument>();
    public DbSet<TrashDocument> Trash => Set<TrashDocument>();
    public DbSet<NoteImage> NoteImages => Set<NoteImage>();
    public DbSet<NoteSetting> NoteSettings => Set<NoteSetting>();
    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<WorkspaceMember> WorkspaceMembers => Set<WorkspaceMember>();
    public DbSet<WorkspaceSettings> WorkspaceSettings => Set<WorkspaceSettings>();
    public DbSet<WorkspaceSubscription> WorkspaceSubscriptions => Set<WorkspaceSubscription>();
    public DbSet<GamificationProfile> GamificationProfiles => Set<GamificationProfile>();
    public DbSet<DomainOutbox> DomainOutbox => Set<DomainOutbox>();
    public DbSet<ProcessedEvent> ProcessedEvents => Set<ProcessedEvent>();
    public DbSet<XpLedger> XpLedger => Set<XpLedger>();
    public DbSet<CoinLedger> CoinLedger => Set<CoinLedger>();
    public DbSet<UserGameStats> UserGameStats => Set<UserGameStats>();
    public DbSet<BadgeDefinition> Badges => Set<BadgeDefinition>();
    public DbSet<UserBadge> UserBadges => Set<UserBadge>();
    public DbSet<QuestDefinition> Quests => Set<QuestDefinition>();
    public DbSet<UserQuestProgress> UserQuestProgress => Set<UserQuestProgress>();
    public DbSet<StreakDay> StreakDays => Set<StreakDay>();
    public DbSet<GameNotification> GameNotifications => Set<GameNotification>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<UserPrivacySettings> UserPrivacySettings => Set<UserPrivacySettings>();
    public DbSet<LeaderboardDefinition> LeaderboardDefinitions => Set<LeaderboardDefinition>();
    public DbSet<LeaderboardSnapshot> LeaderboardSnapshots => Set<LeaderboardSnapshot>();
    public DbSet<ClassGroup> Classes => Set<ClassGroup>();
    public DbSet<ClassMember> ClassMembers => Set<ClassMember>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<TeamMember> TeamMembers => Set<TeamMember>();
    public DbSet<TeamInvite> TeamInvites => Set<TeamInvite>();
    public DbSet<TeamChallenge> TeamChallenges => Set<TeamChallenge>();
    public DbSet<TeamAchievement> TeamAchievements => Set<TeamAchievement>();
    public DbSet<Competition> Competitions => Set<Competition>();
    public DbSet<CompetitionParticipant> CompetitionParticipants => Set<CompetitionParticipant>();
    public DbSet<CompetitionTeam> CompetitionTeams => Set<CompetitionTeam>();
    public DbSet<StoreItem> StoreItems => Set<StoreItem>();
    public DbSet<UserInventoryItem> UserInventory => Set<UserInventoryItem>();
    public DbSet<StorePurchase> StorePurchases => Set<StorePurchase>();
    public DbSet<UserAvatarLoadout> UserAvatarLoadouts => Set<UserAvatarLoadout>();
    public DbSet<RewardGrant> RewardGrants => Set<RewardGrant>();
    public DbSet<AssessmentDefinition> Assessments => Set<AssessmentDefinition>();
    public DbSet<AssessmentQuestion> AssessmentQuestions => Set<AssessmentQuestion>();
    public DbSet<AssessmentAttempt> AssessmentAttempts => Set<AssessmentAttempt>();
    public DbSet<AssessmentAnswer> AssessmentAnswers => Set<AssessmentAnswer>();
    public DbSet<MasteryNode> MasteryNodes => Set<MasteryNode>();
    public DbSet<SpacedRepetitionCard> SpacedRepetitionCards => Set<SpacedRepetitionCard>();
    public DbSet<SpacedRepetitionReview> SpacedRepetitionReviews => Set<SpacedRepetitionReview>();
    public DbSet<SpacedRepetitionProfile> SpacedRepetitionProfiles => Set<SpacedRepetitionProfile>();
    public DbSet<AiWorkspaceSettings> AiWorkspaceSettings => Set<AiWorkspaceSettings>();
    public DbSet<AiRecommendation> AiRecommendations => Set<AiRecommendation>();
    public DbSet<AiWeeklyReport> AiWeeklyReports => Set<AiWeeklyReport>();
    public DbSet<AiPlanSuggestion> AiPlanSuggestions => Set<AiPlanSuggestion>();
    public DbSet<School> Schools => Set<School>();
    public DbSet<AcademicYear> AcademicYears => Set<AcademicYear>();
    public DbSet<GradeLevel> GradeLevels => Set<GradeLevel>();
    public DbSet<SchoolSubject> SchoolSubjects => Set<SchoolSubject>();
    public DbSet<GuardianStudentLink> GuardianStudentLinks => Set<GuardianStudentLink>();
    public DbSet<SchoolAssignment> SchoolAssignments => Set<SchoolAssignment>();
    public DbSet<SchoolAssignmentSubmission> SchoolAssignmentSubmissions => Set<SchoolAssignmentSubmission>();
    public DbSet<TeacherFeedback> TeacherFeedback => Set<TeacherFeedback>();
    public DbSet<SchoolSettingsExtras> SchoolSettingsExtras => Set<SchoolSettingsExtras>();
    public DbSet<ApiClient> ApiClients => Set<ApiClient>();
    public DbSet<ApiClientWorkspace> ApiClientWorkspaces => Set<ApiClientWorkspace>();
    public DbSet<WebhookEndpoint> WebhookEndpoints => Set<WebhookEndpoint>();
    public DbSet<WebhookDelivery> WebhookDeliveries => Set<WebhookDelivery>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserAccount>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(user => user.Id);
            entity.Property(user => user.Username).HasMaxLength(32).IsRequired();
            entity.Property(user => user.NormalizedUsername).HasMaxLength(32).IsRequired();
            entity.Property(user => user.PasswordHash).HasMaxLength(512).IsRequired();
            entity.Property(user => user.DisplayName).HasMaxLength(80);
            entity.Property(user => user.Locale).HasMaxLength(10);
            entity.HasIndex(user => user.NormalizedUsername).IsUnique();
        });

        modelBuilder.Entity<AuthSession>(entity =>
        {
            entity.ToTable("AuthSessions");
            entity.HasKey(session => session.TokenHash);
            entity.Property(session => session.TokenHash).HasMaxLength(64);
            entity.HasIndex(session => session.UserId);
            entity.HasOne(session => session.User)
                .WithMany()
                .HasForeignKey(session => session.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserSettings>(entity =>
        {
            entity.ToTable("Settings");
            entity.HasKey(settings => settings.UserId);
            entity.Property(settings => settings.Data).IsRequired();
            entity.HasOne(settings => settings.User)
                .WithOne(user => user.Settings)
                .HasForeignKey<UserSettings>(settings => settings.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<StudyRecord>(entity =>
        {
            entity.ToTable("StudyRecords");
            entity.HasKey(record => record.Seq);
            entity.Property(record => record.Seq).UseIdentityColumn();
            entity.Property(record => record.Collection).HasMaxLength(32).IsRequired();
            entity.Property(record => record.ItemId).HasMaxLength(100).IsRequired();
            entity.Property(record => record.Data).IsRequired();
            entity.HasIndex(record => new { record.UserId, record.Collection, record.ItemId }).IsUnique();
            entity.HasIndex(record => new { record.UserId, record.Collection, record.Seq });
            entity.HasIndex(record => new { record.WorkspaceId, record.Collection, record.Seq });
            entity.HasOne(record => record.User)
                .WithMany()
                .HasForeignKey(record => record.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<NoteDocument>(entity =>
        {
            entity.ToTable("Notes");
            entity.HasKey(note => new { note.UserId, note.NoteId });
            entity.Property(note => note.NoteId).HasMaxLength(100);
            entity.Property(note => note.Data).IsRequired();
            entity.HasIndex(note => note.WorkspaceId);
            entity.HasOne(note => note.User)
                .WithMany()
                .HasForeignKey(note => note.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FolderDocument>(entity =>
        {
            entity.ToTable("NoteFolders");
            entity.HasKey(folder => new { folder.UserId, folder.FolderId });
            entity.Property(folder => folder.FolderId).HasMaxLength(100);
            entity.Property(folder => folder.Data).IsRequired();
            entity.HasIndex(folder => folder.WorkspaceId);
            entity.HasOne(folder => folder.User)
                .WithMany()
                .HasForeignKey(folder => folder.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TrashDocument>(entity =>
        {
            entity.ToTable("NoteTrash");
            entity.HasKey(item => new { item.UserId, item.TrashId });
            entity.Property(item => item.TrashId).HasMaxLength(100);
            entity.Property(item => item.Data).IsRequired();
            entity.HasIndex(item => new { item.UserId, item.DeletedAt });
            entity.HasIndex(item => item.WorkspaceId);
            entity.HasOne(item => item.User)
                .WithMany()
                .HasForeignKey(item => item.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<NoteImage>(entity =>
        {
            entity.ToTable("NoteImages");
            entity.HasKey(image => new { image.UserId, image.ImageId });
            entity.Property(image => image.ImageId).HasMaxLength(100);
            entity.Property(image => image.DataUrl).IsRequired();
            entity.HasIndex(image => image.WorkspaceId);
            entity.HasOne(image => image.User)
                .WithMany()
                .HasForeignKey(image => image.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<NoteSetting>(entity =>
        {
            entity.ToTable("NoteSettings");
            entity.HasKey(setting => new { setting.UserId, setting.Key });
            entity.Property(setting => setting.Key).HasMaxLength(80);
            entity.Property(setting => setting.Value).IsRequired();
            entity.HasIndex(setting => setting.WorkspaceId);
            entity.HasOne(setting => setting.User)
                .WithMany()
                .HasForeignKey(setting => setting.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Workspace>(entity =>
        {
            entity.ToTable("Workspaces");
            entity.HasKey(workspace => workspace.Id);
            entity.Property(workspace => workspace.Name).HasMaxLength(120).IsRequired();
            entity.Property(workspace => workspace.Slug).HasMaxLength(80);
            entity.HasIndex(workspace => workspace.OwnerUserId);
            entity.HasIndex(workspace => new { workspace.OwnerUserId, workspace.Type })
                .HasFilter("[Type] = 1 AND [DeletedAt] IS NULL")
                .IsUnique();
            entity.HasOne(workspace => workspace.Owner)
                .WithMany()
                .HasForeignKey(workspace => workspace.OwnerUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<WorkspaceMember>(entity =>
        {
            entity.ToTable("WorkspaceMembers");
            entity.HasKey(member => member.Id);
            entity.HasIndex(member => new { member.WorkspaceId, member.UserId })
                .HasFilter("[DeletedAt] IS NULL")
                .IsUnique();
            entity.HasOne(member => member.Workspace)
                .WithMany(workspace => workspace.Members)
                .HasForeignKey(member => member.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(member => member.User)
                .WithMany()
                .HasForeignKey(member => member.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WorkspaceSettings>(entity =>
        {
            entity.ToTable("WorkspaceSettings");
            entity.HasKey(settings => settings.WorkspaceId);
            entity.Property(settings => settings.Data).IsRequired();
            entity.Property(settings => settings.LocaleDefault).HasMaxLength(10).IsRequired();
            entity.Property(settings => settings.TimeZone).HasMaxLength(64).IsRequired();
            entity.Property(settings => settings.FeatureFlags).IsRequired();
            entity.HasOne(settings => settings.Workspace)
                .WithOne(workspace => workspace.Settings)
                .HasForeignKey<WorkspaceSettings>(settings => settings.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WorkspaceSubscription>(entity =>
        {
            entity.ToTable("WorkspaceSubscriptions");
            entity.HasKey(subscription => subscription.Id);
            entity.Property(subscription => subscription.PlanCode).HasMaxLength(40).IsRequired();
            entity.Property(subscription => subscription.ExternalProvider).HasMaxLength(40);
            entity.Property(subscription => subscription.ExternalReference).HasMaxLength(120);
            entity.HasIndex(subscription => subscription.WorkspaceId);
            entity.HasOne(subscription => subscription.Workspace)
                .WithMany()
                .HasForeignKey(subscription => subscription.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GamificationProfile>(entity =>
        {
            entity.ToTable("GamificationProfiles");
            entity.HasKey(profile => profile.Id);
            entity.Property(profile => profile.Code).HasMaxLength(40).IsRequired();
            entity.Property(profile => profile.Name).HasMaxLength(120).IsRequired();
            entity.Property(profile => profile.RulesJson).IsRequired();
            entity.Property(profile => profile.LevelsJson).IsRequired();
            entity.Property(profile => profile.StreakJson).IsRequired();
            entity.HasIndex(profile => profile.Code).IsUnique();
        });

        modelBuilder.Entity<DomainOutbox>(entity =>
        {
            entity.ToTable("DomainOutbox");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.EventType).HasMaxLength(80).IsRequired();
            entity.Property(item => item.PayloadJson).IsRequired();
            entity.Property(item => item.IdempotencyKey).HasMaxLength(120).IsRequired();
            entity.Property(item => item.LeaseOwner).HasMaxLength(80);
            entity.Property(item => item.LastError).HasMaxLength(1000);
            entity.HasIndex(item => new { item.WorkspaceId, item.EventType, item.IdempotencyKey }).IsUnique();
            entity.HasIndex(item => new { item.Status, item.NextAttemptAt });
        });

        modelBuilder.Entity<ProcessedEvent>(entity =>
        {
            entity.ToTable("ProcessedEvents");
            entity.HasKey(item => new { item.ConsumerName, item.EventId });
            entity.Property(item => item.ConsumerName).HasMaxLength(80);
        });

        modelBuilder.Entity<XpLedger>(entity =>
        {
            entity.ToTable("XpLedger");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Reason).HasMaxLength(200).IsRequired();
            entity.Property(item => item.RuleCode).HasMaxLength(80);
            entity.HasIndex(item => item.EventId).IsUnique().HasFilter("[EventId] IS NOT NULL");
            entity.HasIndex(item => new { item.WorkspaceId, item.UserId, item.CreatedAt });
        });

        modelBuilder.Entity<CoinLedger>(entity =>
        {
            entity.ToTable("CoinLedger");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Reason).HasMaxLength(200).IsRequired();
            entity.Property(item => item.RuleCode).HasMaxLength(80);
            entity.HasIndex(item => item.EventId).IsUnique().HasFilter("[EventId] IS NOT NULL");
            entity.HasIndex(item => new { item.WorkspaceId, item.UserId, item.CreatedAt });
        });

        modelBuilder.Entity<UserGameStats>(entity =>
        {
            entity.ToTable("UserGameStats");
            entity.HasKey(item => new { item.WorkspaceId, item.UserId });
            entity.Property(item => item.TimeZone).HasMaxLength(64).IsRequired();
        });

        modelBuilder.Entity<BadgeDefinition>(entity =>
        {
            entity.ToTable("Badges");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Code).HasMaxLength(60).IsRequired();
            entity.Property(item => item.Title).HasMaxLength(120).IsRequired();
            entity.Property(item => item.Description).HasMaxLength(400).IsRequired();
            entity.Property(item => item.Icon).HasMaxLength(40).IsRequired();
            entity.Property(item => item.ConditionsJson).IsRequired();
            entity.HasIndex(item => new { item.ProfileId, item.Code });
            entity.HasIndex(item => new { item.WorkspaceId, item.Code });
        });

        modelBuilder.Entity<UserBadge>(entity =>
        {
            entity.ToTable("UserBadges");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.WorkspaceId, item.UserId, item.BadgeId }).IsUnique();
            entity.HasOne(item => item.Badge)
                .WithMany()
                .HasForeignKey(item => item.BadgeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<QuestDefinition>(entity =>
        {
            entity.ToTable("Quests");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Title).HasMaxLength(160).IsRequired();
            entity.Property(item => item.Description).HasMaxLength(800).IsRequired();
            entity.Property(item => item.Category).HasMaxLength(40).IsRequired();
            entity.Property(item => item.CompletionCondition).HasMaxLength(80).IsRequired();
            entity.HasIndex(item => new { item.WorkspaceId, item.Status, item.Kind });
        });

        modelBuilder.Entity<UserQuestProgress>(entity =>
        {
            entity.ToTable("UserQuestProgress");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.WorkspaceId, item.UserId, item.QuestId }).IsUnique();
            entity.HasOne(item => item.Quest)
                .WithMany()
                .HasForeignKey(item => item.QuestId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<StreakDay>(entity =>
        {
            entity.ToTable("StreakDays");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.WorkspaceId, item.UserId, item.LocalDate }).IsUnique();
        });

        modelBuilder.Entity<GameNotification>(entity =>
        {
            entity.ToTable("GameNotifications");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Title).HasMaxLength(160).IsRequired();
            entity.Property(item => item.Body).HasMaxLength(400).IsRequired();
            entity.Property(item => item.PayloadJson).IsRequired();
            entity.HasIndex(item => new { item.WorkspaceId, item.UserId, item.Read, item.CreatedAt });
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("AuditLogs");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Action).HasMaxLength(80).IsRequired();
            entity.Property(item => item.TargetType).HasMaxLength(80).IsRequired();
            entity.Property(item => item.TargetId).HasMaxLength(80);
            entity.Property(item => item.DetailJson).IsRequired();
            entity.HasIndex(item => new { item.WorkspaceId, item.CreatedAt });
        });

        modelBuilder.Entity<UserPrivacySettings>(entity =>
        {
            entity.ToTable("UserPrivacySettings");
            entity.HasKey(item => new { item.WorkspaceId, item.UserId });
        });

        modelBuilder.Entity<LeaderboardDefinition>(entity =>
        {
            entity.ToTable("LeaderboardDefinitions");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.PeriodCode).HasMaxLength(20).IsRequired();
            entity.Property(item => item.Visibility).HasMaxLength(20).IsRequired();
            entity.HasIndex(item => new { item.WorkspaceId, item.Metric, item.PeriodCode });
        });

        modelBuilder.Entity<LeaderboardSnapshot>(entity =>
        {
            entity.ToTable("LeaderboardSnapshots");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.PeriodKey).HasMaxLength(40).IsRequired();
            entity.Property(item => item.Subject).HasMaxLength(90);
            entity.Property(item => item.PayloadJson).IsRequired();
            entity.HasIndex(item => new { item.WorkspaceId, item.Metric, item.PeriodKey, item.GeneratedAt });
        });

        modelBuilder.Entity<ClassGroup>(entity =>
        {
            entity.ToTable("Classes");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Name).HasMaxLength(120).IsRequired();
            entity.HasIndex(item => item.WorkspaceId);
            entity.HasIndex(item => new { item.WorkspaceId, item.AcademicYearId, item.GradeId });
        });

        modelBuilder.Entity<ClassMember>(entity =>
        {
            entity.ToTable("ClassMembers");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.ClassId, item.UserId }).IsUnique();
            entity.HasOne(item => item.Class).WithMany(c => c.Members).HasForeignKey(item => item.ClassId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Team>(entity =>
        {
            entity.ToTable("Teams");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Name).HasMaxLength(120).IsRequired();
            entity.Property(item => item.Description).HasMaxLength(400).IsRequired();
            entity.HasIndex(item => item.WorkspaceId);
        });

        modelBuilder.Entity<TeamMember>(entity =>
        {
            entity.ToTable("TeamMembers");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.TeamId, item.UserId }).IsUnique();
            entity.HasOne(item => item.Team).WithMany(t => t.Members).HasForeignKey(item => item.TeamId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TeamInvite>(entity =>
        {
            entity.ToTable("TeamInvites");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.TeamId, item.InvitedUserId, item.Status });
            entity.HasOne(item => item.Team).WithMany().HasForeignKey(item => item.TeamId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TeamChallenge>(entity =>
        {
            entity.ToTable("TeamChallenges");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Title).HasMaxLength(160).IsRequired();
            entity.Property(item => item.Description).HasMaxLength(400).IsRequired();
            entity.HasOne(item => item.Team).WithMany().HasForeignKey(item => item.TeamId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TeamAchievement>(entity =>
        {
            entity.ToTable("TeamAchievements");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Code).HasMaxLength(60).IsRequired();
            entity.Property(item => item.Title).HasMaxLength(120).IsRequired();
            entity.HasIndex(item => new { item.TeamId, item.Code });
        });

        modelBuilder.Entity<Competition>(entity =>
        {
            entity.ToTable("Competitions");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Name).HasMaxLength(160).IsRequired();
            entity.Property(item => item.Description).HasMaxLength(800).IsRequired();
            entity.Property(item => item.ScoringRulesJson).IsRequired();
            entity.Property(item => item.RewardsJson).IsRequired();
            entity.Property(item => item.Visibility).HasMaxLength(20).IsRequired();
            entity.HasIndex(item => new { item.WorkspaceId, item.Status });
        });

        modelBuilder.Entity<CompetitionParticipant>(entity =>
        {
            entity.ToTable("CompetitionParticipants");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.CompetitionId, item.UserId }).IsUnique();
            entity.HasOne(item => item.Competition).WithMany().HasForeignKey(item => item.CompetitionId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CompetitionTeam>(entity =>
        {
            entity.ToTable("CompetitionTeams");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.CompetitionId, item.TeamId }).IsUnique();
            entity.HasOne(item => item.Competition).WithMany().HasForeignKey(item => item.CompetitionId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<StoreItem>(entity =>
        {
            entity.ToTable("StoreItems");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Code).HasMaxLength(60).IsRequired();
            entity.Property(item => item.Title).HasMaxLength(120).IsRequired();
            entity.Property(item => item.Description).HasMaxLength(400).IsRequired();
            entity.Property(item => item.AssetKey).HasMaxLength(80).IsRequired();
            entity.Property(item => item.CssClass).HasMaxLength(80).IsRequired();
            entity.HasIndex(item => new { item.WorkspaceId, item.Code });
        });

        modelBuilder.Entity<UserInventoryItem>(entity =>
        {
            entity.ToTable("UserInventory");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.WorkspaceId, item.UserId, item.ItemId }).IsUnique();
            entity.HasOne(item => item.Item).WithMany().HasForeignKey(item => item.ItemId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StorePurchase>(entity =>
        {
            entity.ToTable("StorePurchases");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.WorkspaceId, item.UserId, item.CreatedAt });
        });

        modelBuilder.Entity<UserAvatarLoadout>(entity =>
        {
            entity.ToTable("UserAvatarLoadouts");
            entity.HasKey(item => new { item.WorkspaceId, item.UserId });
        });

        modelBuilder.Entity<RewardGrant>(entity =>
        {
            entity.ToTable("RewardGrants");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Title).HasMaxLength(160).IsRequired();
            entity.Property(item => item.Description).HasMaxLength(400).IsRequired();
            entity.HasIndex(item => new { item.WorkspaceId, item.UserId, item.Status });
        });

        modelBuilder.Entity<AssessmentDefinition>(entity =>
        {
            entity.ToTable("Assessments");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Title).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Description).HasMaxLength(800).IsRequired();
            entity.Property(item => item.Subject).HasMaxLength(90).IsRequired();
            entity.Property(item => item.Chapter).HasMaxLength(120);
            entity.Property(item => item.Topic).HasMaxLength(120);
            entity.HasIndex(item => new { item.WorkspaceId, item.Status, item.Kind });
        });

        modelBuilder.Entity<AssessmentQuestion>(entity =>
        {
            entity.ToTable("AssessmentQuestions");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Prompt).IsRequired();
            entity.Property(item => item.OptionsJson).IsRequired();
            entity.Property(item => item.CorrectAnswerJson).IsRequired();
            entity.Property(item => item.Subject).HasMaxLength(90).IsRequired();
            entity.Property(item => item.Chapter).HasMaxLength(120);
            entity.Property(item => item.Topic).HasMaxLength(120);
            entity.HasIndex(item => new { item.AssessmentId, item.Ordinal });
            entity.HasOne(item => item.Assessment).WithMany(a => a.Questions).HasForeignKey(item => item.AssessmentId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AssessmentAttempt>(entity =>
        {
            entity.ToTable("AssessmentAttempts");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.WorkspaceId, item.UserId, item.StartedAt });
            entity.HasOne(item => item.Assessment).WithMany().HasForeignKey(item => item.AssessmentId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AssessmentAnswer>(entity =>
        {
            entity.ToTable("AssessmentAnswers");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.ResponseJson).IsRequired();
            entity.HasIndex(item => new { item.AttemptId, item.QuestionId }).IsUnique();
            entity.HasOne(item => item.Attempt).WithMany(a => a.Answers).HasForeignKey(item => item.AttemptId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.Question).WithMany().HasForeignKey(item => item.QuestionId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MasteryNode>(entity =>
        {
            entity.ToTable("MasteryNodes");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Subject).HasMaxLength(90).IsRequired();
            entity.Property(item => item.Chapter).HasMaxLength(120);
            entity.Property(item => item.Topic).HasMaxLength(120);
            entity.HasIndex(item => new { item.WorkspaceId, item.UserId, item.Subject, item.Chapter, item.Topic }).IsUnique();
        });

        modelBuilder.Entity<SpacedRepetitionCard>(entity =>
        {
            entity.ToTable("SpacedRepetitionCards");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Subject).HasMaxLength(90).IsRequired();
            entity.Property(item => item.Chapter).HasMaxLength(120);
            entity.Property(item => item.Topic).HasMaxLength(120).IsRequired();
            entity.Property(item => item.AlgorithmCode).HasMaxLength(40).IsRequired();
            entity.Property(item => item.AlgorithmStateJson).IsRequired();
            entity.HasIndex(item => new { item.WorkspaceId, item.UserId, item.NextReviewDate });
            entity.HasIndex(item => new { item.WorkspaceId, item.UserId, item.Subject, item.Topic });
        });

        modelBuilder.Entity<SpacedRepetitionReview>(entity =>
        {
            entity.ToTable("SpacedRepetitionReviews");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.AlgorithmCode).HasMaxLength(40).IsRequired();
            entity.Property(item => item.StateBeforeJson).IsRequired();
            entity.Property(item => item.StateAfterJson).IsRequired();
            entity.HasIndex(item => new { item.CardId, item.ReviewedAt });
            entity.HasOne(item => item.Card).WithMany().HasForeignKey(item => item.CardId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SpacedRepetitionProfile>(entity =>
        {
            entity.ToTable("SpacedRepetitionProfiles");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Code).HasMaxLength(40).IsRequired();
            entity.Property(item => item.Name).HasMaxLength(120).IsRequired();
            entity.Property(item => item.AlgorithmCode).HasMaxLength(40).IsRequired();
            entity.Property(item => item.ConfigJson).IsRequired();
            entity.HasIndex(item => new { item.WorkspaceId, item.Code });
        });

        modelBuilder.Entity<AiWorkspaceSettings>(entity =>
        {
            entity.ToTable("AiWorkspaceSettings");
            entity.HasKey(item => item.WorkspaceId);
            entity.Property(item => item.ProviderCode).HasMaxLength(40).IsRequired();
            entity.Property(item => item.Model).HasMaxLength(80).IsRequired();
            entity.Property(item => item.SystemPromptExtra).HasMaxLength(1000).IsRequired();
        });

        modelBuilder.Entity<AiRecommendation>(entity =>
        {
            entity.ToTable("AiRecommendations");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Title).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Body).IsRequired();
            entity.Property(item => item.PayloadJson).IsRequired();
            entity.Property(item => item.FactsJson).IsRequired();
            entity.Property(item => item.ProviderCode).HasMaxLength(40).IsRequired();
            entity.HasIndex(item => new { item.WorkspaceId, item.UserId, item.Status, item.CreatedAt });
        });

        modelBuilder.Entity<AiWeeklyReport>(entity =>
        {
            entity.ToTable("AiWeeklyReports");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.PeriodKey).HasMaxLength(40).IsRequired();
            entity.Property(item => item.FactsJson).IsRequired();
            entity.Property(item => item.Narrative).IsRequired();
            entity.Property(item => item.RecommendationsJson).IsRequired();
            entity.Property(item => item.ProviderCode).HasMaxLength(40).IsRequired();
            entity.HasIndex(item => new { item.WorkspaceId, item.UserId, item.PeriodKey }).IsUnique();
        });

        modelBuilder.Entity<AiPlanSuggestion>(entity =>
        {
            entity.ToTable("AiPlanSuggestions");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Title).HasMaxLength(200).IsRequired();
            entity.Property(item => item.ScheduleJson).IsRequired();
            entity.Property(item => item.FactsJson).IsRequired();
            entity.Property(item => item.ProviderCode).HasMaxLength(40).IsRequired();
            entity.HasIndex(item => new { item.WorkspaceId, item.UserId, item.Status, item.CreatedAt });
        });

        modelBuilder.Entity<School>(entity =>
        {
            entity.ToTable("Schools");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Name).HasMaxLength(160).IsRequired();
            entity.Property(item => item.Slug).HasMaxLength(80);
            entity.Property(item => item.Description).HasMaxLength(800).IsRequired();
            entity.Property(item => item.City).HasMaxLength(80).IsRequired();
            entity.Property(item => item.Address).HasMaxLength(240).IsRequired();
            entity.Property(item => item.Phone).HasMaxLength(40).IsRequired();
            entity.HasIndex(item => item.WorkspaceId).IsUnique();
            entity.HasIndex(item => item.Slug);
        });

        modelBuilder.Entity<AcademicYear>(entity =>
        {
            entity.ToTable("AcademicYears");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Name).HasMaxLength(80).IsRequired();
            entity.HasIndex(item => new { item.WorkspaceId, item.IsCurrent });
        });

        modelBuilder.Entity<GradeLevel>(entity =>
        {
            entity.ToTable("GradeLevels");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Name).HasMaxLength(80).IsRequired();
            entity.HasIndex(item => new { item.WorkspaceId, item.SortOrder });
        });

        modelBuilder.Entity<SchoolSubject>(entity =>
        {
            entity.ToTable("SchoolSubjects");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Name).HasMaxLength(90).IsRequired();
            entity.Property(item => item.Code).HasMaxLength(40).IsRequired();
            entity.HasIndex(item => new { item.WorkspaceId, item.Code }).IsUnique();
        });

        modelBuilder.Entity<GuardianStudentLink>(entity =>
        {
            entity.ToTable("GuardianStudentLinks");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.WorkspaceId, item.ParentUserId, item.StudentUserId }).IsUnique();
            entity.HasIndex(item => new { item.WorkspaceId, item.StudentUserId, item.Status });
        });

        modelBuilder.Entity<SchoolAssignment>(entity =>
        {
            entity.ToTable("SchoolAssignments");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Title).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Description).HasMaxLength(2000).IsRequired();
            entity.HasIndex(item => new { item.WorkspaceId, item.ClassId, item.Status });
        });

        modelBuilder.Entity<SchoolAssignmentSubmission>(entity =>
        {
            entity.ToTable("SchoolAssignmentSubmissions");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Content).IsRequired();
            entity.HasIndex(item => new { item.AssignmentId, item.StudentUserId }).IsUnique();
            entity.HasOne(item => item.Assignment).WithMany().HasForeignKey(item => item.AssignmentId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TeacherFeedback>(entity =>
        {
            entity.ToTable("TeacherFeedback");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Body).HasMaxLength(2000).IsRequired();
            entity.HasIndex(item => new { item.WorkspaceId, item.StudentUserId, item.CreatedAt });
        });

        modelBuilder.Entity<SchoolSettingsExtras>(entity =>
        {
            entity.ToTable("SchoolSettingsExtras");
            entity.HasKey(item => item.WorkspaceId);
            entity.Property(item => item.AnalyticsDefaultsJson).IsRequired();
        });

        modelBuilder.Entity<ApiClient>(entity =>
        {
            entity.ToTable("ApiClients");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Name).HasMaxLength(120).IsRequired();
            entity.Property(item => item.KeyPrefix).HasMaxLength(16).IsRequired();
            entity.Property(item => item.KeyHash).HasMaxLength(64).IsRequired();
            entity.Property(item => item.ScopesJson).IsRequired();
            entity.HasIndex(item => item.KeyHash).IsUnique();
            entity.HasIndex(item => item.KeyPrefix);
        });

        modelBuilder.Entity<ApiClientWorkspace>(entity =>
        {
            entity.ToTable("ApiClientWorkspaces");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.ApiClientId, item.WorkspaceId }).IsUnique();
            entity.HasOne(item => item.ApiClient).WithMany().HasForeignKey(item => item.ApiClientId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WebhookEndpoint>(entity =>
        {
            entity.ToTable("WebhookEndpoints");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Url).HasMaxLength(500).IsRequired();
            entity.Property(item => item.Secret).HasMaxLength(128).IsRequired();
            entity.Property(item => item.EventTypesJson).IsRequired();
            entity.HasIndex(item => new { item.WorkspaceId, item.Active });
        });

        modelBuilder.Entity<WebhookDelivery>(entity =>
        {
            entity.ToTable("WebhookDeliveries");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.EventType).HasMaxLength(80).IsRequired();
            entity.Property(item => item.PayloadJson).IsRequired();
            entity.Property(item => item.Signature).HasMaxLength(128).IsRequired();
            entity.HasIndex(item => new { item.Status, item.NextAttemptAt });
            entity.HasIndex(item => new { item.WorkspaceId, item.CreatedAt });
            entity.HasOne(item => item.Endpoint).WithMany().HasForeignKey(item => item.WebhookEndpointId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
