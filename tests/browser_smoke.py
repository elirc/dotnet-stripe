"""Real browser + published ASP.NET app. Uses only its own temporary database.

Usage: python tests/browser_smoke.py --output .artifacts/browser-run-01
Build scripts/publish-local.ps1 first. Install requirements-browser.txt and Chromium.
ASTRA_BROWSER_EXECUTABLE optionally selects an existing Chromium executable.
"""

from pathlib import Path
from datetime import datetime
import argparse
import json
import os
import socket
import subprocess
import tempfile
import time
import urllib.request
import uuid
from playwright.sync_api import sync_playwright, expect

root = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser()
parser.add_argument("--output", required=True)
args = parser.parse_args()
output = Path(args.output).resolve()
output.mkdir(parents=True, exist_ok=False)
package = root / ".artifacts/publish"
assert (package / "Billing.Api.dll").is_file(), "Run scripts/publish-local.ps1 first."
checks = []
errors = []
result = {"passed": False, "checks": checks, "pageErrors": errors}
process = None
browser = None
page = None


def checked(message):
    checks.append(message)
    print(message, flush=True)


with tempfile.TemporaryDirectory(prefix="billing-browser-") as temporary:
    private_root = Path(temporary).resolve()
    assert private_root.parent == Path(tempfile.gettempdir()).resolve()
    env = os.environ.copy()
    env["Billing__Database"] = str(private_root / "workshop.sqlite")
    env["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1"
    with socket.socket() as probe:
        probe.bind(("127.0.0.1", 0))
        port = probe.getsockname()[1]
    base = f"http://127.0.0.1:{port}"
    with (output / "server.log").open("w", encoding="utf-8") as log:
        try:
            process = subprocess.Popen(
                ["dotnet", "Billing.Api.dll", "--urls", base],
                cwd=package,
                env=env,
                stdout=log,
                stderr=subprocess.STDOUT,
                creationflags=subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0,
            )
            deadline = time.monotonic() + 60
            while True:
                if process.poll() is not None:
                    raise RuntimeError("Packaged API exited; inspect server.log.")
                try:
                    with urllib.request.urlopen(
                        base + "/api/health", timeout=2
                    ) as response:
                        assert response.status == 200
                    break
                except OSError:
                    if time.monotonic() > deadline:
                        raise TimeoutError("Packaged API did not become ready.")
                    time.sleep(0.2)
            with sync_playwright() as playwright:
                browser = playwright.chromium.launch(
                    headless=True,
                    executable_path=os.environ.get("ASTRA_BROWSER_EXECUTABLE") or None,
                )
                context = browser.new_context(
                    viewport={"width": 1440, "height": 1050}, timezone_id="UTC"
                )
                page = context.new_page()
                page.on("pageerror", lambda error: errors.append(str(error)))
                page.goto(base)
                expect(page.get_by_role("heading", name="Welcome back")).to_be_visible()
                checked(
                    "Published package serves the real React application and static assets"
                )
                page.get_by_role(
                    "button", name="Create a new workspace", exact=True
                ).click()
                page.get_by_label("Your name", exact=True).fill("Morgan Learner")
                page.get_by_label("Workspace name", exact=True).fill(
                    "Studio Billing Lab"
                )
                page.get_by_label("Workspace code").fill("studio-lab")
                page.get_by_label("Email", exact=True).fill("morgan@example.test")
                page.get_by_label("Password", exact=True).fill("workshop-password")
                with page.expect_response(
                    lambda response: response.url.endswith("/api/auth/register")
                ) as registration:
                    page.get_by_role(
                        "button", name="Create workspace", exact=True
                    ).click()
                session = registration.value.json()
                assert registration.value.status == 201
                token = session["token"]
                expect(
                    page.get_by_role("navigation", name="Workspace sections")
                ).to_be_visible()
                checked(
                    "Browser registration creates a private workspace and authenticated session"
                )
                assert page.evaluate("localStorage.length + sessionStorage.length") == 0
                assert not context.cookies()
                checked(
                    "Session credentials are absent from local storage, session storage and cookies"
                )

                def api(method, path, body=None, version=None):
                    headers = {
                        "Authorization": "Bearer " + token,
                        "Idempotency-Key": str(uuid.uuid4()),
                    }
                    if version is not None:
                        headers["If-Match"] = f'"v{version}"'
                    response = page.request.fetch(
                        base + "/api" + path, method=method, headers=headers, data=body
                    )
                    assert response.ok, (response.status, response.text())
                    return response.json()

                def tab(name):
                    page.get_by_role(
                        "navigation", name="Workspace sections"
                    ).get_by_role("button", name=name, exact=True).click()
                    expect(
                        page.get_by_role("heading", name=name, exact=True)
                    ).to_be_visible()

                def save():
                    page.get_by_role("button", name="Save changes", exact=True).click()
                    expect(
                        page.get_by_role("region", name="Record editor")
                    ).to_have_count(0)
                    expect(page.get_by_role("alert")).to_have_count(0)

                page.get_by_role("button", name="Add customer", exact=True).click()
                page.get_by_label("Customer name", exact=True).fill("Ada Studio")
                page.get_by_label("Customer email", exact=True).fill("ada@example.test")
                save()
                expect(
                    page.get_by_role("button", name="Edit Ada Studio", exact=True)
                ).to_be_visible()
                customer = api("GET", "/customers")["items"][0]
                checked("Customer creation persists through the browser form")
                page.get_by_role("button", name="Edit Ada Studio", exact=True).click()
                page.get_by_label("Customer name", exact=True).fill(
                    "Ada draft from browser"
                )
                api(
                    "PUT",
                    "/customers/" + customer["id"],
                    {
                        "name": "Ada winner",
                        "email": customer["email"],
                        "currency": "USD",
                        "isArchived": False,
                    },
                    1,
                )
                page.get_by_role("button", name="Save changes", exact=True).click()
                expect(page.get_by_role("alert")).to_contain_text("changed")
                expect(page.get_by_label("Customer name", exact=True)).to_have_value(
                    "Ada draft from browser"
                )
                page.get_by_role(
                    "button", name="Discard draft and reload latest", exact=True
                ).click()
                expect(page.get_by_label("Customer name", exact=True)).to_have_value(
                    "Ada winner"
                )
                page.get_by_label("Customer name", exact=True).fill("Ada Studio")
                save()
                assert api("GET", "/customers/" + customer["id"])["version"] == 3
                checked(
                    "Stale edit is rejected, the draft survives, and explicit reload allows a reviewed edit"
                )

                dropped = {"done": False}

                def lose_response(route):
                    if route.request.method == "POST" and not dropped["done"]:
                        response = route.fetch()
                        assert response.status == 201
                        dropped["done"] = True
                        route.abort("failed")
                    else:
                        route.continue_()

                page.route("**/api/customers", lose_response)
                page.get_by_role("button", name="Add customer", exact=True).click()
                page.get_by_label("Customer name", exact=True).fill("Lost Response Co")
                page.get_by_label("Customer email", exact=True).fill(
                    "lost@example.test"
                )
                page.get_by_role("button", name="Save changes", exact=True).click()
                expect(
                    page.get_by_role("button", name="Retry exact operation")
                ).to_be_visible()
                page.get_by_role("button", name="Retry exact operation").click()
                expect(page.get_by_role("region", name="Record editor")).to_have_count(
                    0
                )
                page.unroute("**/api/customers", lose_response)
                assert api("GET", "/customers?search=lost@example.test")["total"] == 1
                checked(
                    "A response lost after commit retries the same operation and creates exactly one customer"
                )
                page.get_by_role(
                    "button", name="Edit Lost Response Co", exact=True
                ).click()
                page.get_by_role("button", name="Delete record…", exact=True).click()
                page.get_by_role("button", name="Keep record", exact=True).click()
                assert api("GET", "/customers?search=lost@example.test")["total"] == 1
                page.get_by_role("button", name="Delete record…", exact=True).click()
                page.get_by_role("button", name="Confirm delete", exact=True).click()
                expect(page.get_by_role("region", name="Record editor")).to_have_count(
                    0
                )
                assert api("GET", "/customers?search=lost@example.test")["total"] == 0
                checked("Deletion requires confirmation and removes an unused record")

                tab("Plans")
                page.get_by_role("button", name="Add plan", exact=True).click()
                page.get_by_label("Plan name", exact=True).fill("Studio Hybrid")
                page.get_by_label("Plan code").fill("studio-hybrid")
                page.get_by_label("Pricing model", exact=True).select_option("Hybrid")
                page.get_by_label("Base price", exact=True).fill("20")
                page.get_by_label("Included units", exact=True).fill("100")
                page.get_by_label("Price per extra unit", exact=True).fill("0.10")
                save()
                plan = api("GET", "/plans")["items"][0]
                tab("Coupons")
                page.get_by_role("button", name="Add coupon", exact=True).click()
                page.get_by_label("Coupon name", exact=True).fill("Welcome 20")
                page.get_by_label("Coupon code", exact=True).fill("welcome-20")
                page.get_by_label("Percentage discount").fill("20")
                save()
                coupon = api("GET", "/coupons")["items"][0]
                checked(
                    "Plan and coupon forms persist typed pricing and discount contracts"
                )
                tab("Subscriptions")
                page.get_by_role("button", name="Add subscription", exact=True).click()
                page.get_by_label("Customer", exact=True).select_option(customer["id"])
                page.get_by_label("Plan", exact=True).select_option(plan["id"])
                page.get_by_label("Coupon", exact=True).select_option(coupon["id"])
                save()
                sub = api("GET", "/subscriptions")["items"][0]
                tab("Usage")
                page.get_by_role("button", name="Add usage", exact=True).click()
                page.get_by_label("Subscription", exact=True).select_option(sub["id"])
                page.get_by_label("Quantity", exact=True).fill("150")
                save()
                checked(
                    "Related record selectors create a subscription and correctly timed usage"
                )
                page.get_by_text("Billing simulator controls", exact=True).click()
                target = (
                    datetime.fromisoformat(sub["currentPeriodEnd"])
                    .replace(tzinfo=None)
                    .isoformat(timespec="milliseconds")
                )
                page.get_by_label("Advance to (local time)", exact=True).fill(target)
                page.get_by_role(
                    "button", name="Advance billing clock", exact=True
                ).click()
                expect(
                    page.get_by_role("status").filter(has_text="Saved")
                ).to_be_visible()
                invoice = api("GET", "/invoices")["items"][0]
                assert invoice["subtotal"]["minorUnits"] == 2500
                assert invoice["discount"]["minorUnits"] == 500
                assert invoice["total"]["minorUnits"] == 2000
                checked(
                    "Billing creates the expected USD 25 subtotal, USD 5 discount and USD 20 invoice"
                )
                tab("Invoices")
                page.get_by_role(
                    "button", name="Inspect " + invoice["number"], exact=True
                ).click()
                page.get_by_role(
                    "button", name="Queue simulated payment", exact=True
                ).click()
                expect(page.get_by_role("region", name="Record details")).to_have_count(
                    0
                )
                assert api("GET", "/payments")["items"][0]["status"] == "Pending"
                page.get_by_role(
                    "button", name="Process as decline", exact=True
                ).click()
                expect(
                    page.get_by_role("status").filter(has_text="Saved")
                ).to_be_visible()
                assert api("GET", "/payments")["items"][0]["status"] == "Failed"
                page.get_by_role(
                    "button", name="Inspect " + invoice["number"], exact=True
                ).click()
                page.get_by_role(
                    "button", name="Queue simulated payment", exact=True
                ).click()
                expect(page.get_by_role("region", name="Record details")).to_have_count(
                    0
                )
                page.get_by_role(
                    "button", name="Process as success", exact=True
                ).click()
                expect(
                    page.get_by_role("status").filter(has_text="Saved")
                ).to_be_visible()
                assert api("GET", "/invoices/" + invoice["id"])["status"] == "Paid"
                assert sorted(
                    item["status"] for item in api("GET", "/payments")["items"]
                ) == ["Failed", "Succeeded"]
                assert all(
                    item["status"] == "Simulated"
                    for item in api("GET", "/webhooks")["items"]
                )
                checked(
                    "Payment controls preserve a decline, succeed a new attempt, and simulate the durable outbox"
                )

                for index in range(22):
                    api(
                        "POST",
                        "/customers",
                        {
                            "name": f"Practice Studio {index:02}",
                            "email": f"studio{index}@example.test",
                            "currency": "USD",
                        },
                    )
                tab("Customers")
                expect(
                    page.get_by_role("button", name="Next", exact=True)
                ).to_be_enabled()
                page.get_by_role("button", name="Next", exact=True).click()
                expect(page.get_by_text("21–23 of 23", exact=True)).to_be_visible()
                page.get_by_label("Search customers", exact=True).fill(
                    "Practice Studio 21"
                )
                expect(page.get_by_text("1–1 of 1", exact=True)).to_be_visible()
                page.get_by_label("Search customers", exact=True).fill("")
                expect(page.get_by_text("1–20 of 23", exact=True)).to_be_visible()
                checked(
                    "Server pagination and search agree on counts and reset the page offset"
                )

                api(
                    "POST",
                    "/members",
                    {
                        "name": "Review Reader",
                        "email": "reader@example.test",
                        "password": "workshop-password",
                        "role": "Viewer",
                    },
                )
                reader_context = browser.new_context(
                    viewport={"width": 1100, "height": 850}
                )
                reader_page = reader_context.new_page()
                reader_page.goto(base)
                reader_page.get_by_label("Workspace code").fill("studio-lab")
                reader_page.get_by_label("Email", exact=True).fill(
                    "reader@example.test"
                )
                reader_page.get_by_label("Password", exact=True).fill(
                    "workshop-password"
                )
                reader_page.get_by_role("button", name="Sign in", exact=True).click()
                expect(
                    reader_page.get_by_role("navigation", name="Workspace sections")
                ).to_be_visible()
                expect(
                    reader_page.get_by_role("button", name="Add customer", exact=True)
                ).to_have_count(0)
                expect(
                    reader_page.get_by_text("Billing simulator controls", exact=True)
                ).to_have_count(0)
                expect(
                    reader_page.get_by_role("navigation").get_by_role(
                        "button", name="People", exact=True
                    )
                ).to_have_count(0)
                reader_context.close()
                checked(
                    "Viewer sign-in renders read-only controls and hides owner membership actions"
                )

                expect(page.get_by_role("alert")).to_have_count(0)
                page.screenshot(path=str(output / "desktop.png"), full_page=True)
                page.set_viewport_size({"width": 390, "height": 844})
                assert page.evaluate(
                    "document.documentElement.scrollWidth <= window.innerWidth"
                )
                page.screenshot(path=str(output / "narrow.png"), full_page=True)
                checked("Desktop and narrow layouts render without horizontal overflow")
                assert not errors, errors
                checked("No uncaught browser JavaScript errors occurred")
                context.close()
                browser.close()
                browser = None
                result["passed"] = True
        except Exception as error:
            result["error"] = repr(error)
            if page is not None:
                try:
                    page.screenshot(path=str(output / "failure.png"), full_page=True)
                except Exception:
                    pass
            raise
        finally:
            if browser is not None:
                try:
                    browser.close()
                except Exception:
                    pass
            if process is not None and process.poll() is None:
                process.terminate()
                try:
                    process.wait(timeout=10)
                except subprocess.TimeoutExpired:
                    process.kill()
                    process.wait(timeout=10)
            result["serverStopped"] = process is None or process.poll() is not None
            (output / "result.json").write_text(
                json.dumps(result, indent=2), encoding="utf-8"
            )
print(
    json.dumps(
        {"passed": result["passed"], "checks": len(checks), "output": str(output)}
    )
)
