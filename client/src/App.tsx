import { useEffect, useRef, useState } from 'react'
import { HubConnection, HubConnectionBuilder } from '@microsoft/signalr'

const ROOM_CODE = 'test-room'

function App() {
  const isHost = window.location.pathname === '/host'

  const connectionRef = useRef<HubConnection | null>(null)

  const [connected, setConnected] = useState(false)
  const [playerName, setPlayerName] = useState('')
  const [joined, setJoined] = useState(false)
  const [players, setPlayers] = useState<string[]>([])
  const [presses, setPresses] = useState<string[]>([])
  const [error, setError] = useState('')

  useEffect(() => {
    const connection = new HubConnectionBuilder()
      .withUrl('/partyHub')
      .withAutomaticReconnect()
      .build()

    connectionRef.current = connection

    connection.on('PlayerJoined', (name: string) => {
      setPlayers(current => [...current, name])
    })

    connection.on('PlayerPressed', (name: string) => {
      setPresses(current => [name, ...current])
    })

    connection.onreconnected(() => {
      setConnected(true)

      if (isHost) {
        connection.invoke('JoinRoom', ROOM_CODE, 'Host')
      }
    })

    connection
      .start()
      .then(async () => {
        setConnected(true)

        if (isHost) {
          await connection.invoke('JoinRoom', ROOM_CODE, 'Host')
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
      await connectionRef.current?.invoke(
        'JoinRoom',
        ROOM_CODE,
        playerName.trim()
      )

      setJoined(true)
    } catch (err) {
      console.error(err)
      setError('Could not join the game.')
    }
  }

  async function pressButton() {
    try {
      await connectionRef.current?.invoke(
        'PressButton',
        ROOM_CODE,
        playerName.trim()
      )
    } catch (err) {
      console.error(err)
      setError('Could not send your button press.')
    }
  }

  if (isHost) {
    return (
      <main>
        <h1>LAN Box Party Pack</h1>
        <h2>Host screen</h2>

        <p>Connection: {connected ? 'Connected' : 'Connecting...'}</p>

        <h2>Players ({players.length})</h2>
        {players.map((player, index) => (
          <p key={index}>{player} joined!</p>
        ))}

        <h2>Button presses ({presses.length})</h2>
        {presses.map((player, index) => (
          <p key={index}>
            {player} pressed the button!
          </p>
        ))}

        {error && <p>{error}</p>}
      </main>
    )
  }

  return (
    <main>
      <h1>LAN Box Party Pack</h1>

      <p>Connection: {connected ? 'Connected' : 'Connecting...'}</p>

      {!joined ? (
        <>
          <h2>Join the game</h2>

          <input
            value={playerName}
            onChange={event => setPlayerName(event.target.value)}
            placeholder="Your name"
          />

          <button onClick={joinGame} disabled={!connected}>
            Join
          </button>
        </>
      ) : (
        <>
          <h2>You're in, {playerName}!</h2>

          <button onClick={pressButton}>
            PRESS ME!
          </button>
        </>
      )}

      {error && <p>{error}</p>}
    </main>
  )
}

export default App