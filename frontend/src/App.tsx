import { lazy, Suspense, useState } from 'react'
import { NavLink, Navigate, Outlet, Route, Routes, useNavigate } from 'react-router-dom'
import { useQueryClient } from '@tanstack/react-query'
import { BookOpen, ChartNoAxesCombined, LayoutDashboard, LogOut, Menu, History, UserRound } from 'lucide-react'
import { AuthPage } from './AuthPage'
import { api, json } from './api'
import { Brand, Empty, ErrorState, Loading, PageTitle } from './components'
import { useSession } from './session'
const InstructorHome = lazy(() => import('./Instructor').then(m => ({ default: m.InstructorHome })))
const ExamList = lazy(() => import('./Instructor').then(m => ({ default: m.ExamList })))
const ExamEditor = lazy(() => import('./Instructor').then(m => ({ default: m.ExamEditor })))
const InstructorReports = lazy(() => import('./Instructor').then(m => ({ default: m.InstructorReports })))
import type { ReactNode } from 'react'
const StudentExams = lazy(() => import('./Student').then(m => ({ default: m.StudentExams })))
const StudentHistory = lazy(() => import('./Student').then(m => ({ default: m.StudentHistory })))
const ResultPage = lazy(() => import('./Student').then(m => ({ default: m.ResultPage })))
const StudentPlayer = lazy(() => import('./Player').then(m => ({ default: m.StudentPlayer })))
function Shell() {
  const { user, loading, error } = useSession(); const [open, setOpen] = useState(false); const [logoutError, setLogoutError] = useState<Error | null>(null); const navigate = useNavigate(); const cache = useQueryClient()
  if (loading) return <Loading/>
  if (error) return <ErrorState error={error} retry={() => { void cache.invalidateQueries({ queryKey: ['me'] }) }}/>
  if (!user) return <Navigate to="/login" replace/>
  const instructor = user.role === 'Instructor'
  return <div className="app-shell"><a className="skip-link" href="#main">Skip to content</a><aside className="sidebar" id="workspace-navigation" data-open={open}><Brand/><p className="nav-caption">{instructor ? 'TEACHING WORKSPACE' : 'LEARNING WORKSPACE'}</p><nav aria-label="Main navigation"><NavLink end to="/" onClick={() => setOpen(false)}><LayoutDashboard size={19}/>Overview</NavLink><NavLink to="/exams" onClick={() => setOpen(false)}><BookOpen size={19}/>{instructor ? 'Examinations' : 'Available exams'}</NavLink><NavLink to={instructor ? '/reports' : '/history'} onClick={() => setOpen(false)}>{instructor ? <ChartNoAxesCombined size={19}/> : <History size={19}/>} {instructor ? 'Reports & insights' : 'My results'}</NavLink><NavLink to="/profile" onClick={() => setOpen(false)}><UserRound size={19}/>My profile</NavLink></nav><div className="sidebar-bottom"><p>Knowledge, in focus.</p><small>Aperture Exams · Task 5</small></div></aside><div className="workspace"><header className="topbar"><button className="icon-button menu-toggle" aria-label="Toggle navigation" aria-expanded={open} aria-controls="workspace-navigation" onClick={() => setOpen(!open)}><Menu size={22}/></button><span className="workspace-label">{instructor ? 'Instructor' : 'Student'} workspace</span><div className="account"><span className="avatar">{user.displayName.slice(0, 1).toUpperCase()}</span><span>{user.displayName}<small>{user.role}</small></span><button className="icon-button" aria-label="Sign out" onClick={async () => { try { await api('/auth/logout', json('POST')); cache.clear(); navigate('/login') } catch (e) { setLogoutError(e as Error) } }}><LogOut size={18}/></button></div></header><main id="main" className="main-content">{logoutError && <ErrorState error={logoutError}/>}<Outlet/></main><footer className="workspace-footer">Aperture Exams <span>Thoughtful assessment, meaningful progress.</span></footer></div></div>
}
function Profile() { const { user } = useSession(); return <><PageTitle eyebrow="ACCOUNT" title="My profile" description="Your authenticated workspace identity."/><dl className="profile panel"><dt>Full name</dt><dd>{user!.displayName}</dd><dt>Email address</dt><dd>{user!.email}</dd><dt>Account role</dt><dd>{user!.role}</dd></dl></> }
function ExamDirectory() { const { user } = useSession(); return user?.role === 'Instructor' ? <ExamList/> : <StudentExams/> }
function Home() { const { user } = useSession(); return user?.role === 'Instructor' ? <InstructorHome/> : <StudentExams overview/> }
export function RoleGate({ role, children }: { role: string; children: ReactNode }) { const { user } = useSession(); return user?.role === role ? children : <ErrorState error={new Error('Your account cannot access this workspace. Choose a page from your navigation.')}/> }
export default function App() { return <Suspense fallback={<Loading/>}><Routes><Route path="/login" element={<AuthPage/>}/><Route path="/register" element={<AuthPage registerMode/>}/><Route path="attempts/:id" element={<StudentPlayer/>}/><Route element={<Shell/>}><Route index element={<Home/>}/><Route path="profile" element={<Profile/>}/><Route path="exams" element={<ExamDirectory/>}/><Route path="exams/new" element={<RoleGate role="Instructor"><ExamEditor/></RoleGate>}/><Route path="exams/:id" element={<RoleGate role="Instructor"><ExamEditor/></RoleGate>}/><Route path="history" element={<RoleGate role="Student"><StudentHistory/></RoleGate>}/><Route path="results/:id" element={<RoleGate role="Student"><ResultPage/></RoleGate>}/><Route path="reports/attempts/:id" element={<RoleGate role="Instructor"><ResultPage instructor/></RoleGate>}/><Route path="reports" element={<RoleGate role="Instructor"><InstructorReports/></RoleGate>}/><Route path="reports/:examId" element={<RoleGate role="Instructor"><InstructorReports/></RoleGate>}/><Route path="*" element={<Empty title="Page unavailable"><p>Choose a page from the workspace navigation.</p></Empty>}/></Route></Routes></Suspense> }
