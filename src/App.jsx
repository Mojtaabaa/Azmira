import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import AuthGate from './components/auth/AuthGate'
import AuthProvider from './components/auth/AuthProvider'
import Layout from './components/Layout'
import AnalyticsPage from './pages/AnalyticsPage'
import DashboardPage from './pages/DashboardPage'
import MistakesPage from './pages/MistakesPage'
import PlannerPage from './pages/PlannerPage'
import SettingsPage from './pages/SettingsPage'
import NotesPage from './features/notes/NotesPage'
import TopicsPage from './pages/TopicsPage'
import TasksPage from './pages/TasksPage'
import TestsPage from './pages/TestsPage'
import TimerPage from './pages/TimerPage'

export default function App() {
  return (
    <AuthProvider>
      <AuthGate>
        <BrowserRouter>
          <Routes>
            <Route element={<Layout />}>
              <Route index element={<DashboardPage />} />
              <Route path="timer" element={<TimerPage />} />
              <Route path="tests" element={<TestsPage />} />
              <Route path="mistakes" element={<MistakesPage />} />
              <Route path="analytics" element={<AnalyticsPage />} />
              <Route path="tasks" element={<TasksPage />} />
              <Route path="planner" element={<PlannerPage />} />
              <Route path="topics" element={<TopicsPage />} />
              <Route path="notes" element={<NotesPage />} />
              <Route path="settings" element={<SettingsPage />} />
              <Route path="*" element={<Navigate to="/" replace />} />
            </Route>
          </Routes>
        </BrowserRouter>
      </AuthGate>
    </AuthProvider>
  )
}
