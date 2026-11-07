// C# property names are PascalCase, 
// but ASP.NET Core's default JSON serialization uses camelCase, 
// which is why the TypeScript properties are lowercase.

export interface Player {
  id: string
  name: string
  color: string
  isConnected: boolean
}

export interface GameState {
  gameSessionId: string | null
  phase: string
  currentQuestionIndex: number
  phaseStartedAt: string | null
  phaseEndsAt: string | null
  scores: Record<string, number>
}

export interface RoomSnapshot {
  players: Player[]
  game: GameState
}