// @ts-nocheck
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import '@fontsource/vazirmatn/400.css'
import '@fontsource/vazirmatn/500.css'
import '@fontsource/vazirmatn/700.css'
import '@fontsource/estedad/400.css'
import '@fontsource/estedad/500.css'
import '@fontsource/estedad/600.css'
import '@fontsource/estedad/700.css'
import './index.css'
import App from './App'
import { bootTheme } from './utils/theme'

bootTheme()

createRoot(document.getElementById('root')).render(
  <StrictMode>
    <App />
  </StrictMode>,
)
