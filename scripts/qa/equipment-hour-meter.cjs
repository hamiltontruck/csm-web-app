// Real Blazor/local API fixture only; no production hour-meter writes.
const { chromium, expect } = require('playwright/test');
const http = require('http'), { spawn } = require('child_process'), fs = require('fs'), assert = require('assert/strict');
(async () => {
  const company = '11111111-1111-1111-1111-111111111111', site = '22222222-2222-2222-2222-222222222222', equipment = '33333333-3333-3333-3333-333333333333', id = '44444444-4444-4444-4444-444444444444';
  const permissions = [...fs.readFileSync('CSM.Domain/Enums/GranularPermission.cs', 'utf8').matchAll(/^\s*(\w+)(?:\s*=\s*\d+)?\s*,?\s*$/gm)].map(x => x[1]).filter(x => x.includes('_'));
  let loadFail = true, saveFail = true, writes = [], rows = [];
  const row = { id, equipmentId: equipment, equipmentCode: 'EQ001', equipmentName: 'Roller', projectId: site, projectName: 'MORMOR', loggedAtUtc: '2026-11-14T00:00:00Z', previousMeterReading: 0, currentMeterReading: 2, hoursWorked: 2, operatorName: 'Operator', activity: 'Rolling', remarks: 'Existing entry' };
  const api = http.createServer((req, res) => { const chunks = []; req.on('data', x => chunks.push(x)); req.on('end', () => {
    const path = new URL(req.url, 'http://fixture').pathname.toLowerCase(); let status = 200, data = [];
    if (path === '/auth/login') data = { accessToken: 'local-hour', refreshToken: 'local-refresh', email: 'admin@example.test', fullName: 'Local SuperAdmin', companyId: null, roles: ['SuperAdmin'] };
    else if (path === '/auth/permissions') data = permissions;
    else if (path === '/auth/me' || path === '/auth/me/profile') data = { userId: id, companyId: null, name: 'Local SuperAdmin', email: 'admin@example.test', roles: ['SuperAdmin'] };
    else if (path === '/sites') data = [{ id: site, companyId: company, name: 'MORMOR', siteCode: 'SITE', isActive: true }];
    else if (path === '/equipmentassignments') data = [{ id, companyId: company, equipmentId: equipment, constructionSiteId: site, assignedAtUtc: '2026-11-13T00:00:00Z', releasedAtUtc: null }];
    else if (path === '/equipment') data = [{ id: equipment, companyId: company, equipmentCode: 'EQ001', name: 'Roller', status: 'Available', currentMeterReading: 2, isActive: true }];
    else if (path === '/equipmenthourlogs' && req.method === 'POST') {
      const body = JSON.parse(Buffer.concat(chunks)); writes.push(body);
      if (saveFail) { status = 500; data = { message: 'private diagnostic' }; }
      else { data = { ...row, ...body, id, previousMeterReading: body.previousMeter, currentMeterReading: body.currentMeter, hoursWorked: body.currentMeter - body.previousMeter }; rows = [data]; }
    } else if (path === '/equipmenthourlogs') { if (loadFail) { status = 500; data = { message: 'private diagnostic' }; } else data = [...rows, { ...row, id: company, projectId: company, equipmentName: 'Foreign site equipment', remarks: 'Foreign site history' }]; }
    res.writeHead(status, { 'Content-Type': 'application/json' }); res.end(JSON.stringify(data));
  }); });
  await new Promise(r => api.listen(5106, '127.0.0.1', r));
  const app = spawn('dotnet', ['run', '--project', 'CSM.Web/CSM.Web.csproj', '-c', 'Release', '--no-build', '--no-launch-profile'], { env: { ...process.env, ASPNETCORE_ENVIRONMENT: 'Development', ASPNETCORE_URLS: 'http://127.0.0.1:5107', ApiSettings__BaseUrl: 'http://127.0.0.1:5106/' }, stdio: ['ignore', 'ignore', 'inherit'] });
  let browser;
  try {
    for (let i = 0; i < 160; i++) { try { await fetch('http://127.0.0.1:5107/login'); break; } catch { await new Promise(r => setTimeout(r, 250)); } }
    browser = await chromium.launch({ headless: true, executablePath: process.env.CSM_CHROMIUM || undefined, args: ['--no-sandbox'] });
    for (const width of [390, 1280]) {
      loadFail = saveFail = true; writes = []; rows = [row]; const context = await browser.newContext({ viewport: { width, height: 1000 } }), page = await context.newPage(), errors = [];
      page.on('pageerror', e => errors.push(e.message));
      await page.goto('http://127.0.0.1:5107/login'); await page.locator('#csm-email').fill('admin@example.test'); await page.locator('#csm-password').fill('Local-Fixture-Password-123!'); await page.locator('button[type=submit]').click(); await page.waitForURL('**/dashboard');
      await page.goto(`http://127.0.0.1:5107/equipment/hourmeter/site/${site}`); await expect(page.getByRole('alert')).toContainText('Unable to load hour meter'); loadFail = false; await page.getByRole('button', { name: 'Retry', exact: true }).click(); await expect(page.getByText('EQ001 — Roller', { exact: true })).toBeVisible(); await expect(page.getByText('Foreign site history')).toHaveCount(0);
      const add = page.getByRole('button', { name: 'New Hour Entry', exact: false }); await expect(add).toBeVisible(); const box = await add.boundingBox(); assert(box && box.x >= 0 && box.x + box.width <= width + 1, 'New Hour Entry outside viewport'); await add.click(); await page.waitForURL(`**/equipment/hourmeter/create/${site}`);
      const field = name => page.locator('label').filter({ hasText: name }).locator('..').locator('input,select,textarea').first();
      await field('Equipment').selectOption(equipment); await field('Log Date').fill('2026-11-14'); await field('Previous Meter').fill('2'); await field('Current Meter').fill('3.5'); await field('Operator').fill('Operator'); await field('Activity').fill('Rolling'); await page.locator('textarea').fill('Local hour meter test'); const save = page.getByRole('button', { name: 'Save Hour Entry', exact: true }); await save.click(); await expect(page.getByRole('alert')).toBeVisible(); await expect(page.getByRole('alert')).not.toContainText('private diagnostic'); await expect(page.locator('textarea')).toHaveValue('Local hour meter test');
      saveFail = false; await save.click(); await page.waitForURL(`**/equipment/hourmeter/site/${site}`); await expect(page.getByText('1.50 hrs', { exact: true })).toBeVisible(); assert.equal(writes.length, 2); const body = writes[1]; assert.equal(body.siteId, site); assert.equal(body.equipmentId, equipment); assert.equal(body.previousMeter, 2); assert.equal(body.currentMeter, 3.5); assert.equal(body.remarks, 'Local hour meter test'); assert.equal(errors.length, 0); await expect(page.locator('#blazor-error-ui')).not.toBeVisible();
      console.log(`PASS ${width}px companyless SuperAdmin Hour Meter load/retry, site history filter, visible New Hour Entry, retained input and successful save`); await context.close();
    }
  } finally { await browser?.close(); app.kill('SIGTERM'); await new Promise(r => api.close(r)); }
})().catch(e => { console.error(e); process.exitCode = 1; });
