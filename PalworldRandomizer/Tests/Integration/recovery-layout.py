"""Check real initial recovery markup does not cover the canvas or hide the rescue button."""
from pathlib import Path
import re
from playwright.sync_api import sync_playwright

root = Path(__file__).resolve().parents[3]
html = (root / 'PalworldRandomizer.Browser/wwwroot/index.html').read_text()
# This is initial-DOM geometry, not a framework startup/renderer acceptance test.
html = re.sub(r'<script\b[^>]*>.*?</script>', '', html, flags=re.S)
with sync_playwright() as pw:
    browser = pw.chromium.launch(headless=True,
        executable_path='/root/.cache/ms-playwright/chromium-1217/chrome-linux64/chrome',
        args=['--no-sandbox'])
    for width, height in [(1100, 1000), (720, 560), (420, 700)]:
        page = browser.new_page(viewport={'width':width, 'height':height})
        page.set_content(html)
        bounds = page.evaluate('''() => {
            const rect = id => {
                const r = document.getElementById(id).getBoundingClientRect();
                return {x:r.x,y:r.y,right:r.right,bottom:r.bottom,width:r.width,height:r.height};
            };
            return {canvas:rect('out'),panel:rect('recovery-panel'),button:rect('manual-software-recovery')};
        }''')
        c, p, b = (bounds[k] for k in ('canvas', 'panel', 'button'))
        assert c['height'] > 0 and c['bottom'] <= p['y'] + 0.1, bounds
        assert 0 <= b['x'] < b['right'] <= width and 0 <= b['y'] < b['bottom'] <= height, bounds
        page.close()
    browser.close()
print('Recovery controls remain outside, disjoint from, and accessible beside the real canvas area.')
