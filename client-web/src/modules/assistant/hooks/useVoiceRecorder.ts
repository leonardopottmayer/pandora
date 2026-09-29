import { useRef, useState } from 'react'

const SAMPLE_RATE = 16000

/** Longest note: 16 kHz mono WAV is 32 KB/s, so this stays under the backend's 5 MB cap. */
const MAX_SECONDS = 150

/** Recording needs a secure context (https or localhost) and MediaRecorder. */
export const voiceSupported =
  typeof navigator !== 'undefined' &&
  !!navigator.mediaDevices?.getUserMedia &&
  typeof MediaRecorder !== 'undefined'

/**
 * Records a voice note from the microphone. `start` asks for the mic (and throws when it is denied);
 * `stop` — or the length cap — ends it and hands the raw recording to `onRecorded`.
 */
export function useVoiceRecorder(onRecorded: (recording: Blob) => void) {
  const [recording, setRecording] = useState(false)
  const recorderRef = useRef<MediaRecorder | null>(null)

  async function start() {
    const stream = await navigator.mediaDevices.getUserMedia({ audio: true })
    const recorder = new MediaRecorder(stream)
    const chunks: Blob[] = []
    const cap = window.setTimeout(() => recorder.stop(), MAX_SECONDS * 1000)
    recorder.ondataavailable = (e) => chunks.push(e.data)
    recorder.onstop = () => {
      window.clearTimeout(cap)
      stream.getTracks().forEach((track) => track.stop())
      recorderRef.current = null
      setRecording(false)
      onRecorded(new Blob(chunks, { type: recorder.mimeType }))
    }
    recorder.start()
    recorderRef.current = recorder
    setRecording(true)
  }

  function stop() {
    recorderRef.current?.stop()
  }

  return { recording, start, stop }
}

/**
 * Re-encodes a browser recording (webm/opus in Chrome, ogg in Firefox, mp4 in Safari) as 16 kHz mono
 * 16-bit WAV — a format every chat provider's audio input accepts, whatever the browser recorded.
 */
export async function toWav(recording: Blob): Promise<Blob> {
  const context = new AudioContext()
  let decoded: AudioBuffer
  try {
    decoded = await context.decodeAudioData(await recording.arrayBuffer())
  } finally {
    void context.close()
  }

  // Rendering into a 1-channel context at the target rate downmixes and resamples in one step.
  const offline = new OfflineAudioContext(1, Math.ceil(decoded.duration * SAMPLE_RATE), SAMPLE_RATE)
  const source = offline.createBufferSource()
  source.buffer = decoded
  source.connect(offline.destination)
  source.start()
  const pcm = (await offline.startRendering()).getChannelData(0)

  const view = new DataView(new ArrayBuffer(44 + pcm.length * 2))
  const ascii = (at: number, text: string) =>
    [...text].forEach((c, i) => view.setUint8(at + i, c.charCodeAt(0)))
  ascii(0, 'RIFF')
  view.setUint32(4, 36 + pcm.length * 2, true)
  ascii(8, 'WAVE')
  ascii(12, 'fmt ')
  view.setUint32(16, 16, true) // fmt chunk size
  view.setUint16(20, 1, true) // PCM
  view.setUint16(22, 1, true) // mono
  view.setUint32(24, SAMPLE_RATE, true)
  view.setUint32(28, SAMPLE_RATE * 2, true) // byte rate
  view.setUint16(32, 2, true) // block align
  view.setUint16(34, 16, true) // bits per sample
  ascii(36, 'data')
  view.setUint32(40, pcm.length * 2, true)
  pcm.forEach((sample, i) => view.setInt16(44 + i * 2, Math.max(-1, Math.min(1, sample)) * 0x7fff, true))

  return new Blob([view], { type: 'audio/wav' })
}
