// Real Blazor/local fixture only; never creates production maintenance records.
const { chromium, expect } = require('playwright/test');
const http = require('http'), { spawn } = require('child_process'), fs = require('fs'), assert = require('assert/strict');
(async () => {
  const company = '11111111-1111-1111-1111-111111111111', site = '22222222-2222-2222-2222-222222222222', equipment = '33333333-3333-3333-3333-333333333333', id = '44444444-4444-4444-4444-444444444444';
  const permissions = [...fs.readFileSync('CSM.Domain/Enums/GranularPermission.cs', 'utf8').matchAll(/^\s*(\w+)(?:\s*=\s*\d+)?\s*,?\s*$/gm)].map(x => x[1]).filter(x => x.includes('_'));
  let fail = 500, writes = [], rows = [];
  const api = http.createServer((req, res) => { let chunks = []; req.on('data', x => chunks.push(x)); req.on('end', () => {
    const path = new URL(req.url, 'http://fixture').pathname.toLowerCase(); let status = 200, data = [];
    if (path === '/auth/login') data = { accessToken: 'local-maintenance', refreshToken: 'local-refresh', email: 'admin@example.test', fullName: 'Local SuperAdmin', companyId: null, roles: ['SuperAdmin'] };
    else if (path === '/auth/permissions') data = permissions;
    else if (path === '/auth/me' || path === '/auth/me/profile') data = { userId: id, companyId: null, name: 'Local SuperAdmin', email: 'admin@example.test', roles: ['SuperAdmin'] };
    else if (path === '/sites') data = [{ id: site, companyId: company, name: 'MORMOR', siteCode: 'SITE', isActive: true }];
    else if (path === '/equipmentassignments') data = [{ id, companyId: company, equipmentId: equipment, constructionSiteId: site, equipmentCode: 'EQ001', equipmentName: 'Roller', assignedAtUtc: '2026-11-13T00:00:00Z', releasedAtUtc: null }];
    else if (path === '/equipment') data = [{ id: equipment, companyId: company, equipmentCode: 'EQ001', name: 'Roller', status: 'Available', isActive: true }];
    else if (path === '/equipmentmaintenance' && req.method === 'POST') {
      const body = JSON.parse(Buffer.concat(chunks)); writes.push(body);
      if (fail) { status = fail; data = { message: fail === 400 ? 'Construction site was not found in the equipment company.' : 'private diagnostic: database failure' }; }
      else { data = { id, companyId: company, ...body, equipmentCode: 'EQ001', equipmentName: 'Roller' }; rows = [data]; }
    } else if (path === '/equipmentmaintenance') data = rows;
    res.writeHead(status, { 'Content-Type': 'application/json' }); res.end(JSON.stringify(data));
  }); });
  await new Promise(r => api.listen(5104, '127.0.0.1', r));
  const app = spawn('dotnet', ['run', '--project', 'CSM.Web/CSM.Web.csproj', '-c', 'Release', '--no-build', '--no-launch-profile'], { env: { ...process.env, ASPNETCORE_ENVIRONMENT: 'Development', ASPNETCORE_URLS: 'http://127.0.0.1:5105', ApiSettings__BaseUrl: 'http://127.0.0.1:5104/' }, stdio: ['ignore', 'ignore', 'inherit'] });
  let browser;
  try {
    for (let i = 0; i < 160; i++) { try { await fetch('http://127.0.0.1:5105/login'); break; } catch { await new Promise(r => setTimeout(r, 250)); } }
    browser = await chromium.launch({ headless: true, executablePath: process.env.CSM_CHROMIUM || undefined, args: ['--no-sandbox'] });
    for (const width of [390, 1280]) {
      fail = 500; writes = []; rows = []; const context = await browser.newContext({ viewport: { width, height: 1000 } }), page = await context.newPage(), errors = [];
      page.on('pageerror', e => errors.push(e.message));
      await page.goto('http://127.0.0.1:5105/login'); await page.locator('#csm-email').fill('admin@example.test'); await page.locator('#csm-password').fill('Local-Fixture-Password-123!'); await page.locator('button[type=submit]').click(); await page.waitForURL('**/dashboard');
      await page.goto(`http://127.0.0.1:5105/equipment/maintenance/create/${site}`);
      const field = name => page.locator('label').filter({ hasText: name }).locator('..').locator('input,select,textarea').first();
      await expect(page.locator('form')).toBeVisible(); await field('Equipment').selectOption(equipment); await field('Maintenance Type').fill('service'); await field('Scheduled Date').fill('2026-11-14'); await field('Meter Reading').fill('3.5'); await field('Cost').fill('25'); await field('Description').fill('Local maintenance test');
      const save = page.getByRole('button', { name: 'Save Maintenance', exact: true }); await save.click(); await expect(page.getByRole('alert')).toContainText('Unable to save maintenance'); await expect(page.getByRole('alert')).not.toContainText('private diagnostic'); await expect(field('Maintenance Type')).toHaveValue('service'); await expect(field('Meter Reading')).toHaveValue('3.5');
      fail = 400; await save.click(); await expect(page.getByRole('alert')).toContainText('equipment company'); await expect(field('Description')).toHaveValue('Local maintenance test');
      fail = 0; await save.click(); await page.waitForURL(`**/equipment/maintenance/site/${site}`); await expect(page.getByText('Roller', { exact: false }).first()).toBeVisible(); assert.equal(writes.length, 3); const body = writes[2]; assert.equal(body.siteId, site); assert.equal(body.equipmentId, equipment); assert.equal(body.maintenanceType, 'service'); assert.equal(body.meterReading, 3.5); assert.equal(body.cost, 25); assert.equal(body.currencyCode, 'ETB'); assert(body.scheduledAtUtc.startsWith('2026-11-14')); assert.equal(body.description, 'Local maintenance test'); assert.equal(errors.length, 0); await expect(page.locator('#blazor-error-ui')).not.toBeVisible();
      console.log(`PASS ${width}px companyless SuperAdmin maintenance form, site/equipment payload, safe500/domain400, retained inputs and successful retry`);
      await context.close();
    }
  } finally { await browser?.close(); app.kill('SIGTERM'); await new Promise(r => api.close(r)); }
})().catch(e => { console.error(e); process.exitCode = 1; });
