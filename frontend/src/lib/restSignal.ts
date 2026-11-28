import { restOverHaptic } from './haptics'

const SOUND_KEY = 'gymrat.restSound'
const TONE_HZ = 880
const TONE_SECONDS = 0.25
const TONE_GAP_SECONDS = 0.1
const TONE_COUNT = 2
const TONE_GAIN = 0.2

let audioContext: AudioContext | null = null

function getAudioContext(): AudioContext | null {
  if (audioContext) {
    return audioContext
  }
  if (typeof window === 'undefined' || typeof window.AudioContext !== 'function') {
    return null
  }
  audioContext = new window.AudioContext()
  return audioContext
}

export function readSoundEnabled(): boolean {
  try {
    return localStorage.getItem(SOUND_KEY) !== 'off'
  } catch {
    return true
  }
}

export function writeSoundEnabled(enabled: boolean): void {
  try {
    localStorage.setItem(SOUND_KEY, enabled ? 'on' : 'off')
  } catch {
    return
  }
}

function resumeIfSuspended(context: AudioContext): void {
  if (context.state === 'suspended') {
    void context.resume().catch(() => undefined)
  }
}

export function prepareRestSignal(): void {
  const context = getAudioContext()
  if (context) {
    resumeIfSuspended(context)
  }
}

function playTone(context: AudioContext): void {
  for (let index = 0; index < TONE_COUNT; index += 1) {
    const start = context.currentTime + index * (TONE_SECONDS + TONE_GAP_SECONDS)
    const oscillator = context.createOscillator()
    const gain = context.createGain()
    oscillator.frequency.value = TONE_HZ
    gain.gain.setValueAtTime(TONE_GAIN, start)
    gain.gain.exponentialRampToValueAtTime(0.001, start + TONE_SECONDS)
    oscillator.connect(gain)
    gain.connect(context.destination)
    oscillator.start(start)
    oscillator.stop(start + TONE_SECONDS)
  }
}

export function signalRestOver(soundEnabled: boolean): void {
  restOverHaptic()
  if (!soundEnabled) {
    return
  }
  const context = getAudioContext()
  if (context) {
    resumeIfSuspended(context)
    playTone(context)
  }
}
