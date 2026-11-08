# Phase 14 — Experience Beta release checklist

**Audience:** ops + release owner  
**Does not deploy.** Apply only after backup + health smoke.

## Pre-deploy

1. Backup SQL Server database (`docs/production/DATABASE_MIGRATIONS.md`).
2. Confirm secrets outside source control: `ConnectionStrings__KonkoorHelp`, AI keys, `Integrations__WebhookProtectionKey`, `Security__AllowedOrigins`.
3. Apply pending EF migrations via app startup `MigrateAsync` or `dotnet ef database update`.
4. Verify `/health` and `/health/ready`.

## Migration inventory (Phases 8–13 additive)

| Migration | Feature area |
|-----------|----------------|
| `AddGamification` | XP / quests / streaks |
| `AddSocialGamification` | leaderboards / privacy / teams |
| `AddLearningIntelligence` | mastery / SRS / assessments / AI |
| `AddSchoolEdition` | school domain |
| `AddIntegrations` | public API / webhooks |
| `AddSchoolRegistrationRequests` | school signup |
| `AddLearningJourney` | journey map |
| `AddPersonalImprovementAndCoach` | baselines / coach threads |
| `AddJourneyPackPublishFlags` | school pack publish |

## Product smoke (post-migrate)

| Flow | Expect |
|------|--------|
| Personal login + Home | Four pillars; server streak; `/learner/home` OK |
| Daily missions | Assign + complete with evidence |
| Journey map | Bootstrap or active school pack |
| Improvement | Collecting → Ready; no XP-only baseline |
| Coach chat | `evidencePolicy=serverFactsOnly`; AI-disabled → 400 |
| Leaderboard | Default private / opt-in only |
| School pack | Teacher create → publish → activate |

## Privacy / tenancy

- [ ] Cross-workspace journey pack activate rejected
- [ ] Private users absent from board `entries` and `total`
- [ ] Coach thread id from workspace A rejected in B
- [ ] Improvement baselines scoped by workspace+user

## Rollback

Prefer DB restore from pre-deploy backup + prior container image. Do not hand-edit schema.
