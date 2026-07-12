/** Shared placeholder body for the stub sheets (Phase 5A foundation). */
export function SheetTodo({ screen, targetId }: { screen: string; targetId?: string }) {
  return (
    <div className="space-y-1 text-sm text-muted-foreground">
      {/* TODO: filled in Phase 5B */}
      <p>”{screen}” byggs i Phase 5B.</p>
      {targetId && <p className="font-mono text-xs opacity-70">id: {targetId}</p>}
    </div>
  )
}
