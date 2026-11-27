import { useEffect, useRef, useState } from 'react'
import { playerColors } from './lib/playerColors'
import { createPlayerId, PLAYER_PROFILE_KEY } from './lib/utils'
import type { SavedPlayerProfile } from './lib/utils'
import {
  HubConnection,
  HubConnectionBuilder
} from '@microsoft/signalr'

import type { AnswerOption, AnswerSubmission, ModerationSnapshot, QuestionSetSummary, RoomSnapshot } from './lib/types'
import ReactPlayerImport from 'react-player'
// todo fix this by updating react-player to a version that has proper ESM support, then remove this workaround
const ReactPlayer =
  (ReactPlayerImport as unknown as { default?: typeof ReactPlayerImport }).default
  ?? ReactPlayerImport

function App() {
  const isHost = window.location.pathname === '/host'

  const connectionRef = useRef<HubConnection | null>(null)
  const currentQuestionKeyRef = useRef<string | null>(null)
  const moderationPlayersKeyRef = useRef('')
  const playerRef = useRef<ReactPlayerImport | null>(null)
  const playbackIsAdvancingRef = useRef(false)

  const [connected, setConnected] = useState(false)
  const [playerName, setPlayerName] = useState('')
  const [playerColor, setPlayerColor] = useState<string>(
    playerColors[14].hex
  )
  const [joined, setJoined] = useState(false)

  const [room, setRoom] = useState<RoomSnapshot | null>(null)
  const [moderation, setModeration] = useState<ModerationSnapshot | null>(null)
  const [playerNameDrafts, setPlayerNameDrafts] = useState<Record<string, string>>({})
  const [scoreAdjustment, setScoreAdjustment] = useState(5)
  const [moderationReason, setModerationReason] = useState('')
  const [timeoutMinutes, setTimeoutMinutes] = useState(5)
  const [error, setError] = useState('')
  const [playingQuestionKey, setPlayingQuestionKey] = useState<string | null>(null)
  const [playbackStatus, setPlaybackStatus] = useState('Ready')
  const [playbackTime, setPlaybackTime] = useState(0)
  const [isVideoPlaying, setIsVideoPlaying] = useState(false)
  const [answer, setAnswer] = useState('')
  const [answerResult, setAnswerResult] = useState<AnswerSubmission | null>(null)
  const [playerAnswers, setPlayerAnswers] = useState<AnswerSubmission[]>([])

  const [questionSets, setQuestionSets] = useState<QuestionSetSummary[]>([])
  const [selectedQuestionSet, setSelectedQuestionSet] = useState('')
  const [countdownNow, setCountdownNow] = useState(Date.now())
  const [answerOptions, setAnswerOptions] = useState<AnswerOption[]>([])
  const [selectedAnswerOptionId, setSelectedAnswerOptionId] = useState<number | null>(null)

  async function refreshModerationState(connection: HubConnection) {
    try {
      const state = await connection.invoke<ModerationSnapshot>('GetModerationState')
      setModeration(state)
    } catch (err) {
      console.error('Could not load moderation state:', err)
      setError('Could not load player moderation details.')
    }
  }

  const currentClipKey = room?.game.gameSessionId
    ? `${room.game.gameSessionId}-${room.game.currentQuestionIndex}`
    : null

  const sortedPlayers = room
    ? [...room.players].sort(
      (a, b) =>
        (room.game.scores[b.id] ?? 0) -
        (room.game.scores[a.id] ?? 0)
    )
    : []

  const phaseDurationSeconds =
    room?.game.phase === 'Playing'
      ? room.game.settings.guessingDurationSeconds
      : room?.game.phase === 'Overtime'
        ? room.game.settings.overtimeDurationSeconds
        : 0

  const phaseRemainingSeconds = room?.game.phaseEndsAt
    ? Math.max(
      0,
      (
        Date.parse(room.game.phaseEndsAt) -
        (
          room.game.phasePausedAt
            ? Date.parse(room.game.phasePausedAt)
            : countdownNow
        )
      ) / 1000
    )
    : 0

  const phaseProgress = phaseDurationSeconds > 0
    ? Math.min(1, phaseRemainingSeconds / phaseDurationSeconds)
    : 0

  const filteredAnswerOptions = answer.trim()
    ? answerOptions
      .filter(option =>
        option.displayName.toLowerCase().includes(answer.toLowerCase())
      )
      .slice(0, 10)
    : []

  const countdownIndicator = (
    room?.game.phase === 'Playing' ||
    room?.game.phase === 'Overtime'
  ) && (
      <div style={{ maxWidth: 800, margin: '16px 0' }}>
        <div style={{
          display: 'flex',
          justifyContent: 'space-between',
          marginBottom: 6
        }}>
          <strong>
            {room.game.phase === 'Playing' ? 'Time remaining' : 'Overtime'}
          </strong>
          <strong>{phaseRemainingSeconds.toFixed(1)}s</strong>
        </div>

        <div style={{
          width: '100%',
          height: 16,
          backgroundColor: '#555',
          borderRadius: 8,
          overflow: 'hidden'
        }}>
          <div style={{
            width: `${phaseProgress * 100}%`,
            height: '100%',
            backgroundColor: '#e53935',
            transition: 'width 100ms linear'
          }} />
        </div>
      </div>
    )

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

      if (isHost) {
        const playersKey = snapshot.players
          .map(player => `${player.id}:${player.name}:${player.nameLocked}:${player.isConnected}`)
          .sort()
          .join('|')

        if (playersKey !== moderationPlayersKeyRef.current) {
          moderationPlayersKeyRef.current = playersKey
          void refreshModerationState(connection)
        }
      }
    })

    connection.on('PlayerDisconnected', (reason: string) => {
      setError(reason)
      setJoined(false)
      void connection.stop().catch(err => {
        console.error('Could not close the moderated player connection:', err)
      })
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
          await loadQuestionSets()
          await refreshModerationState(connection)
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

        const options = await connection.invoke<AnswerOption[]>('GetAnswerOptions')
        setAnswerOptions(options)

        if (isHost) {
          await connection.invoke('WatchRoom')
          await loadQuestionSets()
          await refreshModerationState(connection)
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

  useEffect(() => {
    if (!room?.game.phaseEndsAt) return

    const update = () => setCountdownNow(Date.now())
    update()

    const interval = window.setInterval(update, 100)

    return () => window.clearInterval(interval)
  }, [room?.game.phaseEndsAt])

  useEffect(() => {
    if (!moderation?.punishments.some(punishment =>
      punishment.expiresAt !== null &&
      punishment.revokedAt === null &&
      Date.parse(punishment.expiresAt) > countdownNow
    )) {
      return
    }

    const interval = window.setInterval(() => setCountdownNow(Date.now()), 1000)
    return () => window.clearInterval(interval)
  }, [countdownNow, moderation?.punishments])

  async function joinGame() {
    if (!playerName.trim()) return

    const connection = connectionRef.current

    if (!connection) {
      setError('Not connected to the game server. Please wait and try again.')
      return
    }

    try {
      if (connection.state === 'Disconnected') {
        await connection.start()
        setConnected(true)
      } else if (connection.state !== 'Connected') {
        setError('Not connected to the game server. Please wait and try again.')
        return
      }

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

  async function applyPlayerPunishment(
    method: 'KickPlayer' | 'TimeoutPlayer' | 'BanPlayer',
    playerId: string
  ) {
    const connection = connectionRef.current
    if (!connection || connection.state !== 'Connected') {
      setError('Not connected to the game server. Please wait and try again.')
      return
    }

    try {
      if (method === 'TimeoutPlayer') {
        await connection.invoke(method, playerId, timeoutMinutes, moderationReason)
      } else {
        await connection.invoke(method, playerId, moderationReason)
      }

      setError('')
      await refreshModerationState(connection)
    } catch (err) {
      console.error(`Could not ${method}:`, err)
      setError(
        err instanceof Error
          ? `Could not ${method}: ${err.message}`
          : `Could not ${method}: ${String(err)}`
      )
    }
  }

  async function revokePunishment(punishmentId: string) {
    const connection = connectionRef.current
    if (!connection || connection.state !== 'Connected') {
      setError('Not connected to the game server. Please wait and try again.')
      return
    }

    try {
      await connection.invoke('RevokePunishment', punishmentId)
      setError('')
      await refreshModerationState(connection)
    } catch (err) {
      console.error('Could not revoke punishment:', err)
      setError(
        err instanceof Error
          ? `Could not revoke punishment: ${err.message}`
          : `Could not revoke punishment: ${String(err)}`
      )
    }
  }

  async function forcePlayerName(playerId: string, name: string, color: string) {
    const connection = connectionRef.current
    const normalizedName = name.trim()
    if (!connection || connection.state !== 'Connected') {
      setError('Not connected to the game server. Please wait and try again.')
      return
    }
    if (!normalizedName) {
      setError('Player name cannot be empty.')
      return
    }

    try {
      await connection.invoke('UpdatePlayerProfile', playerId, normalizedName, color)
      setPlayerNameDrafts(previous => {
        const next = { ...previous }
        delete next[playerId]
        return next
      })
      setError('')
    } catch (err) {
      console.error('Could not set player name:', err)
      setError(
        err instanceof Error
          ? `Could not set player name: ${err.message}`
          : `Could not set player name: ${String(err)}`
      )
    }
  }

  async function setPlayerNameLock(playerId: string, isLocked: boolean) {
    const connection = connectionRef.current
    if (!connection || connection.state !== 'Connected') {
      setError('Not connected to the game server. Please wait and try again.')
      return
    }

    try {
      await connection.invoke('SetPlayerNameLock', playerId, isLocked)
      setError('')
    } catch (err) {
      console.error('Could not change player name lock:', err)
      setError(
        err instanceof Error
          ? `Could not change player name lock: ${err.message}`
          : `Could not change player name lock: ${String(err)}`
      )
    }
  }

  async function adjustPlayerScore(playerId: string, delta: number) {
    const connection = connectionRef.current
    if (!connection || connection.state !== 'Connected') {
      setError('Not connected to the game server. Please wait and try again.')
      return
    }

    try {
      await connection.invoke('AdjustPlayerScore', playerId, delta)
      setError('')
    } catch (err) {
      console.error('Could not adjust player score:', err)
      setError(
        err instanceof Error
          ? `Could not adjust player score: ${err.message}`
          : `Could not adjust player score: ${String(err)}`
      )
    }
  }

  async function resetPlayerScore(playerId: string, playerName: string) {
    if (!window.confirm(`Reset ${playerName}'s score to zero?`)) return

    const connection = connectionRef.current
    if (!connection || connection.state !== 'Connected') {
      setError('Not connected to the game server. Please wait and try again.')
      return
    }

    try {
      await connection.invoke('ResetPlayerScore', playerId)
      setError('')
    } catch (err) {
      console.error('Could not reset player score:', err)
      setError(
        err instanceof Error
          ? `Could not reset player score: ${err.message}`
          : `Could not reset player score: ${String(err)}`
      )
    }
  }

  async function startNewGame() {
    if (!selectedQuestionSet) return

    try {
      await connectionRef.current?.invoke(
        'StartNewGame',
        selectedQuestionSet
      )

      setPlayerAnswers([])
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
    if (selectedAnswerOptionId === null) return

    try {
      setAnswerResult(null)

      await connectionRef.current?.invoke(
        'SubmitAnswer',
        selectedAnswerOptionId
      )

      setAnswer('')
      setSelectedAnswerOptionId(null)
    } catch (err) {
      console.error(err)
      setError('Could not submit answer.')
    }
  }

  async function loadQuestionSets() {
    try {
      const sets = await connectionRef.current?.invoke<QuestionSetSummary[]>(
        'GetQuestionSets'
      )

      if (sets) {
        setQuestionSets(sets)

        if (sets.length > 0) {
          setSelectedQuestionSet(sets[0].fileName)
        } else {
          setSelectedQuestionSet('')
        }
      }
    } catch (err) {
      console.error('Could not load question sets:', err)
      setError('Could not load question sets.')
    }
  }

  function formatOrdinal(number: number): string {
    const remainder10 = number % 10
    const remainder100 = number % 100

    if (remainder100 >= 11 && remainder100 <= 13) {
      return `${number}th`
    }

    switch (remainder10) {
      case 1: return `${number}st`
      case 2: return `${number}nd`
      case 3: return `${number}rd`
      default: return `${number}th`
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

        <h2>Manage players</h2>
        <label>
          Reason{' '}
          <input
            value={moderationReason}
            onChange={event => setModerationReason(event.target.value)}
            placeholder="Optional reason"
          />
        </label>{' '}
        <label>
          Timeout duration{' '}
          <select
            value={timeoutMinutes}
            onChange={event => setTimeoutMinutes(Number(event.target.value))}
          >
            <option value={1}>1 minute</option>
            <option value={5}>5 minutes</option>
            <option value={15}>15 minutes</option>
            <option value={60}>1 hour</option>
            <option value={1440}>1 day</option>
          </select>
        </label>

        {moderation?.players.map(player => {
          const hasSharedIp = player.ipAddress !== null &&
            moderation.players.some(other =>
              other.id !== player.id && other.ipAddress === player.ipAddress
            )

          const nameDraft = playerNameDrafts[player.id] ?? player.name

          return (
            <div key={player.id} style={{ margin: '12px 0', padding: 12, border: '1px solid #888' }}>
              <strong>{player.name}</strong> — {player.isConnected ? 'Connected' : 'Disconnected'} — {player.nameLocked ? 'Name locked' : 'Name unlocked'}
              <p>
                IP: {player.ipAddress ?? 'Unavailable'} — {
                  player.ipAddress === null
                    ? 'IP moderation unavailable'
                    : hasSharedIp
                      ? 'shared address; punishments use player ID only'
                      : 'unique address; punishments also apply to this IP'
                }
              </p>
              <label>
                Set name{' '}
                <input
                  value={nameDraft}
                  onChange={event => setPlayerNameDrafts(previous => ({
                    ...previous,
                    [player.id]: event.target.value
                  }))}
                />
              </label>{' '}
              <button
                type="button"
                disabled={!connected || !nameDraft.trim() || nameDraft.trim() === player.name}
                onClick={() => void forcePlayerName(player.id, nameDraft, room?.players.find(item => item.id === player.id)?.color ?? '')}
              >
                Set name
              </button>{' '}
              <button
                type="button"
                disabled={!connected}
                onClick={() => void setPlayerNameLock(player.id, !player.nameLocked)}
              >
                {player.nameLocked ? 'Unlock name' : 'Lock name'}
              </button>
              <p>
                Score: {room?.game.scores[player.id] ?? 0}
              </p>
              <label>
                Points per adjustment{' '}
                <input
                  type="number"
                  min={1}
                  max={1000}
                  value={scoreAdjustment}
                  onChange={event => {
                    const value = Number(event.target.value)
                    if (Number.isInteger(value) && value >= 1 && value <= 1000) {
                      setScoreAdjustment(value)
                    }
                  }}
                />
              </label>{' '}
              <button
                type="button"
                disabled={!connected || !room?.game.gameSessionId}
                onClick={() => void adjustPlayerScore(player.id, scoreAdjustment)}
              >
                Add points
              </button>{' '}
              <button
                type="button"
                disabled={!connected || !room?.game.gameSessionId}
                onClick={() => void adjustPlayerScore(player.id, -scoreAdjustment)}
              >
                Remove points
              </button>{' '}
              <button
                type="button"
                disabled={!connected || !room?.game.gameSessionId || (room?.game.scores[player.id] ?? 0) === 0}
                onClick={() => void resetPlayerScore(player.id, player.name)}
              >
                Reset score
              </button>
              <p />
              <button
                type="button"
                disabled={!connected || !player.isConnected}
                onClick={() => void applyPlayerPunishment('KickPlayer', player.id)}
              >
                Kick
              </button>{' '}
              <button
                type="button"
                disabled={!connected}
                onClick={() => void applyPlayerPunishment('TimeoutPlayer', player.id)}
              >
                Timeout
              </button>{' '}
              <button
                type="button"
                disabled={!connected}
                onClick={() => void applyPlayerPunishment('BanPlayer', player.id)}
              >
                Ban
              </button>
            </div>
          )
        })}

        <h3>Punishment history</h3>
        {moderation?.punishments.length ? (
          [...moderation.punishments]
            .sort((a, b) => Date.parse(b.createdAt) - Date.parse(a.createdAt))
            .map(punishment => {
              const isExpired = punishment.expiresAt !== null &&
                Date.parse(punishment.expiresAt) <= countdownNow
              const isActive = punishment.revokedAt === null && !isExpired
              const status = punishment.revokedAt
                ? 'Revoked'
                : isExpired
                  ? 'Expired'
                  : 'Active'

              return (
                <div key={punishment.id} style={{ margin: '8px 0' }}>
                  <strong>{punishment.type}</strong> for {punishment.playerName} — {status}
                  {punishment.ipAddress && <span> (IP {punishment.ipAddress})</span>}
                  {punishment.expiresAt && <span> — expires {new Date(punishment.expiresAt).toLocaleString()}</span>}
                  {punishment.reason && <p>Reason: {punishment.reason}</p>}
                  {isActive && (
                    <button
                      type="button"
                      disabled={!connected}
                      onClick={() => void revokePunishment(punishment.id)}
                    >
                      Revoke
                    </button>
                  )}
                </div>
              )
            })
        ) : (
          <p>No timeouts or bans have been recorded.</p>
        )}

        <h2>Scoreboard</h2>

        {sortedPlayers.length === 0 ? (
          <p>No players yet.</p>
        ) : (
          sortedPlayers.map((player, index) => (
            <p key={player.id}>
              {index + 1}. {player.name} — {room?.game.scores[player.id] ?? 0} points
            </p>
          ))
        )}

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

              {submission.isCorrect && (
                <p>
                  {submission.alreadyAnsweredCorrectly
                    ? 'Already scored for this question.'
                    : `+${submission.pointsAwarded} points — ${formatOrdinal(
                      submission.correctAnswerNumber!
                    )} correct answer`}
                </p>
              )}
            </div>
          ))
        )}

        <h2>Choose a game</h2>

        {questionSets.length === 0 ? (
          <p>No question sets found.</p>
        ) : (
          <>
            <select
              value={selectedQuestionSet}
              onChange={event => setSelectedQuestionSet(event.target.value)}
            >
              {questionSets.map(set => (
                <option key={set.fileName} value={set.fileName}>
                  {set.title}
                </option>
              ))}
            </select>

            {questionSets
              .filter(set => set.fileName === selectedQuestionSet)
              .map(set => (
                <div key={set.fileName}>
                  <p>{set.description}</p>
                  <p>Questions: {set.questionCount}</p>
                  <p>Rating: {set.rating}</p>
                </div>
              ))}
          </>
        )}

        <button
          onClick={startNewGame}
          disabled={
            !connected ||
            !selectedQuestionSet ||
            (room?.players.length ?? 0) === 0
          }
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

            {countdownIndicator}

            <p>
              Question {
                room.game.currentQuestionIndex + 1
              } of {room.game.totalQuestions}
            </p>

            <h3>
              {room.game.currentQuestion?.questionText}
            </h3>

            {room?.game.phase === 'QuestionIntro' && (
              <button
                onClick={() => {
                  startQuestion()
                  setPlayingQuestionKey(currentClipKey)
                  setIsVideoPlaying(true)
                  setPlaybackTime(
                    room.game.currentQuestion!.questionVideo.startTime
                  )
                  setPlaybackStatus('Loading')
                }}
              >
                Play clip
              </button>
            )}

            {room?.game.phase === 'Playing' && room.game.currentQuestion && currentClipKey && (
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

                {room?.game.phase === 'Playing' && (
                  <div
                    style={{
                      width: '100%',
                      maxWidth: 800,
                      aspectRatio: '16 / 9',
                      marginTop: 16
                    }}
                  >
                    <ReactPlayer
                      ref={playerRef}
                      key={currentClipKey}
                      url={`https://www.youtube.com/watch?v=${room.game.currentQuestion.questionVideo.youTubeId}`}
                      playing={isVideoPlaying}
                      controls={false}
                      width="100%"
                      height="100%"
                      progressInterval={500}
                      config={{
                        youtube: {
                          playerVars: {
                            start: room.game.currentQuestion.questionVideo.startTime,
                            end: room.game.currentQuestion.questionVideo.endTime,
                            controls: 0,
                            rel: 0,
                            playsinline: 1
                          }
                        }
                      }}
                      onPlay={() => {
                        playbackIsAdvancingRef.current = true
                        setPlaybackStatus('Playing')

                        if (room?.game.phase === 'QuestionIntro') {
                          connectionRef.current
                            ?.invoke('StartQuestion')
                            .catch(error => console.error('Could not start question:', error))
                        } else {
                          const timestamp = playerRef.current?.getCurrentTime()

                          if (timestamp !== undefined) {
                            connectionRef.current
                              ?.send('UpdatePlayback', timestamp, true)
                              .catch(error => console.error('Could not update playback:', error))
                          }
                        }
                      }}

                      onPause={() => {
                        playbackIsAdvancingRef.current = false
                        setPlaybackStatus('Paused')

                        const timestamp = playerRef.current?.getCurrentTime()

                        if (timestamp !== undefined) {
                          connectionRef.current
                            ?.send('UpdatePlayback', timestamp, false)
                            .catch(error => console.error('Could not update playback:', error))
                        }
                      }}

                      onBuffer={() => {
                        playbackIsAdvancingRef.current = false
                        setPlaybackStatus('Buffering')

                        const timestamp = playerRef.current?.getCurrentTime()

                        if (timestamp !== undefined) {
                          connectionRef.current
                            ?.send('UpdatePlayback', timestamp, false)
                            .catch(error => console.error('Could not update playback:', error))
                        }
                      }}

                      onBufferEnd={() => {
                        playbackIsAdvancingRef.current = true
                        setPlaybackStatus('Playing')

                        const timestamp = playerRef.current?.getCurrentTime()

                        if (timestamp !== undefined) {
                          connectionRef.current
                            ?.send('UpdatePlayback', timestamp, true)
                            .catch(error => console.error('Could not update playback:', error))
                        }
                      }}

                      onProgress={({ playedSeconds }) => {
                        setPlaybackTime(playedSeconds)

                        if (playbackIsAdvancingRef.current) {
                          connectionRef.current
                            ?.send('UpdatePlayback', playedSeconds, true)
                            .catch(error => console.error('Could not update playback:', error))
                        }
                      }}

                      onEnded={() => {
                        playbackIsAdvancingRef.current = false
                        setPlaybackStatus('Ended')
                        setIsVideoPlaying(false)

                        const timestamp =
                          playerRef.current?.getCurrentTime() ?? playbackTime

                        connectionRef.current
                          ?.invoke('EndPlayback', timestamp)
                          .catch(error => console.error('Could not end playback:', error))
                      }}
                    />

                    <p>
                      Playback: {playbackStatus}
                    </p>

                    <p>
                      Timestamp: {playbackTime.toFixed(2)}s
                    </p>

                    <button
                      onClick={() => setIsVideoPlaying(previous => !previous)}
                      disabled={playbackStatus === 'Ended'}
                    >
                      {isVideoPlaying ? 'Pause' : 'Resume'}
                    </button>
                  </div>
                )}
              </>
            )}
            {(room?.game.phase === 'Overtime') && (
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
            disabled={!playerName.trim()}
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

              {countdownIndicator}

              <form
                onSubmit={event => {
                  event.preventDefault()
                  void submitAnswer()
                }}
              >
                <div>
                  <input
                    value={answer}
                    onChange={event => {
                      setAnswer(event.target.value)
                      setSelectedAnswerOptionId(null)
                    }}
                    placeholder="Search for a song or artist..."
                    autoComplete="off"
                  />

                  {answer.trim() && selectedAnswerOptionId === null && (
                    <div style={{
                      border: '1px solid #888',
                      borderRadius: 8,
                      maxHeight: 240,
                      overflowY: 'auto',
                      maxWidth: 400
                    }}>
                      {filteredAnswerOptions.length > 0 ? (
                        filteredAnswerOptions.map(option => (
                          <button
                            key={option.id}
                            type="button"
                            onClick={() => {
                              setAnswer(option.displayName)
                              setSelectedAnswerOptionId(option.id)
                            }}
                            style={{
                              display: 'block',
                              width: '100%',
                              padding: 10,
                              textAlign: 'left',
                              border: 'none',
                              borderBottom: '1px solid #555',
                              cursor: 'pointer'
                            }}
                          >
                            {option.displayName}
                          </button>
                        ))
                      ) : (
                        <p>No matching options.</p>
                      )}
                    </div>
                  )}
                </div>

                <button
                  type="submit"
                  disabled={selectedAnswerOptionId === null || !connected}
                >
                  Submit answer
                </button>
              </form>

              {answerResult && (
                <div>
                  <h2
                    style={{
                      color: answerResult.isCorrect ? 'green' : 'red'
                    }}
                  >
                    {answerResult.isCorrect ? 'Correct!' : 'Incorrect!'}
                  </h2>

                  {answerResult.isCorrect && (
                    <p>
                      {answerResult.alreadyAnsweredCorrectly
                        ? 'You already earned points for this question.'
                        : `You earned ${answerResult.pointsAwarded} points!`}
                    </p>
                  )}
                </div>
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

      {joined && room && (
        <section>
          <h2>Scoreboard</h2>

          {sortedPlayers.map((player, index) => (
            <p key={player.id}>
              {index + 1}. {player.name} — {room.game.scores[player.id] ?? 0} points
            </p>
          ))}
        </section>
      )}

      {error && <p>{error}</p>}
    </main>
  )
}

export default App