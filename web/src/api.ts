// Types mirror src/SignalOrNoise.Api/Contracts.cs. Timestamps are UTC ISO strings.

export type HeadlineLink = {
  title: string
  url: string
  source: string
  publishedUtc: string
}

export type DigestStory = {
  id: number
  firstPublishedUtc: string
  materiality: number
  direction: number
  eventType: string
  primaryTicker: string | null
  summary: string
  headlineCount: number
  // More than 1 when the row merges several stories about the same event.
  storyCount: number
  links: HeadlineLink[]
}

export type Digest = {
  date: string
  storyCount: number
  stories: DigestStory[]
}

export type StoryHeadline = HeadlineLink & {
  tickers: string
  seenByScorer: boolean
}

export type StoryDetail = {
  id: number
  scoredAsOfUtc: string | null
  rationale: string | null
  model: string | null
  headlines: StoryHeadline[]
}

async function getJson<T>(path: string): Promise<T> {
  const response = await fetch(path)
  if (!response.ok) throw new Error(`${path} returned HTTP ${response.status}`)
  return response.json() as Promise<T>
}

export const fetchDigest = (date: string) => getJson<Digest>(`/digest?date=${date}`)
export const fetchStory = (id: number) => getJson<StoryDetail>(`/stories/${id}`)
