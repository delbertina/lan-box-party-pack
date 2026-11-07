import { useEffect, useRef, useState } from 'react'
import { playerColors } from './lib/playerColors'
import { createPlayerId, PLAYER_PROFILE_KEY } from './lib/utils'
import type { SavedPlayerProfile } from './lib/utils'
import {
  HubConnection,
  HubConnectionBuilder
} from '@microsoft/signalr'

import type { RoomSnapshot } from './lib/types'

function App() {
  const isHost = window.location.pathname === '/host'

  const connectionRef = useRef<HubConnection | null>(null)

  const [connected, setConnected] = useState(false)
  const [playerName, setPlayerName] = useState('')
  const [playerColor, setPlayerColor] = useState<string>(
    playerColors[14].hex
  )
  const [joined, setJoined] = useState(false)

  const [room, setRoom] = useState<RoomSnapshot | null>(null)
  const [error, setError] = useState('')

  useEffect(() => {
    const connection = new HubConnectionBuilder()
      .withUrl('/partyHub')
      .withAutomaticReconnect()
      .build()

    restorePlayer()

    async function restorePlayer() {
      const savedProfile = localStorage.getItem(PLAYER_PROFILE_KEY)

      if (!savedProfile) return

      try {
        const profile: SavedPlayerProfile = JSON.parse(savedProfile)

        if (!profile.playerId || !profile.name || !profile.color) {
          return
        }

        setPlayerName(profile.name)
        setPlayerColor(profile.color)

        await connection.invoke(
          'JoinRoom',
          profile.playerId,
          profile.name,
          profile.color
        )

        setJoined(true)
      } catch (err) {
        console.error('Could not restore player:', err)
        setError('Could not automatically rejoin. Please join manually.')
      }
    }

    connectionRef.current = connection

    connection.on('RoomUpdated', (snapshot: RoomSnapshot) => {
      setRoom(snapshot)
    })

    connection.onreconnected(async () => {
      setConnected(true)

      if (isHost) {
        await connection.invoke('WatchRoom')
      } else {
        await restorePlayer()
      }
    })

    connection
      .start()
      .then(async () => {
        setConnected(true)

        if (isHost) {
          await connection.invoke('WatchRoom')
        } else {
          await restorePlayer()
        }
      })
      .catch(err => {
        console.error(err)
        setError('Could not connect to the game server.')
      })

    return () => {
      connection.stop()
    }
  }, [isHost])

  async function joinGame() {
    if (!playerName.trim()) return

    try {
      let playerId = localStorage.getItem('partyGamePlayerId')

      if (!playerId) {
        playerId = createPlayerId()
        localStorage.setItem('partyGamePlayerId', playerId)
      }


      await connectionRef.current?.invoke(
        'JoinRoom',
        playerId,
        playerName.trim(),
        playerColor
      )

      const profile: SavedPlayerProfile = {
        playerId,
        name: playerName.trim(),
        color: playerColor
      }

      localStorage.setItem(
        PLAYER_PROFILE_KEY,
        JSON.stringify(profile)
      )

      setJoined(true)
    } catch (err) {
      console.error('Join failed:', err)
      setError(
        err instanceof Error
          ? `Could not join: ${err.message}`
          : `Could not join: ${String(err)}`
      )
    }
  }

  async function startNewGame() {
    try {
      await connectionRef.current?.invoke('StartNewGame')
    } catch (err) {
      console.error(err)
      setError('Could not start the game.')
    }
  }

  if (isHost) {
    return (
      <main>
        <h1>LAN Box Party Pack</h1>
        <h2>Host screen</h2>

        <p>
          Connection: {connected ? 'Connected' : 'Connecting...'}
        </p>

        <h2>
          Players ({room?.players.length ?? 0})
        </h2>

        {room?.players.map(player => (
          <p key={player.id}>
            <span
              style={{
                display: 'inline-block',
                width: 12,
                height: 12,
                backgroundColor: player.color,
                borderRadius: '50%',
                marginRight: 8
              }}
            />

            {player.name} — {player.isConnected ? 'Connected' : 'Disconnected'}
          </p>
        ))}

        <button
          onClick={startNewGame}
          disabled={!connected || (room?.players.length ?? 0) === 0}
        >
          Start new game
        </button>

        <h2>Game</h2>
        <p>Phase: {room?.game.phase ?? 'Loading...'}</p>

        {error && <p>{error}</p>}
      </main>
    )
  }

  return (
    <main>
      <h1>LAN Box Party Pack</h1>

      <p>
        Connection: {connected ? 'Connected' : 'Connecting...'}
      </p>

      {!joined ? (
        <>
          <h2>Join the game</h2>

          <input
            value={playerName}
            onChange={event => setPlayerName(event.target.value)}
            placeholder="Your name"
          />

          <div style={{ marginTop: 20 }}>
            <h3>Choose your color</h3>

            <div
              style={{
                display: 'grid',
                gridTemplateColumns: 'repeat(8, minmax(0, 1fr))',
                gap: 10,
                maxWidth: 360
              }}
            >
              {playerColors.map(color => (
                <button
                  key={color.hex}
                  type="button"
                  title={color.name}
                  aria-label={color.name}
                  aria-pressed={playerColor === color.hex}
                  onClick={() => setPlayerColor(color.hex)}
                  style={{
                    width: '100%',
                    aspectRatio: '1',
                    backgroundColor: color.hex,
                    borderRadius: '50%',
                    border: playerColor === color.hex
                      ? '4px solid #ffffff'
                      : '2px solid #777777',
                    outline: playerColor === color.hex
                      ? '2px solid #333333'
                      : 'none',
                    cursor: 'pointer',
                    padding: 0
                  }}
                />
              ))}
            </div>

            <p>
              Selected: {
                playerColors.find(color => color.hex === playerColor)?.name
              }
            </p>
          </div>

          <button
            onClick={joinGame}
            disabled={!connected || !playerName.trim()}
          >
            Join
          </button>
        </>
      ) : (
        <>
          <h2>You're in, {playerName}!</h2>
          <p>Waiting for the host to start a game...</p>
        </>
      )}

      {error && <p>{error}</p>}
    </main>
  )
}

export default App