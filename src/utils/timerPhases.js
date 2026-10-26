export function nextPhaseAfterComplete(phase, cycleCount) {
  if (phase === 'focus') {
    const completed = cycleCount + 1
    if (completed >= 4) return { phase: 'longBreak', cycleCount: 4 }
    return { phase: 'shortBreak', cycleCount: completed }
  }

  if (phase === 'longBreak') return { phase: 'focus', cycleCount: 0 }
  return { phase: 'focus', cycleCount }
}

export function nextPhaseAfterSkip(phase, cycleCount) {
  if (phase === 'focus') return { phase: 'shortBreak', cycleCount }
  if (phase === 'longBreak') return { phase: 'focus', cycleCount: 0 }
  return { phase: 'focus', cycleCount }
}
