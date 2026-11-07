// @ts-nocheck
import { useAuth } from '../hooks/useAuth'
import { resolveCapabilities } from '../utils/capabilities'

export default function CapabilityGate({ require, children, title = 'دسترسی ندارید' }) {
  const { user, workspaces, activeWorkspaceId, capabilities } = useAuth()
  const caps = resolveCapabilities(capabilities, workspaces, activeWorkspaceId, user)

  const ok = (() => {
    if (!require) return true
    if (require === 'platformAdmin') return caps.platformAdmin
    if (require === 'schoolAdmin') return caps.active.canManageSchool || caps.canManageSchool
    if (require === 'teacher') return caps.active.teacher || caps.teacher || caps.platformAdmin
    if (require === 'parent') return caps.active.parent || caps.parent
    if (require === 'schoolAnalytics') return caps.active.teacherOrAbove || caps.teacherOrAbove
    if (require === 'schoolLink') return caps.canManageSchool || caps.canRequestSchool || caps.schoolMember
    return true
  })()

  if (ok) return children

  return (
    <div className="school-page">
      <header className="school-hero">
        <div>
          <p className="school-kicker">دسترسی</p>
          <h1>{title}</h1>
          <p className="school-sub">این بخش فقط برای نقش‌های مجاز نمایش داده می‌شود.</p>
        </div>
      </header>
    </div>
  )
}
