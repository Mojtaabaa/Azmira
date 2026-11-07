// @ts-nocheck
import { useState } from 'react'
import AuthForm from '../components/auth/AuthForm'
import BrandLogo from '../components/BrandLogo'
import ThemeToggle from '../components/ThemeToggle'
import { readBrowserLegacy, summarizeLegacy } from '../utils/legacyData'
import './LoginPage.css'

const MODES = [
  { id: 'login', label: 'ورود' },
  { id: 'register', label: 'ثبت‌نام' },
]

export default function LoginPage() {
  const [mode, setMode] = useState('login')
  const legacy = readBrowserLegacy()
  const summary = summarizeLegacy(legacy)

  return (
    <div className="auth-page">
      <div className="auth-theme">
        <ThemeToggle compact />
      </div>
      <section className="auth-card">
        <div className="auth-brand auth-brand-hero">
          <BrandLogo size={112} className="is-hero" />
          <p className="brand-tag">دفتر مطالعه کنکور</p>
        </div>
        <div className="segment auth-tabs" role="tablist">
          {MODES.map((item) => (
            <button
              key={item.id}
              type="button"
              role="tab"
              aria-selected={mode === item.id}
              className={mode === item.id ? 'is-active' : ''}
              onClick={() => setMode(item.id)}
            >
              {item.label}
            </button>
          ))}
        </div>
        <AuthForm key={mode} mode={mode} />
        {legacy ? (
          <p className="auth-legacy" role="status">
            داده‌های قبلی این مرورگر پیدا شد ({summary}). بعد از ورود یا ثبت‌نام، اگر حساب خالی باشد خودکار به حسابت منتقل می‌شوند. در تنظیمات هم «وارد کردن از مرورگر» هست.
          </p>
        ) : null}
        <p className="auth-note">
          {mode === 'register'
            ? 'هر حساب دفتر جدا دارد و داده‌های تو را کسی جز خودت نمی‌بیند.'
            : 'حساب نداری؟ از تب ثبت‌نام یکی بساز.'}
        </p>
      </section>
    </div>
  )
}
