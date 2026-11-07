// @ts-nocheck
import { useAuth } from '../../hooks/useAuth'
import BrandLogo from '../BrandLogo'
import LoginPage from '../../pages/LoginPage'

export default function AuthGate({ children }) {
  const { status, message, retry } = useAuth()

  if (status === 'loading') {
    return (
      <div className="auth-splash" role="status">
        <BrandLogo size={72} className="is-lg" />
        <p>در حال بارگذاری</p>
      </div>
    )
  }

  if (status === 'error') {
    return (
      <div className="auth-splash" role="alert">
        <BrandLogo size={72} className="is-lg" />
        <p>{message}</p>
        <button type="button" className="button" onClick={retry}>
          دوباره امتحان کن
        </button>
      </div>
    )
  }

  if (status === 'anon') return <LoginPage />

  return children
}
