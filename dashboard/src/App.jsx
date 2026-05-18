import { useEffect, useState } from 'react'
import { BrowserRouter, NavLink, Navigate, Route, Routes } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import './App.css'
import './index.css'
import { LanguageSelector } from './components/LanguageSelector'
import { HomeView } from './views/HomeView'
import { OpenApiView } from './views/OpenApiView'

const themeStorageKey = 'rigmonitor-theme'

function getInitialTheme() {
  if (typeof window === 'undefined') {
    return 'light'
  }

  const storedTheme = window.localStorage.getItem(themeStorageKey)
  if (storedTheme === 'light' || storedTheme === 'dark') {
    return storedTheme
  }

  return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light'
}

function Navigation() {
  const { t } = useTranslation()

  return (
    <nav className="nav-strip" aria-label={t('chrome.navigation')}>
      <NavLink className="nav-link" to="/overview">
        {t('nav.overview')}
      </NavLink>
      <a className="nav-link" href="/openapi" rel="noreferrer" target="_blank">
        {t('nav.swagger')}
      </a>
      <NavLink className="nav-link" to="/api">
        {t('nav.api')}
      </NavLink>
    </nav>
  )
}

function DashboardShell() {
  const { t } = useTranslation()
  const [theme, setTheme] = useState(getInitialTheme)
  const isDarkTheme = theme === 'dark'

  useEffect(() => {
    document.documentElement.dataset.theme = theme
    window.localStorage.setItem(themeStorageKey, theme)
  }, [theme])

  return (
    <div className="app-shell">
      <header className="hero-panel">
        <div className="hero-copy">
          <div className="brand-row">
            <img className="brand-mark" src={`${import.meta.env.BASE_URL}icon.png`} alt={t('chrome.logoAlt')} />
            <div className="hero-title-block">
              <h1>{t('chrome.title')}</h1>
            </div>
          </div>
          <p className="hero-text">{t('chrome.subtitle')}</p>
        </div>
        <div className="hero-actions">
          <div className="hero-controls">
            <LanguageSelector />
            <button
              aria-label={isDarkTheme ? t('chrome.themeLight') : t('chrome.themeDark')}
              className="hero-icon-button"
              onClick={() => setTheme(isDarkTheme ? 'light' : 'dark')}
              title={isDarkTheme ? t('chrome.themeLight') : t('chrome.themeDark')}
              type="button"
            >
              {isDarkTheme ? (
                <svg aria-hidden="true" viewBox="0 0 24 24">
                  <path
                    d="M12 3v2.25M12 18.75V21M5.636 5.636l1.591 1.591M16.773 16.773l1.591 1.591M3 12h2.25M18.75 12H21M5.636 18.364l1.591-1.591M16.773 7.227l1.591-1.591M15.75 12a3.75 3.75 0 1 1-7.5 0a3.75 3.75 0 0 1 7.5 0Z"
                    fill="none"
                    stroke="currentColor"
                    strokeLinecap="round"
                    strokeLinejoin="round"
                    strokeWidth="1.75"
                  />
                </svg>
              ) : (
                <svg aria-hidden="true" viewBox="0 0 24 24">
                  <path
                    d="M21 12.79A9 9 0 1 1 11.21 3a7 7 0 0 0 9.79 9.79Z"
                    fill="none"
                    stroke="currentColor"
                    strokeLinecap="round"
                    strokeLinejoin="round"
                    strokeWidth="1.75"
                  />
                </svg>
              )}
            </button>
            <a
              aria-label={t('chrome.github')}
              className="hero-icon-button"
              href="https://github.com/jchristn/RigMonitor"
              rel="noreferrer"
              target="_blank"
              title={t('chrome.github')}
            >
              <svg aria-hidden="true" viewBox="0 0 24 24">
                <path
                  d="M12 2C6.477 2 2 6.589 2 12.248c0 4.527 2.865 8.368 6.839 9.724c.5.096.682-.222.682-.493c0-.243-.009-.888-.014-1.743c-2.782.617-3.369-1.374-3.369-1.374c-.455-1.178-1.11-1.492-1.11-1.492c-.908-.636.069-.623.069-.623c1.004.072 1.532 1.055 1.532 1.055c.892 1.566 2.341 1.114 2.91.852c.091-.666.349-1.114.635-1.37c-2.221-.259-4.555-1.139-4.555-5.07c0-1.12.389-2.036 1.029-2.754c-.103-.259-.446-1.301.098-2.712c0 0 .84-.276 2.75 1.052A9.307 9.307 0 0 1 12 6.872a9.27 9.27 0 0 1 2.504.348c1.909-1.328 2.748-1.052 2.748-1.052c.546 1.411.203 2.453.1 2.712c.64.718 1.027 1.634 1.027 2.754c0 3.941-2.337 4.807-4.565 5.061c.359.319.678.947.678 1.909c0 1.378-.012 2.49-.012 2.829c0 .273.18.593.688.492A10.255 10.255 0 0 0 22 12.248C22 6.589 17.523 2 12 2Z"
                  fill="currentColor"
                />
              </svg>
            </a>
          </div>
        </div>
      </header>
      <Navigation />
      <main className="content-shell">
        <Routes>
          <Route path="/" element={<Navigate to="/overview" replace />} />
          <Route path="/overview" element={<HomeView />} />
          <Route path="/api" element={<OpenApiView />} />
          <Route path="*" element={<Navigate to="/overview" replace />} />
        </Routes>
      </main>
    </div>
  )
}

export default function App() {
  return (
    <BrowserRouter basename="/dashboard">
      <DashboardShell />
    </BrowserRouter>
  )
}
