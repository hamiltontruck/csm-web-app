// Existing Blazor UI against local fixtures only; no production timesheet writes.
const { chromium, expect } = require('playwright/test');
const http = require('http'), { spawn } = require('child_process'), fs = require('fs'), assert = require('assert/strict');
(async () => {
  const company = '11111111-1111-1111-1111-111111111111', site = '22222222-2222-2222-2222-222222222222', equipment = '33333333-3333-3333-3333-333333333333', id = '44444444-4444-4444-4444-444444444444';
  const permissions = [...fs.readFileSync('CSM.Domain/Enums/GranularPermission.cs', 'utf8').matchAll(/^\s*(\w+)(?:\s*=\s*\d+)?\s*,?\s*$/gm)].map(x => x[1]).filter(x => x.includes('_'));
  let fail = true, reads = 0;
  const row = { id, equipmentId: equipment, equipmentCode: 'EQ001', equipmentName: 'Roller', siteId: site, siteName: 'MORMOR', workDate: '2026-11-14T00:00:00', operatorName: 'Operator', shift: 'Day', startTime: '08:00:00', endTime: '16:00:00', breakMinutes: 30, workedHours: 7.5, activity: 'Compaction', remarks: '' };
  const api = http.createServer((req, res) => { let chunks = []; req.on('data', x => chunks.push(x)); req.on('end', () => {
    const path = new URL(req.url, 'http://fixture').pathname.toLowerCase(); let status = 200, data = [];
    if (path === '/auth/login') data = { accessToken: 'local-timesheet', refreshToken: 'local-refresh', email: 'admin@example.test', fullName: 'Local SuperAdmin', companyId: null, roles: ['SuperAdmin'] };
    else if (path === '/auth/permissions') data = permissions;
    else if (path === '/auth/me' || path === '/auth/me/profile') data = { userId: id, companyId: null, name: 'Local SuperAdmin', email: 'admin@example.test', roles: ['SuperAdmin'] };
    else if (path === '/sites') data = [{ id: site, companyId: company, name: 'MORMOR', siteCode: 'SITE', isActive: true }];
    else if (path === '/equipmentassignments') data = [{ id, companyId: company, equipmentId: equipment, constructionSiteId: site, assignedAtUtc: '2026-11-13T00:00:00Z', releasedAtUtc: null }];
    else if (path === '/equipmenttimesheets') { reads++; if (fail) { status = 500; data = { message: 'private diagnostic' }; } else data = [row, { ...row, id: company, siteId: company, equipmentName: 'Foreign site equipment' }]; }
    else if (req.method !== 'GET') { status = 403; data = { message: 'Fixture denies writes' }; }
    res.writeHead(status, { 'Content-Type': 'application/json' }); res.end(JSON.stringify(data));
  }); });
  await new Promise(r => api.listen(5122, '127.0.0.1', r));
  const app = spawn('dotnet', ['run', '--project', 'CSM.Web/CSM.Web.csproj', '-c', 'Release', '--no-build', '--no-launch-profile'], { env: { ...process.env, ASPNETCORE_ENVIRONMENT: 'Development', ASPNETCORE_URLS: 'http://127.0.0.1:5123', ApiSettings__BaseUrl: 'http://127.0.0.1:5122/' }, stdio: ['ignore', 'ignore', 'inherit'] });
  let browser;
  try {
    for (let i = 0; i < 160; i++) { try { await fetch('http://127.0.0.1:5123/login'); break; } catch { await new Promise(r => setTimeout(r, 250)); } }
    browser = await chromium.launch({ headless: true, executablePath: process.env.CSM_CHROMIUM || undefined, args: ['--no-sandbox'] });
    for (const width of [390, 1280]) {
      fail = true; reads = 0; const context = await browser.newContext({ viewport: { width, height: 1000 } }), page = await context.newPage(), errors = [];
      page.on('pageerror', e => errors.push(e.message));
      await page.goto('http://127.0.0.1:5123/login'); await page.locator('#csm-email').fill('admin@example.test'); await page.locator('#csm-password').fill('Local-Fixture-Password-123!'); await page.locator('button[type=submit]').click(); await page.waitForURL('**/dashboard');
      await page.goto(`http://127.0.0.1:5123/equipment/timesheet/site/${site}`);
      await expect(page.getByRole('alert')).toContainText('Unable to load equipment timesheets'); await expect(page.getByRole('alert')).not.toContainText('private diagnostic');
      await expect(page.getByRole('button', { name: '+ New Timesheet', exact: true })).toHaveCount(0);
      fail = false; await page.locator('#retry-equipment-timesheet').click();
      await expect(page.getByText('EQ001 — Roller', { exact: true })).toBeVisible(); await expect(page.getByText('7.50 hrs', { exact: true })).toBeVisible();
      await expect(page.getByText('Foreign site equipment', { exact: false })).toHaveCount(0); await expect(page.getByRole('button', { name: '+ New Timesheet', exact: true })).toBeVisible();
      assert.equal(reads, 2); assert.equal(errors.length, 0); await expect(page.locator('#blazor-error-ui')).not.toBeVisible();
      console.log(`PASS ${width}px companyless SuperAdmin Daily Timesheets retry, history rendering and selected-site isolation`); await context.close();
    }
  } finally { await browser?.close(); app.kill('SIGTERM'); await new Promise(r => api.close(r)); }
})().catch(e => { console.error(e); process.exitCode = 1; });
