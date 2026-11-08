// @ts-nocheck
import { lazy, Suspense } from 'react'
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import AuthGate from './components/auth/AuthGate'
import AuthProvider from './components/auth/AuthProvider'
import Layout from './components/Layout'
import DashboardPage from './pages/DashboardPage'
import TimerPage from './pages/TimerPage'

const AnalyticsPage = lazy(() => import('./pages/AnalyticsPage'))
const DataPage = lazy(() => import('./pages/DataPage'))
const MistakesPage = lazy(() => import('./pages/MistakesPage'))
const PlannerPage = lazy(() => import('./pages/PlannerPage'))
const SettingsPage = lazy(() => import('./pages/SettingsPage'))
const NotesPage = lazy(() => import('./features/notes/NotesPage'))
const TopicsPage = lazy(() => import('./pages/TopicsPage'))
const TasksPage = lazy(() => import('./pages/TasksPage'))
const TestsPage = lazy(() => import('./pages/TestsPage'))
const ProgressPage = lazy(() => import('./pages/ProgressPage'))
const AdminGamificationPage = lazy(() => import('./pages/AdminGamificationPage'))
const LeaderboardsPage = lazy(() => import('./pages/LeaderboardsPage'))
const TeamsPage = lazy(() => import('./pages/TeamsPage'))
const CompetitionsPage = lazy(() => import('./pages/CompetitionsPage'))
const StorePage = lazy(() => import('./pages/StorePage'))
const AssessmentsPage = lazy(() => import('./pages/AssessmentsPage'))
const MasteryPage = lazy(() => import('./pages/MasteryPage'))
const ReviewsPage = lazy(() => import('./pages/ReviewsPage'))
const AiCoachPage = lazy(() => import('./pages/AiCoachPage'))
const SchoolAdminPage = lazy(() => import('./pages/SchoolAdminPage'))
const TeacherPortalPage = lazy(() => import('./pages/TeacherPortalPage'))
const ParentPortalPage = lazy(() => import('./pages/ParentPortalPage'))
const SchoolAnalyticsPage = lazy(() => import('./pages/SchoolAnalyticsPage'))
const PlatformAdminPage = lazy(() => import('./pages/PlatformAdminPage'))
const PillarHubPage = lazy(() => import('./pages/PillarHubPage'))
const DailyMissionsPage = lazy(() => import('./pages/DailyMissionsPage'))
const LearningJourneyPage = lazy(() => import('./pages/LearningJourneyPage'))

function PageFallback() {
  return <p className="page-fallback">در حال باز شدن…</p>
}

function LearnHub() {
  return <PillarHubPage pillarId="learn" />
}

function ImproveHub() {
  return <PillarHubPage pillarId="improve" />
}

function AchieveHub() {
  return <PillarHubPage pillarId="achieve" />
}

export default function App() {
  return (
    <AuthProvider>
      <AuthGate>
        <BrowserRouter>
          <Routes>
            <Route element={<Layout />}>
              <Route index element={<DashboardPage />} />
              <Route path="learn" element={<Suspense fallback={<PageFallback />}><LearnHub /></Suspense>} />
              <Route path="improve" element={<Suspense fallback={<PageFallback />}><ImproveHub /></Suspense>} />
              <Route path="achieve" element={<Suspense fallback={<PageFallback />}><AchieveHub /></Suspense>} />
              <Route path="missions" element={<Suspense fallback={<PageFallback />}><DailyMissionsPage /></Suspense>} />
              <Route path="journey" element={<Suspense fallback={<PageFallback />}><LearningJourneyPage /></Suspense>} />
              <Route path="timer" element={<TimerPage />} />
              <Route path="tests" element={<Suspense fallback={<PageFallback />}><TestsPage /></Suspense>} />
              <Route path="mistakes" element={<Suspense fallback={<PageFallback />}><MistakesPage /></Suspense>} />
              <Route path="analytics" element={<Suspense fallback={<PageFallback />}><AnalyticsPage /></Suspense>} />
              <Route path="progress" element={<Suspense fallback={<PageFallback />}><ProgressPage /></Suspense>} />
              <Route path="leaderboards" element={<Suspense fallback={<PageFallback />}><LeaderboardsPage /></Suspense>} />
              <Route path="teams" element={<Suspense fallback={<PageFallback />}><TeamsPage /></Suspense>} />
              <Route path="competitions" element={<Suspense fallback={<PageFallback />}><CompetitionsPage /></Suspense>} />
              <Route path="store" element={<Suspense fallback={<PageFallback />}><StorePage /></Suspense>} />
              <Route path="assessments" element={<Suspense fallback={<PageFallback />}><AssessmentsPage /></Suspense>} />
              <Route path="mastery" element={<Suspense fallback={<PageFallback />}><MasteryPage /></Suspense>} />
              <Route path="reviews" element={<Suspense fallback={<PageFallback />}><ReviewsPage /></Suspense>} />
              <Route path="ai-coach" element={<Suspense fallback={<PageFallback />}><AiCoachPage /></Suspense>} />
              <Route path="platform" element={<Suspense fallback={<PageFallback />}><PlatformAdminPage /></Suspense>} />
              <Route path="school" element={<Suspense fallback={<PageFallback />}><SchoolAdminPage /></Suspense>} />
              <Route path="teacher" element={<Suspense fallback={<PageFallback />}><TeacherPortalPage /></Suspense>} />
              <Route path="parent" element={<Suspense fallback={<PageFallback />}><ParentPortalPage /></Suspense>} />
              <Route path="school/analytics" element={<Suspense fallback={<PageFallback />}><SchoolAnalyticsPage /></Suspense>} />
              <Route path="admin/gamification" element={<Suspense fallback={<PageFallback />}><AdminGamificationPage /></Suspense>} />
              <Route path="data" element={<Suspense fallback={<PageFallback />}><DataPage /></Suspense>} />
              <Route path="tasks" element={<Suspense fallback={<PageFallback />}><TasksPage /></Suspense>} />
              <Route path="planner" element={<Suspense fallback={<PageFallback />}><PlannerPage /></Suspense>} />
              <Route path="topics" element={<Suspense fallback={<PageFallback />}><TopicsPage /></Suspense>} />
              <Route path="notes" element={<Suspense fallback={<PageFallback />}><NotesPage /></Suspense>} />
              <Route path="settings" element={<Suspense fallback={<PageFallback />}><SettingsPage /></Suspense>} />
              <Route path="*" element={<Navigate to="/" replace />} />
            </Route>
          </Routes>
        </BrowserRouter>
      </AuthGate>
    </AuthProvider>
  )
}
