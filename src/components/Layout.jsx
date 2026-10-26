import { NavLink, Outlet, useLocation } from 'react-router-dom'
import AccountBox from './AccountBox'
import BrandLogo from './BrandLogo'
import LegacyImport from './LegacyImport'
import NavIcon from './NavIcon'
import SyncNotice from './SyncNotice'
import ThemeToggle from './ThemeToggle'
import MiniTimer from './timer/MiniTimer'
import './Layout.css'

const NAV_ITEMS = [
  { to: '/', label: 'پیشخوان', short: 'خانه', icon: 'home', end: true },
  { to: '/timer', label: 'زمان‌سنج', short: 'زمان', icon: 'timer' },
  { to: '/tests', label: 'آزمون‌ها', short: 'آزمون', icon: 'tests' },
  { to: '/mistakes', label: 'اشتباهات', short: 'مرور', icon: 'mistakes' },
  { to: '/analytics', label: 'آمار', short: 'آمار', icon: 'chart' },
  { to: '/tasks', label: 'کارها', short: 'کار', icon: 'tasks' },
  { to: '/planner', label: 'برنامه', short: 'هفته', icon: 'calendar' },
  { to: '/topics', label: 'مبحث‌ها', short: 'مبحث', icon: 'topics' },
  { to: '/notes', label: 'یادداشت‌ها', short: 'یادداشت', icon: 'notes' },
  { to: '/settings', label: 'تنظیمات', short: 'تنظیم', icon: 'settings' },
]

function NavItems() {
  return NAV_ITEMS.map((item) => (
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
          <NavItems />
        </nav>
        <AccountBox />
      </aside>

      <div className="workspace">
        <header className="topbar">
          <BrandLogo className="is-sm" size={32} />
          <p className="brand-name">رتبه</p>
          <ThemeToggle compact />
          <AccountBox />
        </header>
        <main className="content">
          <LegacyImport />
          <Outlet />
        </main>
      </div>

      <SyncNotice />
      {pathname === '/timer' ? null : <MiniTimer />}

      <nav className="bottom-nav" aria-label="اصلی">
        <NavItems />
      </nav>
    </div>
  )
}
