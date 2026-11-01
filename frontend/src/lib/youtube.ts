export function youtubeEmbedUrl(link: string | null): string | null {
  if (!link) {
    return null
  }
  let url: URL
  try {
    url = new URL(link)
  } catch {
    return null
  }
  const host = url.hostname.replace(/^www\./, '')
  let id: string | null = null
  if (host === 'youtu.be') {
    id = url.pathname.slice(1)
  } else if (host === 'youtube.com' || host === 'm.youtube.com') {
    id = url.searchParams.get('v') ?? (url.pathname.startsWith('/embed/') ? url.pathname.slice(7) : null)
  }
  return id ? `https://www.youtube.com/embed/${encodeURIComponent(id)}` : null
}
