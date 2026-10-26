import { useCallback, useEffect, useMemo, useState } from 'react'
import { AuthContext } from '../../context/authContext'
import { adoptBrowserDataIfNeeded } from '../../utils/adoptBrowserData'
import { ApiError, onUnauthorized, request } from '../../utils/api'
import { loadData, refreshData, resetData } from '../../utils/storage'
import { bindTimer, unbindTimer } from '../../utils/timerStore'
import { summarizeLegacy } from '../../utils/legacyData'

const LOADING = { status: 'loading', user: null, message: '', importNotice: '' }
const ANON = { status: 'anon', user: null, message: '', importNotice: '' }

function clearDrafts() {
  try {
    sessionStorage.clear()
  } catch {
    return
  }
}

export default function AuthProvider({ children }) {
  const [state, setState] = useState(LOADING)

  const enter = useCallback(async (user) => {
    await loadData()
    bindTimer(user.id)
    let importNotice = ''
    try {
      const result = await adoptBrowserDataIfNeeded(user.id)
      if (result.imported) {
        const summary = summarizeLegacy(result.summary)
        importNotice = summary
          ? `داده‌های قبلی مرورگر وارد حساب شد (${summary}).`
          : 'داده‌های قبلی مرورگر وارد حساب شد.'
      }
    } catch {
      importNotice = ''
    }
    setState({ status: 'authed', user, message: '', importNotice })
  }, [])

  const clearImportNotice = useCallback(() => {
    setState((current) => ({ ...current, importNotice: '' }))
  }, [])

  const leave = useCallback(() => {
    unbindTimer()
    resetData()
    clearDrafts()
    setState(ANON)
  }, [])

  const boot = useCallback(async () => {
    setState(LOADING)
    try {
      const { user } = await request('GET', '/auth/me', undefined, { silent: true })
      await enter(user)
    } catch (error) {
      if (error instanceof ApiError && error.status === 401) {
        setState(ANON)
        return
      }
      setState({ status: 'error', user: null, message: error.message || 'ارتباط با سرور برقرار نشد.', importNotice: '' })
    }
  }, [enter])

  useEffect(() => {
    const timer = setTimeout(boot, 0)
    return () => clearTimeout(timer)
  }, [boot])

  useEffect(() => onUnauthorized(leave), [leave])

  useEffect(() => {
    if (state.status !== 'authed') return undefined
    function onVisible() {
      if (document.visibilityState === 'visible') refreshData()
    }
    document.addEventListener('visibilitychange', onVisible)
    return () => document.removeEventListener('visibilitychange', onVisible)
  }, [state.status])

  const login = useCallback(async (username, password) => {
    const { user } = await request('POST', '/auth/login', { username, password }, { silent: true })
    await enter(user)
  }, [enter])

  const register = useCallback(async (username, password) => {
    const { user } = await request('POST', '/auth/register', { username, password }, { silent: true })
    await enter(user)
  }, [enter])

  const logout = useCallback(async () => {
    try {
      await request('POST', '/auth/logout', {}, { silent: true })
    } catch {
      void 0
    }
    leave()
  }, [leave])

  const value = useMemo(
    () => ({ ...state, login, register, logout, retry: boot, clearImportNotice }),
    [state, login, register, logout, boot, clearImportNotice],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
