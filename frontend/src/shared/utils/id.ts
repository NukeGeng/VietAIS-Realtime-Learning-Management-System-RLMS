/**
 * Mirrors AssessmentConstants.StableId in the backend.
 * .NET Guid(byte[]) uses little-endian ordering for the first three fields,
 * so the formatted string reverses those bytes before rendering.
 */
export async function stableId(value: string): Promise<string> {
  const bytes = new TextEncoder().encode(value)
  const hash = new Uint8Array(await crypto.subtle.digest('SHA-256', bytes))
  hash[6] = (hash[6] & 0x0f) | 0x50
  hash[8] = (hash[8] & 0x3f) | 0x80
  const hex = Array.from(hash, byte => byte.toString(16).padStart(2, '0'))
  const littleEndian = (start: number, end: number) => hex.slice(start, end).reverse().join('')
  return `${littleEndian(0, 4)}-${littleEndian(4, 6)}-${littleEndian(6, 8)}-${hex.slice(8, 10).join('')}-${hex.slice(10, 16).join('')}`
}

export async function stableSubmissionId(activityId: string, studentId: string, attemptNumber: number): Promise<string> {
  return stableId(`submission:${activityId.toLowerCase()}:${studentId.toLowerCase()}:${attemptNumber}`)
}
