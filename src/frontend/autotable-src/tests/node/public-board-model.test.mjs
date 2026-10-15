import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';
import { runInNewContext } from 'node:vm';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const require = createRequire(path.join(root, 'package.json'));
const ts = require('typescript');
const source = fs.readFileSync(path.join(root, 'src/public-board-model.ts'), 'utf8');
const module = { exports: {} };
runInNewContext(ts.transpileModule(source, {
  compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
}).outputText, {
  module, exports: module.exports,
  require: id => { throw new Error(`The pure public board model must not import ${id}`); },
});
const { buildPublicBoard } = module.exports;
const plain = value => JSON.parse(JSON.stringify(value));
const build = (entries = [], viewer = 0, active = null) => plain(buildPublicBoard(entries, viewer, active));
const info = (slotName, fields = {}) => ({
  slotName, rotationIndex: 0, claimedBy: null,
  heldRotation: { x: 0, y: 0, z: 0, w: 1 }, shiftSlotName: null, ...fields,
});
const entry = (key, slot, fields = {}) => [key, info(slot, fields)];
const face = (slot, value) => ({ slot, kind: 'face', face: value });
const hidden = (slot, kind = 'back') => ({ slot, kind, face: null });
const emptySeats = () => [0, 1, 2, 3].map(seat => ({ seat, discards: [], melds: [] }));

test('the module has no runtime imports or browser/Three dependencies', () => {
  assert.deepEqual(Object.keys(module.exports), ['buildPublicBoard']);
  const ast = ts.createSourceFile('public-board-model.ts', source, ts.ScriptTarget.Latest, true);
  const imports = ast.statements.filter(ts.isImportDeclaration);
  assert.equal(imports.length, 1);
  assert.equal(imports[0].importClause.isTypeOnly, true);
  assert.equal(imports[0].moduleSpecifier.text, './types');
});

test('the exact exported contract is strict-TypeScript checked and readonly', () => {
  const filename = path.join(root, 'tests/node/public-board-model.contract.ts');
  const contract = `
    import { buildPublicBoard } from '../../src/public-board-model';
    import type {
      Seat, PublicTile, PublicMeld, PublicSeat, PublicBoardSnapshot, ReadonlyPublicContext,
    } from '../../src/public-board-model';
    import type { ThingInfo } from '../../src/types';
    type Equal<A, B> = (<T>() => T extends A ? 1 : 2) extends
      (<T>() => T extends B ? 1 : 2) ? true : false;
    type Assert<T extends true> = T;
    type SeatContract = Assert<Equal<Seat, 0 | 1 | 2 | 3>>;
    type TileContract = Assert<Equal<PublicTile, Readonly<{ slot: string } & (
      { kind: 'face'; face: number } | { kind: 'back' | 'unknown'; face: null }
    )>>>;
    type MeldContract = Assert<Equal<PublicMeld,
      Readonly<{ group: number; tiles: readonly PublicTile[] }>>>;
    type SeatViewContract = Assert<Equal<PublicSeat, Readonly<{
      seat: Seat; discards: readonly PublicTile[]; melds: readonly PublicMeld[];
    }>>>;
    type SnapshotContract = Assert<Equal<PublicBoardSnapshot, Readonly<{
      viewerSeat: Seat | null; activeSeat: Seat | null; seats: readonly PublicSeat[]; wallCount: number;
    }>>>;
    type ContextContract = Assert<Equal<ReadonlyPublicContext, Readonly<
      { kind: 'claim' | 'discard'; seat: Seat; face: number; slot?: string } |
      { kind: 'waiting' | 'unavailable' }
    >>>;
    type FunctionContract = Assert<Equal<typeof buildPublicBoard, (
      entries: Iterable<[string | number, ThingInfo]>, viewerSeat: number, activeSeat: number | null
    ) => PublicBoardSnapshot>>;
    declare const board: PublicBoardSnapshot;
    declare const tile: PublicTile;
    declare const meld: PublicMeld;
    declare const seat: PublicSeat;
    declare const context: ReadonlyPublicContext;
    // @ts-expect-error snapshots are readonly
    board.wallCount = 12;
    // @ts-expect-error seat arrays are readonly
    board.seats.push(seat);
    // @ts-expect-error seat fields are readonly
    seat.seat = 2;
    // @ts-expect-error river arrays are readonly
    seat.discards.push(tile);
    // @ts-expect-error meld arrays are readonly
    seat.melds.push(meld);
    // @ts-expect-error meld fields are readonly
    meld.group = 2;
    // @ts-expect-error meld tile arrays are readonly
    meld.tiles.push(tile);
    // @ts-expect-error tile fields are readonly
    tile.slot = 'hand.0@0';
    // @ts-expect-error context fields are readonly
    context.kind = 'waiting';
    // @ts-expect-error backs cannot expose a face
    const leak: PublicTile = { slot: 'meld.0.0@0', kind: 'back', face: 2 };
    const precise: ReadonlyPublicContext = {
      kind: 'claim', seat: 0, face: 2, slot: 'discard.0.0@1',
    };
    const faceOnly: ReadonlyPublicContext = { kind: 'discard', seat: 3, face: 26 };
    const waiting: ReadonlyPublicContext = { kind: 'waiting' };
    const unavailable: ReadonlyPublicContext = { kind: 'unavailable' };
  `;
  const options = {
    strict: true, noEmit: true, target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.ESNext,
    moduleResolution: ts.ModuleResolutionKind.Bundler, types: [],
    lib: ['lib.dom.d.ts', 'lib.dom.iterable.d.ts', 'lib.es2022.d.ts'],
  };
  const host = ts.createCompilerHost(options);
  const originalGetSourceFile = host.getSourceFile.bind(host);
  host.getSourceFile = (name, ...args) => name === filename
    ? ts.createSourceFile(name, contract, options.target, true)
    : originalGetSourceFile(name, ...args);
  const diagnostics = ts.getPreEmitDiagnostics(ts.createProgram([filename], options, host));
  assert.equal(diagnostics.length, 0, diagnostics.map(diagnostic =>
    ts.flattenDiagnosticMessageText(diagnostic.messageText, '\n')).join('\n'));
});

for (const viewer of [0, 1, 2, 3]) {
  test(`viewer ${viewer} retains absolute seat order and all four public groups`, () => {
    const entries = [3, 1, 0, 2].flatMap(seat => [
      entry(seat * 3, `discard.0.0@${seat}`, { face: seat }),
      entry(seat * 3 + 1, `meld.0.0@${seat}`, { face: seat + 9 }),
      entry(seat * 3 + 2, `hand.0@${seat}`, { face: seat + 18 }),
    ]);
    assert.deepEqual(build(entries, viewer, (viewer + 1) % 4), {
      viewerSeat: viewer, activeSeat: (viewer + 1) % 4, wallCount: 0,
      seats: [0, 1, 2, 3].map(seat => ({
        seat, discards: [face(`discard.0.0@${seat}`, seat)],
        melds: [{ group: 0, tiles: [face(`meld.0.0@${seat}`, seat + 9)] }],
      })),
    });
    assert.deepEqual(build([], viewer).seats, emptySeats());
  });
}

test('empty valid snapshots have no synthesized wall, tiles, or turn', () => {
  assert.deepEqual(build(), {
    viewerSeat: 0, activeSeat: null, seats: emptySeats(), wallCount: 0,
  });
});

test('invalid viewers fail closed before consuming any authority entries', () => {
  const entries = { [Symbol.iterator]() { throw new Error('Must not consume'); } };
  for (const viewer of [-1, 4, 0.5, NaN, Infinity, -Infinity, null, undefined, '0', true, {}]) {
    assert.deepEqual(plain(buildPublicBoard(entries, viewer, 2)), {
      viewerSeat: null, activeSeat: null, seats: [], wallCount: 0,
    });
  }
});

test('the active seat must be an integer number in 0..3', () => {
  for (const active of [0, 1, 2, 3]) assert.equal(build([], 0, active).activeSeat, active);
  for (const active of [-1, 4, 0.5, NaN, Infinity, -Infinity, null, undefined, '2', true, {}]) {
    assert.equal(plain(buildPublicBoard([], 0, active)).activeSeat, null);
  }
});

test('every physical key and every explicit face is accepted without deriving one from the other', () => {
  for (let key = 0; key <= 107; key++) {
    const value = (key * 11 + 7) % 27;
    assert.deepEqual(build([entry(key, 'discard.0.0@0', { face: value })]).seats[0].discards,
      [face('discard.0.0@0', value)]);
  }
  assert.deepEqual(build([
    entry(0, 'discard.0.0@0', { face: 26 }),
    entry(107, 'discard.0.1@0', { face: 0 }),
  ]).seats[0].discards, [face('discard.0.0@0', 26), face('discard.0.1@0', 0)]);
});

test('null is a back; missing, inherited, and invalid faces are unknown with no atlas fallback', () => {
  assert.deepEqual(build([entry(53, 'discard.0.0@0', { face: null })]).seats[0].discards,
    [hidden('discard.0.0@0')]);
  const inherited = Object.assign(Object.create({ face: 17 }), info('discard.0.0@0'));
  const cases = [
    info('discard.0.0@0'), inherited,
    info('discard.0.0@0', { typeIndex: 17, index: 68, tile: 68 }),
    ...[undefined, NaN, Infinity, -Infinity, -1, 27, 1.5, '17', '', true, false, {}, [], 2n]
      .map(value => info('discard.0.0@0', { face: value })),
  ];
  for (const value of cases) {
    assert.deepEqual(build([[68, value]]).seats[0].discards, [hidden('discard.0.0@0', 'unknown')]);
  }
});

test('invalid numeric physical keys never authorize even an explicit valid face', () => {
  for (const key of [-1, 108, 200, 0.5, NaN, Infinity, -Infinity, Number.MAX_SAFE_INTEGER]) {
    assert.deepEqual(build([entry(key, 'discard.0.0@0', { face: 17 })]).seats[0].discards,
      [hidden('discard.0.0@0', 'unknown')]);
  }
});

test('all opaque strings, including numeric-looking handles, remain backs under face poisoning', () => {
  for (const key of ['opaque-secret-token', '0', '107', '-1', '', '__proto__', 'constructor', 'tile:99']) {
    for (const fields of [{ face: 17 }, { face: null }, {}, { face: '17' }, { face: 27 }]) {
      assert.deepEqual(build([entry(key, 'discard.0.0@0', fields)]).seats[0].discards,
        [hidden('discard.0.0@0')]);
    }
    const poisoned = info('meld.0.0@1');
    Object.defineProperty(poisoned, 'face', { get() { throw new Error('Opaque face was read'); } });
    assert.deepEqual(build([[key, poisoned]]).seats[1].melds,
      [{ group: 0, tiles: [hidden('meld.0.0@1')] }]);
  }
});

test('own and opposing private hands and all nonpublic objects are ignored', () => {
  const slots = [
    ...[0, 1, 2, 3].flatMap(seat => [`hand.0@${seat}`, `hand.13@${seat}`, `hand.extra@${seat}`]),
    'hidden.0@0', 'hidden@0', 'park.0@0', 'off-table', 'kita.0@0', 'marker@0',
    'stick.0@0', 'discard.extra.0@0', 'wall.open.0.0@0',
  ];
  for (const viewer of [0, 1, 2, 3]) {
    assert.deepEqual(build(slots.map((slot, key) => entry(key, slot, { face: 17 })), viewer), {
      viewerSeat: viewer, activeSeat: null, seats: emptySeats(), wallCount: 0,
    });
  }
});

test('masked concealed kongs stay four grouped backs for every viewer', () => {
  const entries = [0, 1, 2, 3].flatMap(position => [
    entry(`concealed-kong-secret-${position}`, `meld.0.${position}@1`, {
      face: 25, rotationIndex: 2,
    }),
    entry(80 + position, `meld.1.${position}@2`, { face: null, rotationIndex: 2 }),
  ]);
  for (const viewer of [0, 1, 2, 3]) {
    const board = build(entries, viewer);
    assert.deepEqual(board.seats[1].melds, [{
      group: 0, tiles: [0, 1, 2, 3].map(position => hidden(`meld.0.${position}@1`)),
    }]);
    assert.deepEqual(board.seats[2].melds, [{
      group: 1, tiles: [0, 1, 2, 3].map(position => hidden(`meld.1.${position}@2`)),
    }]);
  }
});

test('chow, pung, exposed kong, and added kong are numerically grouped and ordered', () => {
  const groups = [
    [[0, 0], [1, 1], [2, 2]],
    [[0, 9], [1, 9], [2, 9]],
    [[0, 15], [1, 15], [2, 15], [3, 15]],
    [[0, 22], [1, 22], [2, 22], [4, 22]],
  ];
  let key = 0;
  const entries = groups.flatMap((tiles, group) => tiles.map(([position, value]) =>
    entry(key++, `meld.${group}.${position}@3`, { face: value }))).reverse();
  assert.deepEqual(build(entries).seats[3].melds, groups.map((tiles, group) => ({
    group, tiles: tiles.map(([position, value]) => face(`meld.${group}.${position}@3`, value)),
  })));
});

test('rivers are sorted by numeric row and column, never wire order', () => {
  const slots = ['discard.2.5@0', 'discard.0.5@0', 'discard.1.0@0', 'discard.0.0@0'];
  assert.deepEqual(build(slots.map((slot, key) => entry(key, slot, { face: key }))).seats[0].discards,
    [face(slots[3], 3), face(slots[1], 1), face(slots[2], 2), face(slots[0], 0)]);
});

test('duplicate public occupancy becomes one unknown, including identical retransmissions', () => {
  const slot = 'discard.1.2@2';
  for (const duplicates of [
    [entry(1, slot, { face: 3 }), entry(2, slot, { face: 24 })],
    [entry(1, slot, { face: 3 }), entry(2, slot, { face: 3 })],
    [entry(1, slot, { face: 3 }), entry(1, slot, { face: 3 })],
    [entry(1, slot, { face: 3 }), entry('opaque-secret', slot, { face: 3 })],
    [entry(1, slot), entry(2, slot, { face: 3 })],
    [entry('opaque-a', slot), entry('opaque-b', slot)],
  ]) {
    for (const ordered of [duplicates, [...duplicates].reverse()]) {
      assert.deepEqual(build(ordered).seats[2].discards, [hidden(slot, 'unknown')]);
    }
  }
});

test('conflicting fourth-slot aliases never create a fifth tile or select a plausible face', () => {
  const base = [0, 1, 2].map(position => entry(position, `meld.2.${position}@1`, { face: 8 }));
  for (const fourth of [
    entry(4, 'meld.2.4@1', { face: 8 }),
    entry(4, 'meld.2.4@1', { face: 21 }),
    entry('opaque-fourth', 'meld.2.4@1', { face: 8 }),
  ]) {
    const entries = [...base, entry(3, 'meld.2.3@1', { face: 8 }), fourth, fourth];
    const expected = [{
      group: 2,
      tiles: [...[0, 1, 2].map(position => face(`meld.2.${position}@1`, 8)),
        hidden('meld.2.3@1', 'unknown')],
    }];
    assert.deepEqual(build(entries).seats[1].melds, expected);
    assert.deepEqual(build([...entries].reverse()).seats[1].melds, expected);
  }
});

test('arbitrary input order, even with conflicts, produces the same snapshot', () => {
  const entries = [
    entry(0, 'discard.2.5@0', { face: 26 }),
    entry(1, 'discard.0.0@0', { face: 3 }),
    entry(2, 'meld.3.4@2', { face: 5 }),
    entry(3, 'meld.3.3@2', { face: 6 }),
    entry(4, 'meld.0.1@2', { face: 17 }),
    entry(5, 'meld.0.0@2', { face: 16 }),
    entry(6, 'discard.1.1@1', { face: 0 }),
    entry(7, 'discard.1.1@1', { face: 25 }),
    entry('wall-secret-a', 'wall.13.1@0'),
    entry('wall-secret-b', 'wall.13.1@0'),
    entry('wall-secret-c', 'wall.12.0@3'),
    entry(8, 'hand.0@0', { face: 18 }),
  ];
  const expected = build(entries, 3, 1);
  let seed = 319;
  for (let attempt = 0; attempt < 32; attempt++) {
    const shuffled = [...entries];
    for (let index = shuffled.length - 1; index > 0; index--) {
      seed = (seed * 1664525 + 1013904223) >>> 0;
      const other = seed % (index + 1);
      [shuffled[index], shuffled[other]] = [shuffled[other], shuffled[index]];
    }
    assert.deepEqual(build(shuffled, 3, 1), expected);
  }
});

test('fresh snapshots remove claimed rivers, moved tiles, deleted melds, and consumed wall slots', () => {
  const authority = new Map([
    entry(5, 'discard.0.0@0', { face: 19 }),
    entry('wall-secret', 'wall.0.0@2'),
  ]);
  const first = buildPublicBoard(authority.entries(), 0, 1);
  authority.set(5, info('meld.0.2@1', { face: 19 }));
  authority.delete('wall-secret');
  const second = buildPublicBoard(authority.entries(), 0, 1);
  assert.deepEqual(plain(second.seats[0].discards), []);
  assert.deepEqual(plain(second.seats[1].melds), [{
    group: 0, tiles: [face('meld.0.2@1', 19)],
  }]);
  assert.equal(second.wallCount, 0);
  authority.delete(5);
  assert.deepEqual(build(authority.entries()).seats, emptySeats());
  assert.deepEqual(plain(first.seats[0].discards), [face('discard.0.0@0', 19)]);
  assert.equal(first.wallCount, 1);
});

test('wall count uses all 108 bounded slot names, including opaque entries, and deduplicates them', () => {
  const entries = [];
  let ordinal = 0;
  for (const seat of [0, 1, 2, 3]) {
    for (let column = 0; column < (seat < 2 ? 14 : 13); column++) {
      for (const layer of [0, 1]) {
        const key = ordinal % 2 === 0 ? `wall-secret-${ordinal}` : ordinal;
        const value = info(`wall.${column}.${layer}@${seat}`);
        Object.defineProperty(value, 'face', { get() { throw new Error('Wall face was read'); } });
        entries.push([key, value]);
        ordinal++;
      }
    }
  }
  const board = build([...entries, ...entries, entry('extra-secret', 'wall.13.1@0', { face: 26 })]);
  assert.equal(board.wallCount, 108);
  assert.deepEqual(board.seats, emptySeats());
  assert.equal(JSON.stringify(board).includes('wall-secret'), false);
  assert.equal(JSON.stringify(board).includes('wall.13.1@0'), false);
});

test('malformed and noncanonical public or wall slot names are skipped, not partially matched', () => {
  const slots = [
    '', 'discard', 'discard.0@0', 'discard.0.0', 'discard.0.0@', 'discard.0.0@4',
    'discard.0.0@-1', 'discard.0.0@0.5', 'discard.0.0@00', 'discard.-1.0@0',
    'discard.3.0@0', 'discard.0.6@0', 'discard.00.0@0', 'discard.0.00@0',
    'discard.+0.0@0', 'discard.1e0.0@0', 'discard.0.0@0.extra',
    ' discard.0.0@0', 'discard.0.0@0 ', 'discard.0.0@0\n', 'discard.0.0@0\r\n',
    'Discard.0.0@0', 'meld.4.0@0', 'meld.0.5@0', 'meld.0.-1@0',
    'meld.0.0@-1', 'meld.0.0@4', 'meld.00.0@0', 'meld.0.04@0', 'meld.0.4@0\n',
    'wall.14.0@0', 'wall.14.0@1', 'wall.13.0@2', 'wall.13.1@3', 'wall.0.2@0',
    'wall.-1.0@0', 'wall.0.0@4', 'wall.00.0@0', 'wall.0.00@0', 'wall.0.0@0\n',
    'wall.9999999999999999999999999999.0@0', 'wall.0.0@0/hand.0@0',
  ];
  assert.deepEqual(build(slots.map(slot => entry(0, slot, { face: 17 }))), {
    viewerSeat: 0, activeSeat: null, seats: emptySeats(), wallCount: 0,
  });
});

test('malformed values, tuples, and unsupported key types do not crash or reveal faces', () => {
  const entries = [
    null, undefined, {}, 'bad', [], [0], [0, info('discard.0.0@0'), 'extra'],
    ...[null, undefined, 2, 'bad', true].map(value => [0, value]),
    ...[null, undefined, 2, {}, ['discard.0.0@0']].map(slot => entry(0, slot, { face: 17 })),
    ...[null, undefined, true, {}, [], Symbol('invalid')].map(key =>
      entry(key, 'discard.0.0@0', { face: 17 })),
  ];
  assert.deepEqual(build(entries), {
    viewerSeat: 0, activeSeat: null, seats: emptySeats(), wallCount: 0,
  });
});

test('the model does not mutate authority and produces deeply frozen ID-free projections', () => {
  const authority = [
    entry(53, 'discard.0.0@2', { face: 4, claimedBy: 1, shiftSlotName: 'hand.0@0' }),
    entry('opaque-secret-token', 'meld.0.4@1', { face: 17 }),
    entry('wall-secret-token', 'wall.0.0@0', { face: 22 }),
  ];
  for (const tuple of authority) {
    Object.freeze(tuple[1].heldRotation);
    Object.freeze(tuple[1]);
    Object.freeze(tuple);
  }
  Object.freeze(authority);
  const before = JSON.stringify(authority);
  const board = buildPublicBoard(authority.values(), 2, 0);
  assert.equal(JSON.stringify(authority), before);
  const checkFrozen = value => {
    if (value !== null && typeof value === 'object') {
      assert.equal(Object.isFrozen(value), true);
      for (const nested of Object.values(value)) checkFrozen(nested);
    }
  };
  checkFrozen(board);
  for (const seat of board.seats) {
    for (const tile of [...seat.discards, ...seat.melds.flatMap(meld => meld.tiles)]) {
      assert.deepEqual(Object.keys(tile).sort(), ['face', 'kind', 'slot']);
    }
  }
  const serialized = JSON.stringify(board);
  for (const privateField of [
    'opaque-secret-token', 'wall-secret-token', 'claimedBy', 'shiftSlotName', 'heldRotation',
    'rotationIndex', 'hand.0@0', '"key"', '"id"', '"index"', '"typeIndex"',
  ]) assert.equal(serialized.includes(privateField), false, privateField);
  assert.throws(() => { board.seats[2].discards[0].face = 26; });
  assert.throws(() => { board.seats.push({ seat: 0, discards: [], melds: [] }); });
  assert.throws(() => { board.wallCount = 108; });
});
