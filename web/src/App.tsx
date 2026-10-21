import { useEffect, useState } from 'react'
import { fetchDigest, fetchStory, type Digest, type DigestStory, type StoryDetail } from './api'

const timeZone = 'America/New_York'

// en-CA formats dates as YYYY-MM-DD, which is what the API and <input type="date"> expect.
const todayEastern = () => new Intl.DateTimeFormat('en-CA', { timeZone }).format(new Date())

const timeEastern = (utc: string) =>
  new Date(utc).toLocaleTimeString('en-US', { timeZone, hour: 'numeric', minute: '2-digit' })

const directionLabel: Record<number, string> = { 1: '▲', [-1]: '▼', 0: '–' }

export default function App() {
  const [date, setDate] = useState(todayEastern)
  const [digest, setDigest] = useState<Digest | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false
    setDigest(null)
    setError(null)
    fetchDigest(date)
      .then((d) => !cancelled && setDigest(d))
      .catch((e: Error) => !cancelled && setError(e.message))
    return () => {
      cancelled = true
    }
  }, [date])

  return (
    <main>
      <header>
        <div>
          <h1>Signal or Noise</h1>
          <p className="muted">Top stories by LLM-judged materiality. Times are US/Eastern.</p>
        </div>
        <label>
          Day <input type="date" value={date} max={todayEastern()} onChange={(e) => e.target.value && setDate(e.target.value)} />
        </label>
      </header>

      {error && <p className="error">Could not load the digest: {error}</p>}
      {!digest && !error && <p className="muted">Loading…</p>}
      {digest && digest.stories.length === 0 && <p className="muted">No scored stories for this day yet.</p>}
      {digest && digest.stories.length > 0 && (
        <>
          <p className="muted">
            Top {digest.stories.length} events from {digest.storyCount} scored stories.
          </p>
          <ol className="stories">
            {digest.stories.map((story) => (
              <StoryRow key={story.id} story={story} />
            ))}
          </ol>
        </>
      )}
    </main>
  )
}

function StoryRow({ story }: { story: DigestStory }) {
  const [open, setOpen] = useState(false)
  const level = story.materiality >= 7 ? 'high' : story.materiality >= 4 ? 'mid' : 'low'

  return (
    <li className="story">
      <div className={`score ${level}`} title="Materiality, 0-10">
        {story.materiality}
      </div>
      <div className="body">
        <div className="meta">
          <span className={`direction d${story.direction}`}>{directionLabel[story.direction]}</span>
          <strong title={story.primaryTicker ? undefined : 'Market-wide, or a company outside the 50 tracked tickers'}>
            {story.primaryTicker ?? 'Other'}
          </strong>
          <span>{story.eventType.replaceAll('_', ' ')}</span>
          <span>{timeEastern(story.firstPublishedUtc)}</span>
          <span>
            {story.headlineCount} headline{story.headlineCount === 1 ? '' : 's'}
            {story.storyCount > 1 && ` across ${story.storyCount} stories`}
          </span>
        </div>
        <p className="summary">{story.summary}</p>
        <div className="links">
          {story.links.map((link) => (
            <a key={link.url} href={link.url} target="_blank" rel="noreferrer">
              {link.title}
            </a>
          ))}
          <button type="button" onClick={() => setOpen(!open)}>
            {open ? 'Hide details' : 'Details'}
          </button>
        </div>
        {open && <StoryDetails id={story.id} />}
      </div>
    </li>
  )
}

function StoryDetails({ id }: { id: number }) {
  const [detail, setDetail] = useState<StoryDetail | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    fetchStory(id).then(setDetail).catch((e: Error) => setError(e.message))
  }, [id])

  if (error) return <p className="error">{error}</p>
  if (!detail) return <p className="muted">Loading…</p>

  return (
    <div className="details">
      {detail.rationale && <p>{detail.rationale}</p>}
      {detail.scoredAsOfUtc && (
        <p className="muted">
          Scored by {detail.model} using only headlines up to {timeEastern(detail.scoredAsOfUtc)}.
        </p>
      )}
      <ul>
        {detail.headlines.map((h) => (
          <li key={h.url} className={h.seenByScorer ? '' : 'unseen'}>
            <span className="muted">{timeEastern(h.publishedUtc)}</span>{' '}
            <a href={h.url} target="_blank" rel="noreferrer">
              {h.title}
            </a>
            {!h.seenByScorer && <span className="muted"> (after cutoff, not seen by scorer)</span>}
          </li>
        ))}
      </ul>
    </div>
  )
}
