import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { RouterProvider, createRouter } from '@tanstack/react-router'
import { ThemeProvider } from 'next-themes'
import { routeTree } from './routeTree.gen'
import { DevUserProvider } from '@/lib/dev-user'
import { ActiveHouseholdProvider } from '@/lib/active-household'
import './index.css'

const queryClient = new QueryClient({
  defaultOptions: {
    queries: { staleTime: 30_000, retry: 1 },
  },
})

const router = createRouter({ routeTree })

declare module '@tanstack/react-router' {
  interface Register {
    router: typeof router
  }
}

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <ThemeProvider attribute="class" defaultTheme="system" enableSystem>
        <DevUserProvider>
          <ActiveHouseholdProvider>
            <RouterProvider router={router} />
          </ActiveHouseholdProvider>
        </DevUserProvider>
      </ThemeProvider>
    </QueryClientProvider>
  </StrictMode>,
)
