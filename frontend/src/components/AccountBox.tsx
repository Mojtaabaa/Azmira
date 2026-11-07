// @ts-nocheck
import { useAuth } from '../hooks/useAuth'

export default function AccountBox() {
  const { user, logout } = useAuth()

  return (
    <div className="account-box">
      <span className="account-name" dir="ltr" title={user.username}>{user.username}</span>
      <button type="button" className="account-logout" onClick={logout}>
        خروج
      </button>
    </div>
  )
}
