function createPlayerId(): string {
  if (typeof crypto !== 'undefined' && crypto.randomUUID) {
    try {
      return crypto.randomUUID()
    } catch {
      // Fall through to the compatibility fallback.
    }
  }

  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(
    /[xy]/g,
    character => {
      const random = Math.floor(Math.random() * 16)

      return (
        character === 'x'
          ? random
          : (random & 0x3) | 0x8
      ).toString(16)
    }
  )
}

interface SavedPlayerProfile {
  playerId: string
  name: string
  color: string
}

const PLAYER_PROFILE_KEY = 'partyGamePlayerProfile'

export { createPlayerId, PLAYER_PROFILE_KEY }
export type { SavedPlayerProfile }