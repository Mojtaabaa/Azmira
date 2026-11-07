// @ts-nocheck
import { useCallback, useEffect, useMemo, useState } from 'react'
import { AuthContext } from '../../context/authContext'
import { adoptBrowserDataIfNeeded } from '../../utils/adoptBrowserData'
import { ApiError, onUnauthorized, request, setWorkspaceHeader } from '../../utils/api'
import { loadData, refreshData, resetData } from '../../utils/storage'
import { bindTimer, unbindTimer } from '../../utils/timerStore'
import { summarizeLegacy } from '../../utils/legacyData'

const LOADING = {
  status: 'loading',
  user: null,
  workspaces: [],
  activeWorkspaceId: null,
  capabilities: null,
  message: '',
  importNotice: '',
}
const ANON = {
  status: 'anon',
  user: null,
  workspaces: [],
  activeWorkspaceId: null,
  capabilities: null,
  message: '',
  importNotice: '',
}

function clearDrafts() {
  try {
    sessionStorage.clear()
  } catch {
    return
  }
}

function pickWorkspace(payload) {
  const workspaces = Array.isArray(payload?.workspaces) ? payload.workspaces : []
  const activeWorkspaceId = payload?.activeWorkspaceId || payload?.personalWorkspaceId || workspaces[0]?.id || null
  return { workspaces, activeWorkspaceId }
}

export default function AuthProvider({ children }) {
  const [state, setState] = useState(LOADING)

  const enter = useCallback(async (user, workspaceInfo = {}) => {
    const { workspaces, activeWorkspaceId } = pickWorkspace(workspaceInfo)
    setWorkspaceHeader(activeWorkspaceId)
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
    setState({
      status: 'authed',
      user,
      workspaces,
      activeWorkspaceId,
      capabilities: workspaceInfo?.capabilities || null,
      message: '',
      importNotice,
    })
  }, [])

  const clearImportNotice = useCallback(() => {
    setState((current) => ({ ...current, importNotice: '' }))
  }, [])

  const leave = useCallback(() => {
    unbindTimer()
    resetData()
    clearDrafts()
    setWorkspaceHeader('')
    setState(ANON)
  }, [])

  const boot = useCallback(async () => {
    setState(LOADING)
    try {
      const payload = await request('GET', '/auth/me', undefined, { silent: true })
      await enter(payload.user, payload)
    } catch (error) {
      if (error instanceof ApiError && error.status === 401) {
        setState(ANON)
        return
      }
      setState({
        status: 'error',
        user: null,
        workspaces: [],
        activeWorkspaceId: null,
        capabilities: null,
        message: error.message || 'ارتباط با سرور برقرار نشد.',
        importNotice: '',
      })
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
    const payload = await request('POST', '/auth/login', { username, password }, { silent: true })
    await enter(payload.user, payload)
  }, [enter])

  const register = useCallback(async (username, password) => {
    const payload = await request('POST', '/auth/register', { username, password }, { silent: true })
    await enter(payload.user, payload)
  }, [enter])

  const logout = useCallback(async () => {
    try {
      await request('POST', '/auth/logout', {}, { silent: true })
    } catch {
      void 0
    }
    leave()
  }, [leave])

  const switchWorkspace = useCallback(async (workspaceId) => {
    if (!workspaceId || workspaceId === state.activeWorkspaceId) return
    await request('POST', `/workspaces/${workspaceId}/switch`, {})
    setWorkspaceHeader(workspaceId)
    resetData()
    clearDrafts()
    await loadData()
    let capabilities = state.capabilities
    try {
      const me = await request('GET', '/auth/me', undefined, { silent: true })
      capabilities = me.capabilities || capabilities
    } catch {
      void 0
    }
    setState((current) => ({
      ...current,
      activeWorkspaceId: workspaceId,
      capabilities,
    }))
  }, [state.activeWorkspaceId, state.capabilities])

  const value = useMemo(
    () => ({ ...state, login, register, logout, retry: boot, clearImportNotice, switchWorkspace }),
    [state, login, register, logout, boot, clearImportNotice, switchWorkspace],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
