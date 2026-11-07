// @ts-nocheck
import { useAuth } from '../hooks/useAuth'
import './WorkspaceSwitcher.css'

export default function WorkspaceSwitcher({ compact = false }) {
  const { workspaces = [], activeWorkspaceId, switchWorkspace, status } = useAuth()
  if (status !== 'authed' || workspaces.length <= 1) return null

  return (
    <label className={compact ? 'workspace-switcher is-compact' : 'workspace-switcher'}>
      <span className="workspace-switcher-label">فضا</span>
      <select
        value={activeWorkspaceId || ''}
        onChange={(event) => {
          const next = event.target.value
          if (next) void switchWorkspace(next)
        }}
        aria-label="انتخاب فضای کاری"
      >
        {workspaces.map((workspace) => (
          <option key={workspace.id} value={workspace.id}>
            {workspace.name}
          </option>
        ))}
      </select>
    </label>
  )
}
