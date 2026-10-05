import { readFileSync } from 'node:fs'
import { test } from 'node:test'
import assert from 'node:assert/strict'
import ts from 'typescript'
const source = readFileSync(new URL('../src/features/classrooms/media/tileLayout.ts', import.meta.url), 'utf8')
const js = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.ESNext } }).outputText
const { layoutLiveTiles, pageUserTiles } = await import(`data:text/javascript;base64,${Buffer.from(js).toString('base64')}`)
for (const [width, height] of [[1600, 780], [1000, 540], [360, 540], [740, 220]]) {
  for (const presenting of [false, true]) {
    test(`${width}x${height}, presenting=${presenting}: all tiles fit without overlap`, () => {
      for (let count = presenting ? 2 : 1; count <= (presenting ? 7 : 12); count++) {
        const tiles = layoutLiveTiles(count, presenting, width, height)
        assert.equal(tiles.length, count)
        for (const [index, rect] of tiles.entries()) {
          assert.ok(rect.width > 0 && rect.height > 0)
          assert.ok(rect.left >= -.01 && rect.top >= -.01)
          assert.ok(rect.left + rect.width <= width + .01 && rect.top + rect.height <= height + .01)
          for (const other of tiles.slice(index + 1)) {
            assert.ok(rect.left + rect.width <= other.left + .01 || other.left + other.width <= rect.left + .01 ||
              rect.top + rect.height <= other.top + .01 || other.top + other.height <= rect.top + .01)
          }
        }
        if (presenting) assert.ok(tiles[0].width * tiles[0].height > tiles[1].width * tiles[1].height)
      }
    })
  }
}
test('12 desktop participants use 3 rows x 4 columns; one user grows', () => {
  const tiles = layoutLiveTiles(12, false, 1600, 780)
  assert.equal(new Set(tiles.map(x => x.top)).size, 3)
  assert.equal(new Set(tiles.map(x => x.left)).size, 4)
  assert.ok(layoutLiveTiles(1, false, 1600, 780)[0].width > tiles[0].width)
})

test('100 users: every user reachable and presenter pinned on each page', () => {
  const users = Array.from({ length: 100 }, (_, i) => ({ userId: String(i) }))
  for (const capacity of [3, 6, 12]) {
    const visited = []
    const pages = pageUserTiles(users, '42', 0, capacity).pageCount
    for (let page = 0; page < pages; page++) {
      const result = pageUserTiles(users, '42', page, capacity)
      assert.equal(result.users[0].userId, '42')
      assert.ok(result.users.length <= capacity)
      visited.push(...result.users.slice(1).map(user => user.userId))
    }
    assert.equal(visited.length, 99)
    assert.equal(new Set(visited).size, 99)
  }
  assert.equal(pageUserTiles(users.slice(0, 1), '0', 8, 6).users.length, 1)
  assert.deepEqual(pageUserTiles([], undefined, 8, 12), { pageCount: 1, users: [] })
})

 test('landscape share uses its natural ratio and fills lower space with users', () => {
  const tiles = layoutLiveTiles(7, true, 1000, 740)
  assert.ok(Math.abs(tiles[0].width / tiles[0].height - 16 / 9) < .001)
  assert.ok(tiles[1].left > tiles[0].width)
  assert.ok(tiles.slice(2).some(tile => tile.top >= tiles[0].height + 12))
})
