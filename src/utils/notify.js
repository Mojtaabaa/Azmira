let audioContext = null

function getAudioContext() {
  if (typeof AudioContext === 'undefined') return null
  if (!audioContext) audioContext = new AudioContext()
  return audioContext
}

export function unlockSound() {
  const context = getAudioContext()
  if (context && context.state === 'suspended') {
    context.resume()
  }
}

export function playChime() {
  const context = getAudioContext()
  if (!context) return
  if (context.state === 'suspended') context.resume()

  const start = context.currentTime
  const notes = [660, 880]
  notes.forEach((frequency, index) => {
    const oscillator = context.createOscillator()
    const gain = context.createGain()
    const noteStart = start + index * 0.16
    oscillator.type = 'sine'
    oscillator.frequency.value = frequency
    gain.gain.setValueAtTime(0.0001, noteStart)
    gain.gain.exponentialRampToValueAtTime(0.18, noteStart + 0.02)
    gain.gain.exponentialRampToValueAtTime(0.0001, noteStart + 0.28)
    oscillator.connect(gain)
    gain.connect(context.destination)
    oscillator.start(noteStart)
    oscillator.stop(noteStart + 0.3)
  })
}

export function askNotifyPermission() {
  if (typeof Notification === 'undefined') return
  if (Notification.permission === 'default') {
    Notification.requestPermission()
  }
}

export function notifyPhaseEnd(finishedPhase, nextPhase) {
  playChime()
  if (typeof Notification === 'undefined' || Notification.permission !== 'granted') return

  const title = finishedPhase === 'focus' ? 'تمرکز تمام شد' : 'استراحت تمام شد'
  let body = 'وقتشه دوباره تمرکز کنی.'
  if (nextPhase === 'shortBreak') body = 'وقتشه یک استراحت کوتاه بکنی.'
  if (nextPhase === 'longBreak') body = 'وقتشه یک استراحت طولانی بکنی.'

  try {
    new Notification(title, { body })
  } catch {
    return
  }
}
