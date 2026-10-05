export type TileRect = { left: number; top: number; width: number; height: number }

// Choose the largest 16:9 tiles that fit, and centre incomplete rows.
export function fitUserTiles(count: number, width: number, height: number, gap = 12): TileRect[] {
  if (!count || width <= 0 || height <= 0) return []
  let best = { columns: 1, width: 0, height: 0 }
  for (let columns = 1; columns <= Math.min(5, count); columns++) {
    const rows = Math.ceil(count / columns)
    const tileWidth = Math.max(0, Math.min((width - gap * (columns - 1)) / columns,
      (height - gap * (rows - 1)) / rows * 16 / 9))
    if (tileWidth > best.width) best = { columns, width: tileWidth, height: tileWidth * 9 / 16 }
  }
  const rows = Math.ceil(count / best.columns)
  const top = (height - rows * best.height - (rows - 1) * gap) / 2
  return Array.from({ length: count }, (_, index) => {
    const row = Math.floor(index / best.columns)
    const columnsInRow = Math.min(best.columns, count - row * best.columns)
    return { left: (width - columnsInRow * best.width - (columnsInRow - 1) * gap) / 2 +
      (index % best.columns) * (best.width + gap), top: top + row * (best.height + gap),
      width: best.width, height: best.height }
  })
}

export function layoutLiveTiles(count: number, presenting: boolean, width: number, height: number, screenRatio = 16 / 9): TileRect[] {
  if (!count || width <= 0 || height <= 0) return []
  if (!Number.isFinite(screenRatio) || screenRatio <= 0) screenRatio = 16 / 9
  if (!presenting) return fitUserTiles(count, width, height)
  const gap = 12
  // Portrait devices keep a large presentation above the participant strip.
  if (width < 640 && height > width) {
    const screenHeight = Math.min(height * .64, width / screenRatio)
    return [{ left: 0, top: 0, width, height: screenHeight },
      ...fitUserTiles(count - 1, width, Math.min(height - screenHeight - gap, screenHeight * .85), gap)
        .map(tile => ({ ...tile, top: tile.top + screenHeight + gap }))]
  }
  const railWidth = width * .27
  const screenWidth = width - railWidth - gap
  const screenHeight = Math.min(height, screenWidth / screenRatio)
  const users = count - 1
  const remainingHeight = height - screenHeight - gap
  // Use the space beneath a landscape presentation for users instead of letterboxing.
  if (users > 1 && remainingHeight >= 110) {
    const railCount = Math.min(users, Math.max(1, Math.floor((screenHeight + gap) / (railWidth * 9 / 16 + gap))))
    return [{ left: 0, top: 0, width: screenWidth, height: screenHeight },
      ...fitUserTiles(railCount, railWidth, screenHeight)
        .map(tile => ({ ...tile, left: tile.left + screenWidth + gap })),
      ...fitUserTiles(users - railCount, width, remainingHeight)
        .map(tile => ({ ...tile, top: tile.top + screenHeight + gap }))]
  }
  const columns = users > 3 && railWidth >= 300 ? 2 : 1
  const rows = Math.ceil(users / columns)
  const tileWidth = (railWidth - gap * (columns - 1)) / columns
  const tileHeight = Math.min((height - gap * (rows - 1)) / rows, tileWidth * .85)
  const top = Math.max(0, (height - rows * tileHeight - gap * (rows - 1)) / 2)
  return [{ left: 0, top: (height - screenHeight) / 2, width: screenWidth, height: screenHeight },
    ...Array.from({ length: users }, (_, index) => ({
      left: screenWidth + gap + (index % columns) * (tileWidth + gap),
      top: top + Math.floor(index / columns) * (tileHeight + gap),
      width: tileWidth, height: tileHeight
    }))]
}

export function pageUserTiles<T extends { userId: string }>(users: T[], presenterId: string | undefined, page: number, capacity: number) {
  const presenter = presenterId ? users.find(user => user.userId === presenterId) : undefined
  const others = presenter ? users.filter(user => user.userId !== presenterId) : users
  const slots = Math.max(1, capacity - (presenter ? 1 : 0))
  const pageCount = Math.max(1, Math.ceil(others.length / slots))
  const currentPage = Math.min(Math.max(0, page), pageCount - 1)
  return { pageCount, users: [...(presenter ? [presenter] : []), ...others.slice(currentPage * slots, (currentPage + 1) * slots)] }
}
