import { useEffect, useRef, useState } from 'react'
import { playerColors } from './lib/playerColors'
import { createPlayerId, PLAYER_PROFILE_KEY } from './lib/utils'
import type { SavedPlayerProfile } from './lib/utils'
import {
  HubConnection,
  HubConnectionBuilder
} from '@microsoft/signalr'

import type { AnswerSubmission, RoomSnapshot } from './lib/types'

function App() {
  const isHost = window.location.pathname === '/host'

  const connectionRef = useRef<HubConnection | null>(null)
  const currentQuestionKeyRef = useRef<string | null>(null)

  const [connected, setConnected] = useState(false)
  const [playerName, setPlayerName] = useState('')
  const [playerColor, setPlayerColor] = useState<string>(
    playerColors[14].hex
  )
  const [joined, setJoined] = useState(false)

  const [room, setRoom] = useState<RoomSnapshot | null>(null)
  const [error, setError] = useState('')
  const [playingQuestionKey, setPlayingQuestionKey] = useState<string | null>(null)
  const [answer, setAnswer] = useState('')
  const [answerResult, setAnswerResult] = useState<AnswerSubmission | null>(null)
  const [playerAnswers, setPlayerAnswers] = useState<AnswerSubmission[]>([])

  const currentClipKey = room?.game.gameSessionId
    ? `${room.game.gameSessionId}-${room.game.currentQuestionIndex}`
    : null

  useEffect(() => {
    const connection = new HubConnectionBuilder()
      .withUrl('/partyHub')
      .withAutomaticReconnect()
      .build()

    restorePlayer()

    async function restorePlayer() {
      const savedProfile = localStorage.getItem(PLAYER_PROFILE_KEY)

      if (!savedProfile) return

      let profile: SavedPlayerProfile

      try {
        profile = JSON.parse(savedProfile)
      } catch {
        console.error('Saved player profile is invalid.')
        localStorage.removeItem(PLAYER_PROFILE_KEY)
        return
      }

      if (!profile.playerId || !profile.name || !profile.color) {
        console.error('Saved player profile is incomplete.')
        localStorage.removeItem(PLAYER_PROFILE_KEY)
        return
      }

      setPlayerName(profile.name)
      setPlayerColor(profile.color)

      if (connection.state !== 'Connected') {
        console.warn('Cannot restore player: SignalR is not connected.')
        return
      }

      try {
        await connection.invoke(
          'JoinRoom',
          profile.playerId,
          profile.name,
          profile.color
        )

        setJoined(true)
        setError('')
      } catch (err) {
        console.error('Player restoration failed:', err)

        setError(
          err instanceof Error
            ? `Could not rejoin: ${err.message}`
            : `Could not rejoin: ${String(err)}`
        )
      }
    }

    connectionRef.current = connection

    connection.on('RoomUpdated', (snapshot: RoomSnapshot) => {
      const nextQuestionKey = snapshot.game.gameSessionId
        ? `${snapshot.game.gameSessionId}-${snapshot.game.currentQuestionIndex}`
        : null

      if (currentQuestionKeyRef.current !== nextQuestionKey) {
        currentQuestionKeyRef.current = nextQuestionKey
        setPlayerAnswers([])
        setAnswerResult(null)
      }

      setRoom(snapshot)
    })

    connection.on('AnswerResult', (result: AnswerSubmission) => {
      setAnswerResult(result)
    })

    connection.on('PlayerAnswerSubmitted', (submission: AnswerSubmission) => {
      setPlayerAnswers(previous => [submission, ...previous])
    })

    connection.onreconnecting(() => {
      setConnected(false)
    })

    connection.onreconnected(async () => {
      setConnected(true)

      try {
        if (isHost) {
          await connection.invoke('WatchRoom')
        } else {
          await restorePlayer()
        }
      } catch (err) {
        console.error('Reconnection handling failed:', err)
        setError('Reconnected, but could not restore the room session.')
      }
    })

    connection.onclose(() => {
      setConnected(false)
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
        console.error('SignalR connection failed:', err)
        setConnected(false)
        setError('Could not connect to the game server.')
      })

    return () => {
      connection.stop()
    }
  }, [isHost])

  async function joinGame() {
    if (!playerName.trim()) return

    const connection = connectionRef.current

    if (!connection || connection.state !== 'Connected') {
      setError('Not connected to the game server. Please wait and try again.')
      return
    }

    try {
      let playerId = localStorage.getItem('partyGamePlayerId')

      if (!playerId) {
        playerId = createPlayerId()
        localStorage.setItem('partyGamePlayerId', playerId)
      }

      await connection.invoke(
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
      setError('')
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

  async function startQuestion() {
    try {
      await connectionRef.current?.invoke('StartQuestion')
    } catch (err) {
      console.error(err)
      setError('Could not start question.')
    }
  }

  async function revealAnswer() {
    try {
      await connectionRef.current?.invoke('RevealAnswer')
    } catch (err) {
      console.error(err)
      setError('Could not reveal answer.')
    }
  }

  async function advanceQuestion() {
    try {
      await connectionRef.current?.invoke('AdvanceQuestion')
    } catch (err) {
      console.error(err)
      setError('Could not advance question.')
    }
  }

  async function submitAnswer() {
    const trimmedAnswer = answer.trim()

    if (!trimmedAnswer) return

    try {
      setAnswerResult(null)

      await connectionRef.current?.invoke(
        'SubmitAnswer',
        trimmedAnswer
      )

      setAnswer('')
    } catch (err) {
      console.error(err)
      setError('Could not submit answer.')
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

        <h2>Answers</h2>

        {playerAnswers.length === 0 ? (
          <p>No answers submitted yet.</p>
        ) : (
          playerAnswers.map((submission, index) => (
            <div
              key={`${submission.playerId}-${index}`}
              style={{
                border: '1px solid #888',
                borderRadius: 8,
                padding: 12,
                marginBottom: 8
              }}
            >
              <span
                style={{
                  display: 'inline-block',
                  width: 12,
                  height: 12,
                  backgroundColor: submission.playerColor,
                  borderRadius: '50%',
                  marginRight: 8
                }}
              />

              **{submission.playerName}** — {submission.answer}

              <strong
                style={{
                  display: 'block',
                  color: submission.isCorrect ? 'green' : 'red',
                  marginTop: 4
                }}
              >
                {submission.isCorrect ? 'Correct!' : 'Incorrect'}
              </strong>
            </div>
          ))
        )}

        <button
          onClick={startNewGame}
          disabled={!connected || (room?.players.length ?? 0) === 0}
        >
          Start new game
        </button>

        <h2>Game</h2>

        {room?.game.gameSessionId ? (
          <>
            <h3>{room.game.questionSetTitle}</h3>

            <p>
              Phase: {room.game.phase}
            </p>

            <p>
              Question {
                room.game.currentQuestionIndex + 1
              } of {room.game.totalQuestions}
            </p>

            <h3>
              {room.game.currentQuestion?.questionText}
            </h3>

            {room.game.currentQuestion && currentClipKey && (
              <>
                <p>
                  Video: {room.game.currentQuestion.questionVideo.youTubeId}
                </p>

                <p>
                  Clip: {
                    room.game.currentQuestion.questionVideo.startTime
                  }s – {
                    room.game.currentQuestion.questionVideo.endTime
                  }s
                </p>

                <button
                  onClick={() => setPlayingQuestionKey(currentClipKey)}
                >
                  Play clip
                </button>

                {room?.game.phase === 'QuestionIntro' && (
                  <button onClick={startQuestion}>
                    Start question
                  </button>
                )}

                {room?.game.phase === 'Playing' && (
                  <button onClick={revealAnswer}>
                    Reveal answer
                  </button>
                )}

                {room?.game.phase === 'Reveal' && (
                  <button onClick={advanceQuestion}>
                    Next question
                  </button>
                )}

                {room?.game.phase === 'Reveal' && (
                  <div>
                    <h2>{room.game.revealedAnswer}</h2>
                    {room.game.revealedAnswerSubtext && (
                      <p>{room.game.revealedAnswerSubtext}</p>
                    )}
                  </div>
                )}

                {room?.game.phase === 'Results' && (
                  <h2>Game complete!</h2>
                )}

                {playingQuestionKey === currentClipKey && (
                  <div
                    style={{
                      width: '100%',
                      maxWidth: 800,
                      aspectRatio: '16 / 9',
                      marginTop: 16
                    }}
                  >
                    <iframe
                      key={currentClipKey}
                      width="100%"
                      height="100%"
                      src={`https://www.youtube.com/embed/${room.game.currentQuestion.questionVideo.youTubeId
                        }?autoplay=1&start=${room.game.currentQuestion.questionVideo.startTime
                        }&end=${room.game.currentQuestion.questionVideo.endTime
                        }&controls=0&rel=0&playsinline=1`}
                      title="Question video"
                      allow="autoplay; encrypted-media; picture-in-picture"
                      allowFullScreen
                      style={{ border: 0 }}
                    />
                  </div>
                )}
              </>
            )}
          </>
        ) : (
          <p>No game started yet.</p>
        )}

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

          {room?.game.phase === 'Playing' ||
            room?.game.phase === 'Overtime' ? (
            <>
              <h3>{room.game.currentQuestion?.questionText}</h3>

              <form
                onSubmit={event => {
                  event.preventDefault()
                  void submitAnswer()
                }}
              >
                <input
                  value={answer}
                  onChange={event => setAnswer(event.target.value)}
                  placeholder="Type your answer..."
                  autoComplete="off"
                />

                <button
                  type="submit"
                  disabled={!answer.trim() || !connected}
                >
                  Submit answer
                </button>
              </form>

              {answerResult && (
                <h2
                  style={{
                    color: answerResult.isCorrect ? 'green' : 'red'
                  }}
                >
                  {answerResult.isCorrect ? 'Correct!' : 'Incorrect!'}
                </h2>
              )}
            </>
          ) : (
            <p>
              {room?.game.phase === 'QuestionIntro'
                ? 'Get ready! The next question is starting.'
                : room?.game.phase === 'Reveal'
                  ? 'The answer has been revealed.'
                  : room?.game.phase === 'Results'
                    ? 'Game complete!'
                    : 'Waiting for the host to start a game...'}
            </p>
          )}
        </>
      )}

      {error && <p>{error}</p>}
    </main>
  )
}

export default App