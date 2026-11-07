// @ts-nocheck
import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { useAuth } from '../../hooks/useAuth'
import { request } from '../../utils/api'
import './ProgressWidget.css'

export default function ProgressWidget() {
  const { status } = useAuth()
  const [profile, setProfile] = useState(null)

  useEffect(() => {
    if (status !== 'authed') return undefined
    let alive = true
    request('GET', '/gamification/me', undefined, { silent: true })
      .then((payload) => {
        if (alive) setProfile(payload)
      })
      .catch(() => {
        if (alive) setProfile(null)
      })
    return () => {
      alive = false
    }
  }, [status])

  if (!profile) return null
  const ratio = Math.round((profile.levelProgressRatio || 0) * 100)

  return (
    <Link to="/progress" className="progress-widget">
      <div>
        <p className="progress-widget-label">پیشرفت</p>
        <strong>
          سطح {profile.level} · {profile.xpTotal} XP
        </strong>
        <span>
          استریک {profile.streakCurrent} · {profile.coins} سکه
        </span>
      </div>
      <div className="progress-widget-bar" aria-hidden="true">
        <span style={{ width: `${ratio}%` }} />
      </div>
    </Link>
  )
}
