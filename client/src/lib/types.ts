// C# property names are PascalCase, 
// but ASP.NET Core's default JSON serialization uses camelCase, 
// which is why the TypeScript properties are lowercase.

export interface Player {
  id: string
  name: string
  color: string
  nameLocked: boolean
  isConnected: boolean
}

export interface PlayerModerationInfo {
  id: string
  name: string
  isConnected: boolean
  nameLocked: boolean
  ipAddress: string | null
}

export type PunishmentType = 'Timeout' | 'Ban'

export interface PlayerPunishment {
  id: string
  playerId: string
  playerName: string
  ipAddress: string | null
  type: PunishmentType
  reason: string
  createdAt: string
  expiresAt: string | null
  revokedAt: string | null
}

export interface ModerationSnapshot {
  players: PlayerModerationInfo[]
  punishments: PlayerPunishment[]
}

export interface QuestionVideo {
  youTubeId: string
  startTime: number
  endTime: number
}

export interface QuestionPrompt {
  questionText: string
  questionVideo: QuestionVideo
}

export interface GameState {
  gameSessionId: string | null
  phase: string
  currentQuestionIndex: number
  phaseStartedAt: string | null
  phaseEndsAt: string | null
  phasePausedAt: string | null
  scores: Record<string, number>

  questionSetId: number
  questionSetTitle: string
  totalQuestions: number
  currentQuestion: QuestionPrompt | null

  settings: {
    guessingDurationSeconds: number
    overtimeDurationSeconds: number
    incorrectGuessLockoutMilliseconds: number
    correctAnswerPoints: number[]
    remainingCorrectAnswerPoints: number
  }

  revealedAnswer: string | null
  revealedAnswerSubtext: string | null
}

export interface RoomSnapshot {
  players: Player[]
  game: GameState
}

export interface AnswerSubmission {
  playerId: string
  playerName: string
  playerColor: string
  answer: string
  isCorrect: boolean
  pointsAwarded: number
  correctAnswerNumber: number | null
  alreadyAnsweredCorrectly: boolean
}

export interface QuestionSetSummary {
  id: number
  fileName: string
  title: string
  description: string
  rating: number
  questionCount: number
}

export interface AnswerOption {
  id: number
  displayName: string
  category: string
}