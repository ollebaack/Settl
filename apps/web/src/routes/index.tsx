import { createFileRoute } from '@tanstack/react-router'
import { useQuery } from '@tanstack/react-query'
import { apiFetch } from '@/lib/api'

export const Route = createFileRoute('/')({
  component: HomePage,
})

function HomePage() {
  const { data, isPending } = useQuery({
    queryKey: ['health'],
    queryFn: () => apiFetch<{ status: string }>('/health'),
  })

  return (
    <main className="flex min-h-dvh flex-col items-center justify-center gap-2 p-6">
      <h1 className="text-3xl font-semibold tracking-tight">Settl</h1>
      <p className="text-muted-foreground text-sm">
        API: {isPending ? 'checking…' : (data?.status ?? 'unreachable')}
      </p>
    </main>
  )
}
