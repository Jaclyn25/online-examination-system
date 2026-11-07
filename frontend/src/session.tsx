import { createContext, useContext } from 'react'
import type { ReactNode } from 'react'
import { useQuery } from '@tanstack/react-query'
import { api, ApiError } from './api'
import type { User } from './types'
const Session = createContext<{ user: User | null | undefined; loading: boolean; error: Error | null }>({ user: undefined, loading: true, error: null })
export function SessionProvider({ children }: { children: ReactNode }) {
  const query = useQuery({ queryKey: ['me'], queryFn: async () => { try { return await api<User>('/auth/me') } catch (e) { if (e instanceof ApiError && e.status === 401) return null; throw e } }, retry: false })
  return <Session.Provider value={{ user: query.data, loading: query.isPending, error: query.error }}>{children}</Session.Provider>
}
export const useSession = () => useContext(Session)
