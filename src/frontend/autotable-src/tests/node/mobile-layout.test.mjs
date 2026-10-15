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
const module = { exports: {} };
runInNewContext(ts.transpileModule(fs.readFileSync(path.join(root, 'src/mobile-layout.ts'), 'utf8'), {
  compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
}).outputText, {
  module, exports: module.exports, URLSearchParams,
  require: id => { throw new Error(`The mobile layout helper must not import ${id}`); },
});
const { parseMobileLayout, resolveMobileLayout, isReadableBoardPhase } = module.exports;

test('mobile layout parsing is exact, explicit and independent of Three or browser globals', () => {
  for (const [search, expected] of [
    ['?mobileLayout=camera', 'camera'], ['?mobileLayout=hybrid', 'hybrid'],
    ['?mobileLayout=readable', 'readable'],
    ['mobileLayout=camera', 'camera'], ['?mobileLayout=%63amera', 'camera'],
    ['?variant=changsha&mobileLayout=hybrid&stats=1', 'hybrid'],
    ['', null], ['?variant=changsha', null], ['?mobileLayout', null],
    ['?mobileLayout=', null], ['?mobileLayout=off', null], ['?mobileLayout=1', null],
    ['?mobileLayout=Camera', null], ['?mobileLayout=HYBRID', null],
    ['?mobileLayout=camera+', null], ['?mobileLayout=%20hybrid', null],
    ['?MobileLayout=camera', null], ['?other=mobileLayout%3Dcamera', null],
    ['?mobileLayout=camera-extra', null],
  ]) assert.equal(parseMobileLayout(search), expected, search);
});

test('duplicate flags always disable the experiment, including identical or empty duplicates', () => {
  for (const search of [
    '?mobileLayout=camera&mobileLayout=camera',
    '?mobileLayout=hybrid&mobileLayout=hybrid',
    '?mobileLayout=camera&mobileLayout=hybrid',
    '?mobileLayout=&mobileLayout=camera',
    '?mobileLayout=hybrid&mobileLayout',
    '?mobileLayout=invalid&mobileLayout=hybrid',
    '?mobileLayout=readable&mobileLayout=hybrid',
    '?mobileLayout=readable&mobileLayout=readable',
    '?mobileLayout=camera&mobile%4cayout=camera',
  ]) assert.equal(parseMobileLayout(search), null, search);
  assert.equal(parseMobileLayout('?other=1&other=2&mobileLayout=camera'), 'camera');
});

test('only a positive, finite full CSS portrait viewport at or below 600px can activate', () => {
  for (const mode of ['camera', 'hybrid', 'readable']) {
    for (const [width, height] of [[390, 844], [600, 1000], [600, 600.01], [.5, 1]]) {
      assert.equal(resolveMobileLayout(mode, { width, height, changsha: true }), mode);
    }
    for (const [width, height] of [
      [601, 1000], [600.01, 1000], [820, 1180], [1280, 900],
      [844, 390], [390, 390], [0, 844], [-1, 844], [390, 0],
      [NaN, 844], [390, NaN], [Infinity, 844], [390, Infinity],
      ['390', 844], [390, '844'],
    ]) assert.equal(resolveMobileLayout(mode, { width, height, changsha: true }), null, `${width}x${height}`);
  }
});

test('actual Changsha scope is required; the query flag does not declare a variant or connection', () => {
  for (const mode of ['camera', 'hybrid', 'readable']) {
    assert.equal(resolveMobileLayout(mode, { width: 390, height: 844, changsha: false }), null);
  }
  for (const mode of [null, undefined, '', 'off', 'invalid', 'CAMERA']) {
    assert.equal(resolveMobileLayout(mode, { width: 390, height: 844, changsha: true }), null);
  }
});

test('the readable board requires authoritative play or validated end-of-hand signals, never a deal phase', () => {
  for (const phase of ['AwaitingDiscard', 'AwaitingClaim', 'awaitingdiscard']) {
    assert.equal(isReadableBoardPhase(phase, false, false), true);
  }
  for (const phase of ['Scoring', 'EndHand', 'WallExhausted']) {
    assert.equal(isReadableBoardPhase(phase, false, true), false);
    assert.equal(isReadableBoardPhase(phase, true, false), true);
  }
  assert.equal(isReadableBoardPhase('GameComplete', false, true), true);
  assert.equal(isReadableBoardPhase('GameComplete', true, false), false);
  for (const phase of [
    undefined, '', 'unknown', 'Seating', 'RollingDice', 'Dealing', 'BreakPointMarked',
    'PickupRound1', 'PickupRound2', 'PickupRound3', 'SingleTilePickup', 'DealerExtra', 'RotatingBanker',
  ]) {
    assert.equal(isReadableBoardPhase(phase, true, true), false, `${phase}`);
  }
});
