// @ts-nocheck
import { useEffect, useState } from 'react'
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
import './Layout.css'

const NAV_GROUPS = [
  {
    label: 'امروز',
    items: [
      { to: '/', label: 'خانه', short: 'خانه', icon: 'home', end: true },
      { to: '/timer', label: 'زمان‌سنج', short: 'زمان', icon: 'timer' },
      { to: '/progress', label: 'پیشرفت', short: 'پیشرفت', icon: 'chart' },
      { to: '/leaderboards', label: 'رتبه‌بندی', short: 'رتبه', icon: 'chart' },
      { to: '/store', label: 'فروشگاه', short: 'فروش', icon: 'data' },
    ],
  },
  {
    label: 'تمرین',
    items: [
      { to: '/tests', label: 'آزمون‌ها', short: 'آزمون', icon: 'tests' },
      { to: '/assessments', label: 'ارزیابی', short: 'ارزیابی', icon: 'tests' },
      { to: '/mistakes', label: 'اشتباهات', short: 'مرور', icon: 'mistakes' },
      { to: '/reviews', label: 'بازبینی', short: 'مرور', icon: 'mistakes' },
      { to: '/topics', label: 'مباحث', short: 'مبحث', icon: 'topics' },
      { to: '/mastery', label: 'تسلط', short: 'تسلط', icon: 'chart' },
    ],
  },
  {
    label: 'برنامه',
    items: [
      { to: '/tasks', label: 'کارها', short: 'کار', icon: 'tasks' },
      { to: '/planner', label: 'برنامه', short: 'هفته', icon: 'calendar' },
      { to: '/notes', label: 'یادداشت‌ها', short: 'یادداشت', icon: 'notes' },
    ],
  },
  {
    label: 'حساب',
    items: [
      { to: '/analytics', label: 'آمار', short: 'آمار', icon: 'chart' },
      { to: '/ai-coach', label: 'مربی AI', short: 'AI', icon: 'chart' },
      { to: '/school', label: 'مدرسه', short: 'مدرسه', icon: 'data' },
      { to: '/teacher', label: 'معلم', short: 'معلم', icon: 'tasks' },
      { to: '/parent', label: 'والدین', short: 'والد', icon: 'home' },
      { to: '/school/analytics', label: 'تحلیل مدرسه', short: 'تحلیل', icon: 'chart' },
      { to: '/teams', label: 'تیم‌ها', short: 'تیم', icon: 'tasks' },
      { to: '/competitions', label: 'مسابقات', short: 'مسابقه', icon: 'tests' },
      { to: '/data', label: 'انتقال داده', short: 'داده', icon: 'data' },
      { to: '/settings', label: 'تنظیمات', short: 'تنظیم', icon: 'settings' },
    ],
  },
]

const NAV_ITEMS = NAV_GROUPS.flatMap((group) => group.items)

const PRIMARY_PATHS = new Set(['/', '/timer', '/tests', '/planner'])
const PRIMARY_ITEMS = NAV_ITEMS.filter((item) => PRIMARY_PATHS.has(item.to))
const MORE_ITEMS = NAV_ITEMS.filter((item) => !PRIMARY_PATHS.has(item.to))

function NavItems({ items = NAV_ITEMS }) {
  return items.map((item) => (
    <NavLink
      key={item.to}
      to={item.to}
      end={item.end}
      className={({ isActive }) => (isActive ? 'nav-link is-active' : 'nav-link')}
    >
      <NavIcon name={item.icon} />
      <span className="nav-label-full">{item.label}</span>
      <span className="nav-label-short">{item.short}</span>
    </NavLink>
  ))
}

export default function Layout() {
  const { pathname } = useLocation()
  const [commandOpen, setCommandOpen] = useState(false)
  const [moreOpen, setMoreOpen] = useState(false)
  const moreActive = MORE_ITEMS.some((item) =>
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
    <div className="app-shell">
      <aside className="sidebar">
        <div className="brand">
          <BrandLogo />
          <div>
            <p className="brand-name">رتبه</p>
            <p className="brand-tag">دفتر مطالعه</p>
          </div>
          <ThemeToggle compact />
        </div>
        <nav className="sidebar-nav" aria-label="اصلی">
          {NAV_GROUPS.map((group) => (
            <div className="nav-group" key={group.label}>
              <p className="nav-group-label">{group.label}</p>
              <NavItems items={group.items} />
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

      <nav className="bottom-nav" aria-label="اصلی">
        <NavItems items={PRIMARY_ITEMS} />
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
              <NavItems items={MORE_ITEMS} />
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
