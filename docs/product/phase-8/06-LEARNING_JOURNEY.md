# 6. Learning Journey domain model

## Concept
Map-based path of **regions → stages → checkpoints**. Unlock via **learning evidence**, never elapsed study time or XP alone.

## Entities (logical)

```
JourneyDefinition (curriculum pack or workspace override)
  id, title, locale, version

JourneyRegion
  id, journeyId, order, title, visualThemeKey

JourneyStage
  id, regionId, order, title, narrativeBlurb?
  objectives[]          // LearningObjective refs
  masteryRequirements[] // see below
  unlockConditions[]    // prior stage + evidence thresholds
  rewards[]             // cosmetic / badge / coins (capped)

LearningObjective
  id, subject, topicKey?, description
  evidenceTypes: Assessment | SRS | MasteryNode | TeacherSignoff

Checkpoint
  stageId, order, label
  requiredEvidence: { type, minScore?, minMastery?, minReviewsCorrect? }

LearnerJourneyProgress
  workspaceId, userId, journeyId
  currentStageId
  unlockedStageIds[]
  checkpointEvidence[]  // refs to assessment attempts / mastery snapshots
  updatedAt
```

## Mastery requirements (examples)
- Mastery node EMA ≥ T for topic set
- Assessment pass with rubric evidence
- N successful SRS reviews on weak cards (correctness, not time)
- Optional teacher sign-off in school workspaces

## Unlock evaluation
`JourneyUnlockService` reads mastery + assessments + SRS; writes progress; emits outbox `JourneyStageUnlocked`.

**Rejected unlock signals:** raw minutes studied, XP, login streak alone.

## Narrative / visual feedback
- Region map SVG; stages as nodes; locked = muted, available = accent, complete = success
- Unlock animation: path segment reveal
- Evidence drawer: “چرا باز شد؟” lists concrete evidence IDs

## Bootstrap path
Phase 9: generate provisional journey from existing Topics + Mastery nodes (one region per subject).  
Phase 11+: authorable packs for schools.