#!/usr/bin/env python3
"""Real PHP preview + Chromium rendering; no live accounts or packages used.
Requires playwright and `python -m playwright install chromium`.
"""
import base64,json,subprocess,tempfile
from pathlib import Path
from playwright.sync_api import sync_playwright
R=Path(__file__).resolve().parents[1]
png='iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIW2P4z8DwHwAFgAI/ScLbtAAAAABJRU5ErkJggg=='
doc=dict(format='orange-site',version=1,title='</script><script>parent.PWNED=1</script>',width=960,height=600,viewportMode='viewport',background='#ffffff',assets=[dict(name='test.png',media='image/png',data=png)],pages=[dict(path='/about',title='About',width=960,height=600,background='#ffeecc',elements=[dict(id='about',kind='text',text='About screen',x=10,y=20,width=200,height=40)])],elements=[dict(id='text',kind='text',text='<img onerror="parent.PWNED=2">',x=30,y=40,width=300,height=40,fontSize=20),dict(id='pic',kind='image',image='test.png',x=30,y=100,width=200,height=100),dict(id='nav',kind='button',text='Go to About',x=250,y=100,width=200,height=60,actions=[dict(command='GoToPage',arguments=['/about'])])])
code="function is_admin(){return true;}function h($s){return htmlspecialchars((string)$s,ENT_QUOTES,'UTF-8');}require "+json.dumps(str(R/'server/sites_preview.php'))+";$doc=json_decode(stream_get_contents(STDIN),true);hosted_preview_render($doc);"
p=subprocess.run(['php','-r',code],input=json.dumps(doc),text=True,capture_output=True,check=True);assert not p.stderr,p.stderr
with sync_playwright()as pw:
 browser=pw.chromium.launch(headless=True,args=['--no-sandbox']);page=browser.new_page(viewport=dict(width=1200,height=900));errors=[];requests=[];page.on('pageerror',lambda e:errors.append(str(e)));page.on('request',lambda r:requests.append(r.url));page.set_content(p.stdout)
 frame=page.frames[1];frame.locator('#page button').wait_for();assert frame.locator('#page button').inner_text()=='Go to About'
 assert frame.locator('#page').locator('img').count()==0 and frame.locator('#page div').first.inner_text().startswith('<img ')
 assert frame.locator('#page div').nth(1).evaluate('(e)=>getComputedStyle(e).backgroundImage').startswith('url("data:image/png;base64,')
 assert page.evaluate('typeof PWNED')=='undefined'
 frame.locator('#page button').click();assert frame.locator('#page').inner_text()=='About screen';assert frame.locator('#address').inner_text()=='/about'
 frame.locator('#routes').select_option('/');assert frame.locator('#page button').is_visible()
 gallery=page.locator('details img').first;assert gallery.evaluate('(e)=>e.complete&&e.naturalWidth===1');page.locator('summary').click();assert page.locator('details img').nth(1).is_visible()
 page.set_viewport_size(dict(width=700,height=900));page.wait_for_timeout(200);assert frame.locator('#page').evaluate('(e)=>e.getBoundingClientRect().width')<710
 assert not errors,errors;assert not [u for u in requests if u.startswith(('http:','https:'))],requests
 browser.close()
print('PASS: 10 real PHP/Chromium admin preview checks: site appearance, images/gallery, page navigation, resize, escaped text, sandbox and no outbound requests')
