import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { AuthProvider } from './auth'
import Layout from './components/Layout'
import { Gate, InvitePage, LoginPage, RegisterPage, SetupPage } from './pages/AuthPages'
import Dashboard from './pages/Dashboard'
import { CreateGroupPage, GroupDetailPage, GroupsPage } from './pages/GroupsPages'
import MembersPage from './pages/MembersPage'
import { ContributionsPage, InstalmentsPage, SavingsPage, TransactionsPage } from './pages/FinancePages'
import { AllLoansPage, GroupLoansPage, MemberLoansPage } from './pages/LoanPages'
import { AuditPage, NotificationsPage, ReportsPage, SettingsPage } from './pages/PortalPages'

export default function App() {
  return (
    <AuthProvider>
      <BrowserRouter>
        <Routes>
          <Route path="/setup" element={<SetupPage />} />
          <Route path="/login" element={<LoginPage />} />
          <Route path="/register" element={<RegisterPage />} />
          <Route path="/invite" element={<InvitePage />} />
          <Route path="/" element={<Gate><Layout /></Gate>}>
            <Route index element={<Dashboard />} />
            <Route path="stokvels" element={<GroupsPage />} />
            <Route path="stokvels/new" element={<CreateGroupPage />} />
            <Route path="stokvels/:id" element={<GroupDetailPage />} />
            <Route path="members" element={<MembersPage />} />
            <Route path="contributions" element={<ContributionsPage />} />
            <Route path="instalments" element={<InstalmentsPage />} />
            <Route path="savings" element={<SavingsPage />} />
            <Route path="group-loans" element={<GroupLoansPage />} />
            <Route path="member-loans" element={<MemberLoansPage />} />
            <Route path="loans" element={<AllLoansPage />} />
            <Route path="transactions" element={<TransactionsPage />} />
            <Route path="reports" element={<ReportsPage />} />
            <Route path="notifications" element={<NotificationsPage />} />
            <Route path="audit" element={<AuditPage />} />
            <Route path="settings" element={<SettingsPage />} />
          </Route>
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </BrowserRouter>
    </AuthProvider>
  )
}
