import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { Link, Navigate, useNavigate } from 'react-router-dom'
import { useQueryClient } from '@tanstack/react-query'
import { ArrowRight, CheckCircle2, Eye, EyeOff } from 'lucide-react'
import { api, json } from './api'
import { Brand, Loading } from './components'
import { useSession } from './session'
const loginSchema = z.object({
  email: z.email('Enter a valid email address.'),
  password: z.string().min(1, 'Enter your password.').max(128),
  displayName: z.string().optional(),
})
const registrationSchema = loginSchema.extend({
  displayName: z.string().trim().min(1, 'Enter your name.').max(100),
  password: z
    .string()
    .min(12, 'Use at least 12 characters.')
    .max(128)
    .regex(
      /(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z0-9])/,
      'Include uppercase, lowercase, a number and a symbol.',
    ),
})
export type AuthValues = z.infer<typeof loginSchema>
export function AuthForm({
  registerMode,
  submit,
}: {
  registerMode: boolean
  submit: (values: AuthValues) => Promise<void>
}) {
  const [error, setError] = useState('')
  const [showPassword, setShowPassword] = useState(false)
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<AuthValues>({
    resolver: zodResolver(registerMode ? registrationSchema : loginSchema),
  })
  return (
    <form
      onSubmit={handleSubmit(async (values) => {
        setError('')
        try {
          await submit(values)
        } catch (e) {
          setError(e instanceof Error ? e.message : 'Unable to connect. Please retry.')
        }
      })}
      noValidate
    >
      {registerMode && (
        <div className="field">
          <label htmlFor="displayName">Full name</label>
          <input
            id="displayName"
            autoComplete="name"
            {...register('displayName')}
            aria-invalid={!!errors.displayName}
            aria-describedby="name-error"
          />
          {errors.displayName && (
            <p className="field-error" id="name-error">
              {errors.displayName.message}
            </p>
          )}
        </div>
      )}
      <div className="field">
        <label htmlFor="email">Email address</label>
        <input
          id="email"
          type="email"
          autoComplete="email"
          {...register('email')}
          aria-invalid={!!errors.email}
          aria-describedby="email-error"
        />
        {errors.email && (
          <p className="field-error" id="email-error">
            {errors.email.message}
          </p>
        )}
      </div>
      <div className="field">
        <label htmlFor="password">Password</label>
        <div className="password-input-wrap">
          <input
            id="password"
            type={showPassword ? 'text' : 'password'}
            autoComplete={registerMode ? 'new-password' : 'current-password'}
            {...register('password')}
            aria-invalid={!!errors.password}
            aria-describedby="password-help password-error"
          />
          <button
            type="button"
            className="password-toggle"
            aria-label={showPassword ? 'Hide password' : 'Show password'}
            onClick={() => setShowPassword(!showPassword)}
          >
            {showPassword ? <EyeOff size={18} /> : <Eye size={18} />}
          </button>
        </div>
        {registerMode && (
          <p className="hint" id="password-help">
            At least 12 characters, with uppercase, lowercase, a number and a symbol.
          </p>
        )}
        {errors.password && (
          <p className="field-error" id="password-error">
            {errors.password.message}
          </p>
        )}
      </div>
      {error && (
        <p className="notice error" role="alert">
          {error}
        </p>
      )}
      <button className="button full" type="submit" disabled={isSubmitting}>
        {isSubmitting ? 'Please wait…' : registerMode ? 'Create student account' : 'Sign in'}
        <ArrowRight size={18} />
      </button>
    </form>
  )
}
export function AuthPage({ registerMode = false }: { registerMode?: boolean }) {
  const { user, loading } = useSession()
  const navigate = useNavigate()
  const cache = useQueryClient()
  if (loading) return <Loading />
  if (user) return <Navigate to="/" replace />
  return (
    <main className="auth-layout">
      <aside className="auth-story">
        <Brand />
        <div>
          <p className="eyebrow">A CLEARER WAY TO ASSESS</p>
          <h1>
            Make room for
            <br />
            what you know.
          </h1>
          <p>
            Thoughtful assessments. Focused learning.
            <br />A clear picture of your progress.
          </p>
          <div className="auth-illustration" aria-hidden="true">
            <div className="orbit" />
            <div className="illustration-sheet">
              <span>KNOWLEDGE IN FOCUS</span>
              <h3>
                Your next chapter
                <br />
                starts here.
              </h3>
              <div />
              <div />
              <CheckCircle2 size={36} />
            </div>
          </div>
        </div>
        <p className="auth-foot">Built for clarity. Designed for confidence.</p>
      </aside>
      <section className="auth-form-panel">
        <div className="mobile-brand">
          <Brand />
        </div>
        <div className="auth-form">
          <p className="eyebrow">YOUR APERTURE WORKSPACE</p>
          <h1>{registerMode ? 'Start your learning journey' : 'Welcome back'}</h1>
          <p className="muted">
            {registerMode
              ? 'Create a student account to take assessments and track your progress.'
              : 'Sign in to continue to your examination workspace.'}
          </p>
          <AuthForm
            key={String(registerMode)}
            registerMode={registerMode}
            submit={async (values) => {
              if (registerMode) await api('/auth/register', json('POST', values))
              await api(
                '/auth/login',
                json('POST', { email: values.email, password: values.password }),
              )
              await cache.invalidateQueries({ queryKey: ['me'] })
              navigate('/')
            }}
          />
          <p className="auth-switch">
            {registerMode ? 'Already have an account?' : 'New to Aperture?'}{' '}
            <Link to={registerMode ? '/login' : '/register'}>
              {registerMode ? 'Sign in' : 'Create a student account'}
            </Link>
          </p>
          <div className="auth-note">
            <CheckCircle2 size={18} />
            <p>
              {registerMode
                ? 'Instructor access is provisioned separately by your institution.'
                : 'Your session is protected. Your progress stays with you.'}
            </p>
          </div>
        </div>
        <footer>
          Aperture Exams <span>·</span> Learn with perspective
        </footer>
      </section>
    </main>
  )
}
