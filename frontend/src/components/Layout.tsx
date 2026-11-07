// @ts-nocheck
import { useEffect, useMemo, useState } from 'react'
import { NavLink, Outlet, useLocation } from 'react-router-dom'
import AccountBox from './AccountBox'
import AppCommandPalette from './AppCommandPalette'
import BrandLogo from './BrandLogo'
import LegacyImport from './LegacyImport'
import NavIcon from './NavIcon'
import SyncNotice from './SyncNotice'
import ThemeToggle from './ThemeToggle'
import WorkspaceSwitcher from './WorkspaceSwitcher'
import GamificationToasts from './GamificationToasts'
import MiniTimer from './timer/MiniTimer'
import { useAuth } from '../hooks/useAuth'
import { MOBILE_PILLAR_PATHS, NAV_GROUPS, findActivePillar } from '../navigation/pillars'
import { navVisible, resolveCapabilities } from '../utils/capabilities'
import './Layout.css'

function NavItems({ items, activePillar }) {
  return items.map((item) => (
    <NavLink
      key={item.to}
      to={item.to}
      end={item.end}
      className={({ isActive }) => {
        const classes = ['nav-link']
        if (isActive) classes.push('is-active')
        if (activePillar && item.to && findActivePillar(item.to) === activePillar && isActive) {
          classes.push(`is-pillar-${activePillar}`)
        }
        return classes.join(' ')
      }}
    >
      <NavIcon name={item.icon} />
      <span className="nav-label-full">{item.label}</span>
      <span className="nav-label-short">{item.short}</span>
    </NavLink>
  ))
}

export default function Layout() {
  const { pathname } = useLocation()
  const { user, workspaces, activeWorkspaceId, capabilities } = useAuth()
  const caps = useMemo(
    () => resolveCapabilities(capabilities, workspaces, activeWorkspaceId, user),
    [capabilities, workspaces, activeWorkspaceId, user],
  )
  const groups = useMemo(
    () => NAV_GROUPS.map((group) => ({
      ...group,
      items: group.items.filter((item) => navVisible(item, caps)),
    })).filter((group) => group.items.length > 0),
    [caps],
  )
  const allItems = useMemo(() => groups.flatMap((group) => group.items), [groups])
  const primaryItems = useMemo(() => {
    const byPath = Object.fromEntries(allItems.map((item) => [item.to, item]))
    return MOBILE_PILLAR_PATHS.map((path) => byPath[path]).filter(Boolean)
  }, [allItems])
  const moreItems = useMemo(
    () => allItems.filter((item) => !MOBILE_PILLAR_PATHS.includes(item.to)),
    [allItems],
  )
  const activePillar = findActivePillar(pathname)

  const [commandOpen, setCommandOpen] = useState(false)
  const [moreOpen, setMoreOpen] = useState(false)
  const moreActive = moreItems.some((item) =>
    item.end ? pathname === item.to : pathname === item.to || pathname.startsWith(`${item.to}/`),
  )

  useEffect(() => {
    setMoreOpen(false)
  }, [pathname])

  useEffect(() => {
    function onKey(event) {
      const meta = event.metaKey || event.ctrlKey
      const key = event.key.toLowerCase()
      if (meta && key === 'k') {
        event.preventDefault()
        setCommandOpen(true)
        setMoreOpen(false)
      }
      if (event.key === 'Escape') {
        setCommandOpen(false)
        setMoreOpen(false)
      }
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [])

  return (
    <div className={`app-shell${activePillar ? ` pillar-${activePillar}` : ''}`}>
      <aside className="sidebar">
        <div className="brand">
          <BrandLogo />
          <div>
            <p className="brand-name">رتبه</p>
            <p className="brand-tag">مسیر یادگیری</p>
          </div>
          <ThemeToggle compact />
        </div>
        <nav className="sidebar-nav" aria-label="اصلی">
          {groups.map((group) => (
            <div
              className={`nav-group${group.pillar ? ` nav-group--${group.pillar}` : ''}`}
              key={group.label}
            >
              <p className="nav-group-label">
                <span className="nav-group-label-fa">{group.label}</span>
                {group.labelEn ? <span className="nav-group-label-en">{group.labelEn}</span> : null}
              </p>
              <NavItems items={group.items} activePillar={group.pillar} />
            </div>
          ))}
        </nav>
        <button
          type="button"
          className="sidebar-search"
          onClick={() => setCommandOpen(true)}
        >
          جستجو
          <kbd>Ctrl K</kbd>
        </button>
        <WorkspaceSwitcher />
        <AccountBox />
      </aside>

      <div className="workspace">
        <header className="topbar">
          <BrandLogo className="is-sm" size={32} />
          <p className="brand-name">رتبه</p>
          <button
            type="button"
            className="topbar-search"
            aria-label="جستجو در همه بخش‌ها"
            onClick={() => setCommandOpen(true)}
          >
            جستجو
          </button>
          <ThemeToggle compact />
          <WorkspaceSwitcher compact />
          <AccountBox />
        </header>
        <main className="content">
          <LegacyImport />
          <Outlet />
        </main>
      </div>

      <SyncNotice />
      <GamificationToasts />
      {pathname === '/timer' ? null : <MiniTimer />}

      <nav className="bottom-nav" aria-label="چهار ستون">
        <NavItems items={primaryItems} activePillar={activePillar} />
        <button
          type="button"
          className={moreActive || moreOpen ? 'nav-link is-active more-nav-btn' : 'nav-link more-nav-btn'}
          aria-expanded={moreOpen}
          aria-controls="more-nav-sheet"
          onClick={() => setMoreOpen((open) => !open)}
        >
          <NavIcon name="more" />
          <span className="nav-label-full">بیشتر</span>
          <span className="nav-label-short">بیشتر</span>
        </button>
      </nav>

      {moreOpen ? (
        <div className="more-nav-layer">
          <button
            type="button"
            className="more-nav-backdrop"
            aria-label="بستن"
            onClick={() => setMoreOpen(false)}
          />
          <div className="more-nav-sheet" id="more-nav-sheet" role="dialog" aria-label="صفحات بیشتر">
            <p className="more-nav-title">صفحات دیگر</p>
            <nav className="more-nav-list" aria-label="بیشتر">
              <NavItems items={moreItems} />
            </nav>
            <button
              type="button"
              className="more-nav-search"
              onClick={() => {
                setMoreOpen(false)
                setCommandOpen(true)
              }}
            >
              جستجو در همه صفحات
            </button>
          </div>
        </div>
      ) : null}

      <AppCommandPalette open={commandOpen} onClose={() => setCommandOpen(false)} />
    </div>
  )
}
