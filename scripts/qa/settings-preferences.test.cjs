const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');
function browser(cookie = '') {
  const attributes = {}, classes = new Set(), storage = new Map(); let systemChange;
  const document = { cookie, documentElement: {
    setAttribute: (key, value) => attributes[key] = value,
    classList: { toggle: (name, on) => on ? classes.add(name) : classes.delete(name) }
  }};
  const window = { matchMedia: () => ({ matches: true, addEventListener: (_, cb) => systemChange = cb }),
    sessionStorage: { getItem: key => storage.get(key) ?? null, setItem: (key, value) => storage.set(key, value) } };
  const file = path.join(__dirname, '../../CSM.Web/wwwroot/js/settingsPreferences.js');
  if (fs.existsSync(file)) vm.runInNewContext(fs.readFileSync(file, 'utf8'), { window, document });
  return { window, document, attributes, classes, storage, change: dark => systemChange({ matches: dark }) };
}
test('System theme follows system changes and explicit Light overrides it', () => {
  const b = browser(); assert.equal(typeof b.window.csmTheme?.apply, 'function');
  b.window.csmTheme.apply('system'); assert.equal(b.attributes['data-bs-theme'], 'dark');
  b.change(false); assert.equal(b.attributes['data-bs-theme'], 'light');
  b.window.csmTheme.apply('light'); b.change(true); assert.equal(b.attributes['data-bs-theme'], 'light');
});
test('compact preview does not persist and discard restores saved display', () => {
  const b = browser(); assert.equal(typeof b.window.csmTheme?.setCompact, 'function');
  b.window.csmTheme.setCompact(true); assert.ok(b.classes.has('csm-compact')); assert.equal(b.document.cookie, '');
  b.window.csmTheme.setCompact(false); assert.ok(!b.classes.has('csm-compact'));
});
test('culture rejects unsupported cookie values without writing or reload', () => {
  const b = browser(); assert.equal(typeof b.window.csmCulture?.set, 'function');
  assert.equal(b.window.csmCulture.set('xx;path=/'), false); assert.equal(b.document.cookie, '');
  assert.equal(b.window.csmCulture.set('om'), true); assert.ok(decodeURIComponent(b.document.cookie).includes('c=om|uic=om'));
});
test('remembered navigation belongs to the account and disabled preference does not read it', () => {
  const b = browser(); assert.equal(typeof b.window.csmNavigation?.save, 'function');
  b.window.csmNavigation.save('account-a', 'admin', false, true);
  assert.equal(b.window.csmNavigation.load('account-a', 'admin', true), false);
  assert.equal(b.window.csmNavigation.load('account-b', 'admin', true), true);
  assert.equal(b.window.csmNavigation.load('account-a', 'admin', false), true);
});
test('personal account download writes the supplied bytes and releases its object URL', async () => {
  let blob, clicked=false, removed=false, revoked=false;
  const link={ click:()=>clicked=true, remove:()=>removed=true };
  const window={ URL:{ createObjectURL:value=>{blob=value;return 'blob:test';}, revokeObjectURL:url=>revoked=url==='blob:test' } };
  const document={ createElement:()=>link, body:{ appendChild:()=>{} } };
  vm.runInNewContext(fs.readFileSync(path.join(__dirname,'../../CSM.Web/wwwroot/js/download.js'),'utf8'),
    {window,document,Blob,Uint8Array,atob,setTimeout:callback=>callback()});
  assert.equal(typeof window.downloadFileFromBytes,'function');
  window.downloadFileFromBytes('personal.json','application/json',Buffer.from('{"account":"own"}').toString('base64'));
  assert.equal(await blob.text(),'{"account":"own"}');assert.equal(link.download,'personal.json');
  assert.ok(clicked && removed && revoked);
});
