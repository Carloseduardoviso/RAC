'use strict';
const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const root = path.resolve(__dirname, '../../src/RAC.Web/wwwroot');
const getText = filename => fs.readFileSync(path.join(root, filename), 'utf8');

test('photorealistic 3D render exists as a local optimized WEBP asset', () => {
  const buffer = fs.readFileSync(path.join(root, 'trading-floor-photoreal.webp'));
  assert.equal(buffer.toString('ascii', 0, 4), 'RIFF');
  assert.equal(buffer.toString('ascii', 8, 12), 'WEBP');
  assert.equal(crypto.createHash('sha256').update(buffer).digest('hex'),
    '514134ff5e66fa4102b6bcd8b5ab5bff3a5b58ab519e2ed3adede2517fdc71c6');
});

test('active scene uses one photo stylesheet and a separate responsive stylesheet', () => {
  const html = getText('index.html');
  assert.match(html, /id="officePhotoreal"/);
  assert.match(html, /trading-floor-photo\.css\?v=3/);
  assert.match(html, /trading-floor-responsive\.css\?v=3/);
  assert.doesNotMatch(html, /trading-floor-v2\.css/);
  assert.ok(html.indexOf('trading-floor.js?v=3') < html.indexOf('app.js?v=3'),
    'map HUD must be loaded before simulation renderer');
});

test('photoreal CSS has no !important and retains interactive agent overlays', () => {
  for (const file of ['styles.css', 'trading-floor-photo.css', 'trading-floor-responsive.css']) {
    assert.doesNotMatch(getText(file), /!important/, file);
  }
  const css = getText('trading-floor-photo.css');
  assert.match(css, /trading-floor-photoreal\.webp/);
  assert.match(css, /#officePhotoreal \.office-operations \.office-stations/);
  assert.match(css, /#officePhotoreal \.office-leadership \.office-stations/);
  assert.match(css, /\.office-station:focus-within/);
});

test('scene labels are real DOM elements rather than baked profit numbers', () => {
  const html = getText('index.html');
  for (const id of ['officeDayResult', 'officeCountAnalyzing',
    'officeCountOperating', 'officeCountWaiting', 'officeCountPaused']) {
    assert.ok(html.includes('id="' + id + '"'), id);
  }
});
