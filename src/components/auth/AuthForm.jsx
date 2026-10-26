import { useState } from 'react'
import { useAuth } from '../../hooks/useAuth'

const USERNAME_PATTERN = /^[A-Za-z0-9_.]{3,32}$/

export default function AuthForm({ mode }) {
  const { login, register } = useAuth()
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [repeat, setRepeat] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const signingUp = mode === 'register'

  function check() {
    const name = username.trim()
    if (!name) return 'نام کاربری را بنویس.'
    if (signingUp && !USERNAME_PATTERN.test(name)) {
      return 'نام کاربری باید 3 تا 32 نویسه باشد و فقط حرف انگلیسی، عدد، نقطه یا زیرخط داشته باشد.'
    }
    if (!password) return 'رمز عبور را بنویس.'
    if (signingUp && (password.length < 8 || password.length > 128)) {
      return 'رمز عبور باید از 8 تا 128 نویسه باشد.'
    }
    if (signingUp && repeat !== password) return 'تکرار رمز عبور با رمز عبور یکی نیست.'
    return ''
  }

  async function handleSubmit(event) {
    event.preventDefault()
    if (busy) return
    const problem = check()
    if (problem) {
      setError(problem)
      return
    }

    setBusy(true)
    setError('')
    try {
      if (signingUp) await register(username.trim(), password)
      else await login(username.trim(), password)
    } catch (failure) {
      setError(failure.message || 'انجام نشد. دوباره امتحان کن.')
      setBusy(false)
    }
  }

  return (
    <form className="auth-form" noValidate onSubmit={handleSubmit}>
      <label>
        نام کاربری
        <input
          dir="ltr"
          value={username}
          maxLength={32}
          autoComplete="username"
          autoCapitalize="none"
          spellCheck={false}
          onChange={(event) => {
            setUsername(event.target.value)
            setError('')
          }}
        />
      </label>
      <label>
        رمز عبور
        <input
          dir="ltr"
          type="password"
          value={password}
          maxLength={128}
          autoComplete={signingUp ? 'new-password' : 'current-password'}
          onChange={(event) => {
            setPassword(event.target.value)
            setError('')
          }}
        />
      </label>
      {signingUp ? (
        <label>
          تکرار رمز عبور
          <input
            dir="ltr"
            type="password"
            value={repeat}
            maxLength={128}
            autoComplete="new-password"
            onChange={(event) => {
              setRepeat(event.target.value)
              setError('')
            }}
          />
        </label>
      ) : null}
      {error ? <p className="form-message is-error" role="alert">{error}</p> : null}
      <button type="submit" className="button button-primary" disabled={busy}>
        {busy ? 'کمی صبر کن' : signingUp ? 'ساخت حساب' : 'ورود'}
      </button>
    </form>
  )
}
