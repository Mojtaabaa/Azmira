// @ts-nocheck
import { useEffect, useState } from 'react'
import { useAuth } from '../hooks/useAuth'
import { request } from '../utils/api'
import './GamificationToasts.css'

export default function GamificationToasts() {
  const { status } = useAuth()
  const [items, setItems] = useState([])

  useEffect(() => {
    if (status !== 'authed') return undefined
    let alive = true

    async function poll() {
      try {
        const payload = await request('GET', '/gamification/notifications', undefined, { silent: true })
        if (!alive) return
        setItems(payload.items || [])
      } catch {
        if (alive) setItems([])
      }
    }

    void poll()
    const timer = setInterval(poll, 8000)
    return () => {
      alive = false
      clearInterval(timer)
    }
  }, [status])

  async function dismiss(id) {
    setItems((current) => current.filter((item) => item.id !== id))
    try {
      await request('POST', `/gamification/notifications/${id}/read`, {})
    } catch {
      void 0
    }
  }

  if (!items.length) return null

  return (
    <div className="game-toasts" aria-live="polite">
      {items.slice(0, 3).map((item) => (
        <button
          key={item.id}
          type="button"
          className={`game-toast kind-${String(item.kind || '').toLowerCase()}`}
          onClick={() => dismiss(item.id)}
        >
          <strong>{item.title}</strong>
          <span>{item.body}</span>
        </button>
      ))}
    </div>
  )
}
