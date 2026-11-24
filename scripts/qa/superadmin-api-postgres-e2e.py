#!/usr/bin/env python3
"""Real CSM API + disposable GitHub Actions PostgreSQL smoke.
Never accepts a public host, external database or production credentials.
"""
import json
import os
import subprocess
import sys
import urllib.error
import urllib.request
from uuid import UUID

BASE = "http://127.0.0.1:5189"
DB = "csm_superadmin_e2e"
EMAIL = "superadmin-qa@example.test"
PASSWORD = "Qa-Temporary-Password-2026!"

def require_isolated_runner():
    if os.environ.get("GITHUB_ACTIONS") != "true" or os.environ.get("CSM_DISPOSABLE_API_QA") != "1":
        raise RuntimeError("Refusing to run outside the isolated GitHub Actions QA job")
    connection = os.environ.get("ConnectionStrings__DefaultConnection", "").lower()
    for item in ("host=127.0.0.1", "database=" + DB, "username=postgres", "ssl mode=disable"):
        if item not in connection:
            raise RuntimeError("Non-disposable PostgreSQL target blocked: " + item)
    if os.environ.get("ASPNETCORE_URLS") != BASE:
        raise RuntimeError("Non-loopback API target blocked")

def check(condition, message):
    if not condition:
        raise AssertionError(message)

def valid_guid(value):
    return str(UUID(str(value)))

def api(method, path, payload=None, token=None):
    if not path.startswith("/api/") or "//" in path:
        raise ValueError("Only local CSM API routes are permitted")
    data = json.dumps(payload).encode() if payload is not None else None
    headers = {"Content-Type": "application/json"}
    if token:
        headers["Authorization"] = "Bearer " + token
    req = urllib.request.Request(BASE + path, data=data, headers=headers, method=method)
    try:
        with urllib.request.urlopen(req, timeout=25) as response:
            code, content = response.status, response.read()
    except urllib.error.HTTPError as e:
        code, content = e.code, e.read()
    try:
        body = json.loads(content) if content else None
    except (ValueError, UnicodeDecodeError):
        body = None
    return code, body

def expect_status(method, path, status, payload=None, token=None):
    code, body = api(method, path, payload, token)
    if code != status:
        detail = json.dumps(body, default=str)[:450] if body is not None else "no json"
        raise AssertionError(f"{method} {path}: expected HTTP {status}, got {code}: {detail}")
    return body

def sql(statement):
    require_isolated_runner()
    env = dict(os.environ, PGPASSWORD="postgres")
    result = subprocess.run([
        "psql", "-X", "-v", "ON_ERROR_STOP=1",
        "-h", "127.0.0.1", "-p", "5432", "-U", "postgres", "-d", DB,
        "-At", "-c", statement
    ], env=env, capture_output=True, text=True, timeout=25, check=True)
    return result.stdout.strip()

def main():
    require_isolated_runner()
    expect_status("GET", "/api/equipment", 401)
    bad = expect_status("POST", "/api/auth/login", 401,
                        {"email": EMAIL, "password": "Invalid-Once-Only", "deviceName": "qa"})
    check(bad is not None, "Missing unauthorized login response")
    auth = expect_status("POST", "/api/auth/login", 200,
                         {"email": EMAIL, "password": PASSWORD, "deviceName": "disposable-qa"})
    token = auth.get("accessToken")
    check(bool(token) and auth.get("companyId") is None and "SuperAdmin" in auth.get("roles", []),
          "Bootstrap login must be a real companyless SuperAdmin")
    me = expect_status("GET", "/api/auth/me", 200, token=token)
    check(me.get("companyId") is None, "Authenticated SuperAdmin must remain companyless")
    print("PASS actual HTTP /auth/login + /auth/me + anonymous 401")

    def company(code):
        row = expect_status("POST", "/api/companies", 201, {
            "code": code, "name": "Disposable " + code,
            "baseCurrencyCode": "ETB"
        }, token)
        return valid_guid(row["id"])

    alpha, beta = company("QA-ALPHA"), company("QA-BETA")
    options = expect_status("GET", "/api/equipment/create-options", 200, token=token)
    check(options.get("canSelectCompany") is True, "SuperAdmin must choose target company")
    ids = {valid_guid(c["id"]) for c in options.get("companies", [])}
    check(alpha in ids and beta in ids, "Missing active QA companies in selector")

    payload = {"companyId": beta, "equipmentCode": "QA-BETA-EQ",
               "name": "Original roller", "meterUnit": "Hours", "isActive": True}
    expect_status("POST", "/api/equipment", 400, {**payload, "companyId": None}, token)
    created = expect_status("POST", "/api/equipment", 201, payload, token)
    equipment_id = valid_guid(created["id"])
    check(valid_guid(created["companyId"]) == beta, "Equipment tenant creation mismatch")
    updated = expect_status("PUT", "/api/equipment/" + equipment_id, 200, {
        "equipmentCode": "QA-BETA-EQ", "name": "Updated roller",
        "meterUnit": "Hours", "currentMeterReading": 25,
        "isActive": True, "status": "Available", "ownershipType": "Owned"
    }, token)
    saved = expect_status("GET", "/api/equipment/" + equipment_id, 200, token=token)
    check(updated["name"] == "Updated roller" and saved["name"] == "Updated roller"
          and saved["currentMeterReading"] == 25
          and valid_guid(saved["companyId"]) == beta, "Equipment save/read mismatch")
    own = expect_status("POST", "/api/equipment", 201, {
        **payload, "companyId": alpha, "equipmentCode": "QA-ALPHA-EQ"
    }, token)
    own_id = valid_guid(own["id"])
    print("PASS real DB company selector, create/edit/save/view and tenant ownership")

    def site(company_id, code):
        s = expect_status("POST", "/api/sites", 201, {
            "companyId": company_id, "siteCode": code, "name": code
        }, token)
        return valid_guid(s["id"])

    alpha_site = site(alpha, "QA-ALPHA-SITE")
    beta_site = site(beta, "QA-BETA-SITE")
    maintenance_request = {
        "equipmentId": equipment_id, "maintenanceType": "Inspection",
        "scheduledAtUtc": "2026-11-20T10:00:00Z", "currencyCode": "ETB",
        "meterReading": 25, "cost": 0, "siteId": alpha_site
    }
    expect_status("POST", "/api/equipmentmaintenance", 400, maintenance_request, token)
    good_maintenance = expect_status("POST", "/api/equipmentmaintenance", 201,
                                     {**maintenance_request, "siteId": beta_site}, token)
    check(valid_guid(good_maintenance["companyId"]) == beta,
          "Maintenance company must come from equipment")
    print("PASS real API rejects cross-company equipment/site and accepts same-tenant linkage")

    original = sql("""SELECT COUNT(*) FROM "RoleGranularPermissions" p
        JOIN "Roles" r ON p."RoleId" = r."Id"
        WHERE r."NormalizedName"='SUPERADMIN'
          AND p."Permission"='Procurement_Inventory_Adjust'""")
    check(int(original) == 1, "Expected one seeded SuperAdmin Adjust grant")
    sql("""DELETE FROM "RoleGranularPermissions"
        WHERE "RoleId" = (SELECT "Id" FROM "Roles" WHERE "NormalizedName"='SUPERADMIN')
        AND "Permission" = 'Procurement_Inventory_Adjust'""")
    expect_status("POST", "/api/equipment", 403,
                  {**payload, "equipmentCode": "FORBIDDEN-EQ"}, token)
    expect_status("PUT", "/api/equipment/" + equipment_id, 403, {
        "equipmentCode": "QA-BETA-EQ", "name": "Forbidden edit"
    }, token)
    persisted = expect_status("GET", "/api/equipment/" + equipment_id, 200, token=token)
    check(persisted["name"] == "Updated roller", "Denied update mutated equipment")
    print("PASS live grant revocation: POST/PUT 403, GET allowed, record unchanged")

    sql(f"""UPDATE "Users" SET "CompanyId"='{alpha}'
        WHERE "NormalizedEmail"='{EMAIL.upper()}'""")
    sql(f"""UPDATE "UserRoles" SET "RoleId"=
        (SELECT "Id" FROM "Roles" WHERE "NormalizedName"='COMPANYADMIN')
        WHERE "UserId"=(SELECT "Id" FROM "Users"
            WHERE "NormalizedEmail"='{EMAIL.upper()}')""")
    current = expect_status("GET", "/api/auth/me", 200, token=token)
    check(valid_guid(current["companyId"]) == alpha
          and "SuperAdmin" not in current.get("roles", [])
          and "CompanyAdmin" in current.get("roles", []),
          "Existing JWT must reflect current DB role/company")
    expect_status("GET", "/api/equipment/" + own_id, 200, token=token)
    denied_code, _ = api("GET", "/api/equipment/" + equipment_id, token=token)
    check(denied_code in (403, 404, 400), "Foreign equipment read unexpectedly allowed")
    after = sql(f"""SELECT COUNT(*) FROM "Equipment"
        WHERE "Id"='{equipment_id}' AND "CompanyId"='{beta}' AND "Name"='Updated roller'""")
    check(after == "1", "Foreign denial must preserve original equipment")
    print("PASS existing JWT re-evaluates company/role; foreign tenant denied; no mutation")
    print("PASS SuperAdmin actual HTTP/PostgreSQL integration acceptance slice")

if __name__ == "__main__":
    try:
        main()
    except Exception as error:
        print(f"FAIL {type(error).__name__}: {error}", file=sys.stderr)
        sys.exit(1)
