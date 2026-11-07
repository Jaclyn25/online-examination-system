import type { ReactNode } from 'react'
import { Aperture, ArrowUpRight } from 'lucide-react'
import { Link } from 'react-router-dom'
export function Brand() {
  return (
    <Link className="brand" to="/">
      <Aperture size={29} />
      <span>
        Aperture<span className="brand-sub">EXAMINATION WORKSPACE</span>
      </span>
    </Link>
  )
}
export function Loading() {
  return (
    <div role="status" aria-label="Loading workspace" className="skeleton">
      <span />
      <span />
      <span />
      <p>Loading your workspace…</p>
    </div>
  )
}
export function ErrorState({ error, retry }: { error: Error; retry?: () => void }) {
  return (
    <div className="notice error" role="alert">
      <strong>We couldn’t complete that request.</strong>
      <p>{error.message}</p>
      {retry && (
        <button className="button secondary" onClick={retry}>
          Try again
        </button>
      )}
    </div>
  )
}
export function Empty({ title, children }: { title: string; children: ReactNode }) {
  return (
    <div className="empty">
      <Aperture size={34} />
      <h2>{title}</h2>
      <div>{children}</div>
    </div>
  )
}
export function PageTitle({
  eyebrow,
  title,
  description,
  children,
}: {
  eyebrow: string
  title: string
  description?: string
  children?: ReactNode
}) {
  return (
    <header className="page-title">
      <div>
        <p className="eyebrow">{eyebrow}</p>
        <h1>{title}</h1>
        {description && <p className="muted">{description}</p>}
      </div>
      {children}
    </header>
  )
}
export function Badge({ status }: { status: string }) {
  return <span className={`badge ${status.toLowerCase()}`}>{status}</span>
}
export function TextLink({ to, children }: { to: string; children: ReactNode }) {
  return (
    <Link className="text-link" to={to}>
      {children}
      <ArrowUpRight size={16} />
    </Link>
  )
}
export const date = (value: string) =>
  new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(
    new Date(value),
  )
