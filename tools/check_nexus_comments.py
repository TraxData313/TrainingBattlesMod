#!/usr/bin/env python3
"""
check_nexus_comments.py — reports new comments/posts on a Nexus Mods mod page.

WHY A REAL BROWSER: Nexus Mods sits behind Cloudflare bot management. A plain
HTTP request (requests/curl/Invoke-WebRequest) gets a flat 403 even with normal
browser headers and a residential IP — confirmed while building this script.
This tool does not attempt to defeat that check (no header/TLS spoofing, no
CAPTCHA solving); it simply drives a real Chromium browser via Playwright, the
same way a human visitor's browser would load the page.

SETUP (one-time):
    pip install playwright
    playwright install chromium

USAGE:
    python check_nexus_comments.py --url "https://www.nexusmods.com/mountandblade2bannerlord/mods/12119?tab=posts"

    First run just records a baseline (every comment currently on the page) and
    reports nothing as "new" — otherwise the first run would dump the mod's
    entire comment history. Every run after that reports only comments whose
    ID wasn't seen before.

    --show-all       print every comment currently on the page, not just new ones
    --json           machine-readable output instead of the human-readable report
    --state-dir DIR  where the per-mod "seen comments" file is kept (default:
                      next to this script)

NOTE ON SCOPE: Nexus loads additional comment pages via JavaScript (no plain
page-2 URL), so this only reads the first page — which is also the page new
top-level comments/replies land on, so it's what you want for "did anything
new show up".

EXIT CODES: 0 = ran fine, no new comments. 2 = ran fine, new comments found.
1 = something went wrong (Cloudflare challenge page, no comments section
found, Playwright not installed, etc).
"""

import argparse
import hashlib
import json
import sys
from datetime import datetime, timezone
from pathlib import Path

EXTRACT_JS = """
() => {
    const results = [];
    function walk(li, parentId) {
        const id = li.id.replace('comment-', '');
        const nameLink = li.querySelector(':scope > .comment-head .comment-name a');
        const timeEl = li.querySelector(':scope > .comment-content time');
        const contentEl = li.querySelector(':scope > .comment-content .comment-content-text');
        results.push({
            id: id,
            parentId: parentId,
            author: nameLink ? nameLink.textContent.trim() : null,
            profileUrl: nameLink ? nameLink.href : null,
            dateEpoch: timeEl ? parseInt(timeEl.getAttribute('data-date'), 10) : null,
            dateText: timeEl ? timeEl.textContent.trim() : null,
            text: contentEl ? contentEl.textContent.trim() : null,
        });
        const kids = li.querySelector(':scope > .comment-kids');
        if (kids) {
            kids.querySelectorAll(':scope > li.comment').forEach(child => walk(child, id));
        }
    }
    document.querySelectorAll('#comment-container > ol > li.comment').forEach(li => walk(li, null));
    return results;
}
"""


def state_file_for(url: str, state_dir: Path) -> Path:
    slug = hashlib.sha1(url.encode("utf-8")).hexdigest()[:12]
    return state_dir / f"nexus_comments_{slug}.json"


def load_state(path: Path) -> dict:
    if not path.exists():
        return {"seen_ids": [], "last_checked": None}
    return json.loads(path.read_text(encoding="utf-8"))


def save_state(path: Path, seen_ids: set) -> None:
    payload = {
        "seen_ids": sorted(seen_ids),
        "last_checked": datetime.now(timezone.utc).isoformat(),
    }
    path.write_text(json.dumps(payload, indent=2), encoding="utf-8")


def fetch_comments(url: str, timeout_ms: int = 30000) -> list:
    try:
        from playwright.sync_api import sync_playwright
    except ImportError:
        print(
            "Playwright isn't installed. Run:\n"
            "  pip install playwright\n"
            "  playwright install chromium",
            file=sys.stderr,
        )
        sys.exit(1)

    with sync_playwright() as p:
        browser = p.chromium.launch(headless=True)
        try:
            page = browser.new_page(
                user_agent=(
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
                    "(KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36"
                )
            )
            page.goto(url, timeout=timeout_ms, wait_until="domcontentloaded")

            title = page.title()
            if "just a moment" in title.lower() or "attention required" in title.lower():
                print(
                    f"Blocked by a Cloudflare challenge page (title: {title!r}). "
                    "This script doesn't attempt to solve it — open the page in a "
                    "real browser once, then try again.",
                    file=sys.stderr,
                )
                sys.exit(1)

            try:
                page.wait_for_selector("#comment-container", timeout=timeout_ms)
            except Exception:
                print(
                    "Never found the comments section on the page (layout changed, "
                    "wrong URL, or still blocked). Page title was: "
                    f"{title!r}",
                    file=sys.stderr,
                )
                sys.exit(1)

            return page.evaluate(EXTRACT_JS)
        finally:
            browser.close()


def main():
    parser = argparse.ArgumentParser(description="Check a Nexus Mods page for new comments.")
    parser.add_argument("--url", required=True, help="Mod page URL, e.g. .../mods/12119?tab=posts")
    parser.add_argument("--state-dir", default=None, help="Directory to keep the seen-comments file in")
    parser.add_argument("--show-all", action="store_true", help="Print every comment, not just new ones")
    parser.add_argument("--json", action="store_true", help="Machine-readable output")
    args = parser.parse_args()

    state_dir = Path(args.state_dir) if args.state_dir else Path(__file__).resolve().parent
    state_dir.mkdir(parents=True, exist_ok=True)
    state_path = state_file_for(args.url, state_dir)

    state = load_state(state_path)
    seen_ids = set(state["seen_ids"])
    is_first_run = state["last_checked"] is None

    comments = fetch_comments(args.url)
    current_ids = {c["id"] for c in comments}

    if is_first_run:
        new_comments = []
    else:
        new_comments = [c for c in comments if c["id"] not in seen_ids]

    save_state(state_path, current_ids)

    to_print = comments if args.show_all else new_comments

    if args.json:
        print(json.dumps({
            "first_run": is_first_run,
            "total_comments": len(comments),
            "new_comments": new_comments,
            "printed": to_print,
        }, indent=2))
    else:
        if is_first_run:
            print(f"First run — recorded a baseline of {len(comments)} existing comments.")
            print("Run this again later to see new ones.")
        elif not new_comments:
            print(f"No new comments. ({len(comments)} total on the page.)")
        else:
            print(f"{len(new_comments)} new comment(s):\n")

        for c in to_print:
            reply_marker = "  ↳ reply — " if c["parentId"] else ""
            print(f"{reply_marker}{c['author']}  ({c['dateText']})")
            print(f"  {c['text']}")
            print(f"  {args.url.split('?')[0]}?tab=posts#comment-{c['id']}\n")

    sys.exit(2 if new_comments and not is_first_run else 0)


if __name__ == "__main__":
    main()
