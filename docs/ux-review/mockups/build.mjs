import fs from 'fs'
import { chromium } from 'playwright-core'
const css = fs.readFileSync('_base.css', 'utf8'), icons = fs.readFileSync('_icons.html', 'utf8')
const I = n => `<svg class="i"><use href="#${n}"/></svg>`
const wrap = body => `<!doctype html><html><head><meta charset="utf-8"><style>${css}</style></head><body>${icons}${body}</body></html>`
const tabbar = on => `<div class="tabbar">${[['home', 'Home'], ['dumb', 'Workouts'], ['cal', 'Split'], ['chart', 'Progress'], ['book', 'Library']].map(([i, l]) => `<div class="tab ${l === on ? 'on' : ''}">${I(i)}<span>${l}</span></div>`).join('')}</div>`
const topnav = on => `<div style="display:flex;align-items:center;gap:28px;padding:16px 26px;border-bottom:1px solid var(--hair);position:relative"><div style="font-weight:800;font-size:17px;letter-spacing:-.02em;margin-right:12px">GymRat</div>${['Home', 'Workouts', 'Split', 'Progress', 'Library'].map(l => `<div style="font-size:14px;font-weight:${l === on ? 700 : 500};color:${l === on ? 'var(--text)' : 'var(--text-3)'};padding:6px 0;border-bottom:2px solid ${l === on ? 'var(--emerald)' : 'transparent'}">${l}</div>`).join('')}<div style="margin-left:auto;display:flex;gap:10px"><div class="btn" style="padding:7px 12px;font-size:13px">${I('help')}How to use</div><div class="btn" style="padding:7px 12px;font-size:13px">${I('gear')}Settings</div></div></div>`
const wk = names => names.map(n => `<div class="card" style="padding:18px;margin-bottom:10px;border-radius:16px;display:flex;justify-content:space-between;font-weight:700;font-size:16px">${n}<span style="color:var(--text-3)">${I('more')}</span></div>`).join('')

const files = {}

files['GR-ux-1-navigation'] = wrap(`
<div class="row"><div><div class="cap">Desktop: persistent top navigation</div>
<div class="frame d">${topnav('Workouts')}<div class="pad" style="padding:28px 40px"><h1>My workouts</h1><div class="sub" style="margin-top:6px">Create, edit, and organize your routines.</div>
<div style="display:flex;gap:12px;margin:22px 0"><div class="card" style="flex:1;padding:13px 16px;color:var(--text-3);font-size:14px;border-radius:14px">Search workouts by name…</div><div class="btn p" style="padding:12px 20px">${I('plus')}New workout</div></div>
${wk(['Push day', 'Pull day', 'Leg day'])}</div></div>
<div class="note" style="max-width:900px">Replaces the floating Back pill and the dashboard-only module grid, so every page is one click from every other. The AI coach button stays bottom right.</div></div>
<div><div class="cap">Mobile: bottom tab bar</div>
<div class="frame m"><div class="pad"><h1>My workouts</h1><div class="sub" style="margin-top:6px">Create, edit, and organize your routines.</div>
<div class="btn p" style="margin:18px 0 14px">${I('plus')}New workout</div>${wk(['Push day', 'Pull day', 'Leg day'])}</div>${tabbar('Workouts')}</div>
<div class="note">No Back pill on top-level pages. Detail pages and the running workout keep an in-header back arrow. The tab bar hides during a running workout.</div></div></div>`)

const hdr = `<div style="display:flex;justify-content:space-between;align-items:center;padding:16px 18px"><div style="font-weight:800;font-size:17px">GymRat</div><div style="display:flex;gap:8px"><div class="btn" style="padding:7px 11px;font-size:12px">${I('help')}How to use</div><div class="btn" style="padding:7px 9px">${I('gear')}</div></div></div>`
const quick = `<div class="cap" style="margin:0 0 8px;font-size:11px">Jump to</div><div class="card" style="border-radius:16px">${[['dumb', 'Workouts'], ['cal', 'Training split'], ['chart', 'Progression'], ['book', 'Exercise library']].map(([i, l], k) => `<div style="display:flex;align-items:center;gap:12px;padding:13px 16px;${k ? 'border-top:1px solid var(--hair)' : ''};font-size:15px;font-weight:600"><span style="color:var(--text-2)">${I(i)}</span>${l}<span style="margin-left:auto;color:var(--text-4)">${I('chev')}</span></div>`).join('')}</div>`
files['GR-ux-2-dashboard'] = wrap(`
<div class="row">
<div><div class="cap">A. New account, nothing set up</div><div class="frame m">${hdr}<div class="pad" style="padding-top:6px"><div class="sub">Welcome, Alex</div><h1 style="margin:4px 0 14px">Let’s set up your first week</h1>
<div class="card" style="padding:4px 0;border-radius:18px">${[['Create a workout', 'Pick exercises and sets', 1], ['Assign it to days', 'Optional, but it powers “today”', 2], ['Log your first session', 'Start from today’s card', 3]].map(([t, s, n], k) => `<div style="display:flex;gap:12px;padding:14px 16px;${k ? 'border-top:1px solid var(--hair)' : ''}"><div style="width:26px;height:26px;border-radius:50%;border:1.5px solid var(--hair2);display:grid;place-items:center;font-size:12px;color:var(--text-2)">${n}</div><div><div style="font-weight:700;font-size:15px">${t}</div><div class="sub" style="font-size:13px">${s}</div></div></div>`).join('')}</div>
<div class="btn p" style="margin:16px 0 20px">Create your first workout</div>${quick}</div>${tabbar('Home')}</div></div>
<div><div class="cap">B. Workout scheduled today</div><div class="frame m">${hdr}<div class="pad" style="padding-top:6px"><div class="sub">Friday</div><h1 style="margin:4px 0 14px">Push day</h1>
<div class="card" style="padding:4px 0;border-radius:18px">${['Bench press', 'Incline bench press', 'Overhead press', 'Triceps pushdown'].map((t, k) => `<div style="display:flex;justify-content:space-between;padding:12px 16px;${k ? 'border-top:1px solid var(--hair)' : ''};font-size:15px"><span>${t}</span><span style="color:var(--text-3);font-size:13px">3 sets</span></div>`).join('')}</div>
<div class="btn p" style="margin:16px 0 8px">${I('play')}Start workout</div><div style="text-align:center;font-size:13px;color:var(--text-3);margin-bottom:18px">Last session: Wed, Nov 4</div>${quick}</div>${tabbar('Home')}</div></div>
<div><div class="cap">C. Rest day</div><div class="frame m">${hdr}<div class="pad" style="padding-top:6px"><div class="sub">Friday</div><h1 style="margin:4px 0 14px">Rest day</h1>
<div class="card" style="padding:16px;border-radius:18px"><div class="sub" style="font-size:13px">Next up</div><div style="font-weight:700;font-size:17px;margin-top:2px">Monday · Pull day</div></div>
<div class="btn" style="margin:16px 0 20px">Start a workout anyway</div>${quick}</div>${tabbar('Home')}</div></div>
</div><div class="note" style="max-width:1000px;margin-top:18px">Today’s workout is shown by name with its exercises, so the primary button is never a leap of faith. The colored module tiles become one plain list. One accent color only. “How to use” stays a header button that opens the modal.</div>`)

const setrow = (n, w, r, done) => `<div style="display:flex;align-items:center;gap:10px;padding:10px 12px;border-radius:16px;margin-bottom:8px;background:${done ? 'rgba(34,197,139,.12)' : 'var(--surface)'};border:1px solid ${done ? 'rgba(34,197,139,.3)' : 'var(--hair)'}"><div style="width:22px;color:var(--text-2);font-weight:700">${n}</div><div class="card" style="flex:1;text-align:center;padding:10px;border-radius:12px;font-weight:700;color:${w ? 'var(--text)' : 'var(--text-4)'}">${w || '–'}</div><div class="card" style="flex:1;text-align:center;padding:10px;border-radius:12px;font-weight:700;color:${r ? 'var(--text)' : 'var(--text-4)'}">${r || '–'}</div><div style="width:38px;height:38px;border-radius:12px;display:grid;place-items:center;background:${done ? 'var(--emerald)' : 'var(--surface-2)'};color:${done ? 'var(--on)' : 'var(--text-4)'}">${I('check')}</div></div>`
const runTop = `<div style="display:flex;align-items:center;gap:10px;padding:16px 18px"><div class="btn" style="padding:8px 10px;border-radius:12px">${I('back')}</div><div style="font-weight:700">Push day</div><div style="margin-left:auto;font-family:ui-monospace,Consolas,monospace;font-weight:700;background:var(--surface-2);padding:7px 12px;border-radius:999px;font-size:13px">12:41</div></div>`
const runBody = `<div class="pad" style="padding-top:2px"><div style="font-size:11px;letter-spacing:.12em;color:var(--emerald);font-weight:700">EXERCISE 1 OF 3</div><h1 style="font-size:26px;margin:4px 0 2px">Bench press</h1><div class="sub" style="font-size:13px;margin-bottom:14px">Chest · Barbell · Last time: 60 kg × 8</div>
<div style="display:flex;gap:10px;padding:0 12px;font-size:11px;letter-spacing:.1em;color:var(--text-3);margin-bottom:6px"><span style="width:22px">SET</span><span style="flex:1;text-align:center">KG</span><span style="flex:1;text-align:center">REPS</span><span style="width:38px"></span></div>
${setrow(1, '62.5', '8', true)}${setrow(2, '', '', false)}${setrow(3, '', '', false)}</div>`
files['GR-ux-3-workout-execution'] = wrap(`
<div class="row">
<div><div class="cap">A. Running: End is labeled</div><div class="frame m">${runTop}${runBody}
<div style="position:absolute;left:18px;right:18px;bottom:20px;display:flex;gap:10px"><div class="btn dng" style="padding:14px 16px">End</div><div class="btn" style="flex:0 0 auto">Previous</div><div class="btn p" style="flex:1">Next ${I('chev')}</div></div></div>
<div class="note">The red square is replaced by a text button. A rest timer pill sits above these buttons as today. The exercise name stays tappable for the form guide.</div></div>
<div><div class="cap">B. Confirm before ending</div><div class="frame m" style="opacity:1">${runTop}${runBody}
<div style="position:absolute;inset:0;background:rgba(3,5,10,.62)"></div>
<div style="position:absolute;left:10px;right:10px;bottom:10px;background:var(--surface-2);border:1px solid var(--hair2);border-radius:26px;padding:22px 18px 18px"><h2>End this workout?</h2><div class="sub" style="margin:6px 0 16px">1 of 3 exercises started. Sets you completed are already saved.</div><div class="btn p" style="margin-bottom:8px">Keep going</div><div class="btn dng">End workout</div></div></div>
<div class="note">Also used for the header back arrow, which today leaves immediately and drops the session.</div></div>
<div><div class="cap">C. Finish summary</div><div class="frame m"><div class="pad" style="padding-top:44px"><div style="width:44px;height:44px;border-radius:50%;background:rgba(34,197,139,.14);color:var(--emerald);display:grid;place-items:center;margin-bottom:14px">${I('check')}</div><h1>Workout finished</h1><div class="sub" style="margin:6px 0 18px">Push day · Fri, Nov 6</div>
<div style="display:flex;gap:10px;margin-bottom:16px">${[['Time', '48:12'], ['Sets', '9'], ['Volume', '3,420 kg']].map(([a, b]) => `<div class="card" style="flex:1;padding:12px;border-radius:14px"><div class="sub" style="font-size:11px">${a}</div><div style="font-weight:800;font-size:17px;margin-top:2px">${b}</div></div>`).join('')}</div>
<div class="card" style="border-radius:16px">${[['Bench press', '62.5 kg × 8', true], ['Incline bench press', '50 kg × 10', false], ['Bulgarian split squats', '24 kg × 10', false]].map(([n, b, pr], k) => `<div style="display:flex;justify-content:space-between;padding:13px 16px;${k ? 'border-top:1px solid var(--hair)' : ''};font-size:14px"><span>${n}</span><span style="color:var(--text-2)">${b}${pr ? ' <span class="chip" style="margin-left:6px;color:var(--emerald)">Best</span>' : ''}</span></div>`).join('')}</div></div>
<div style="position:absolute;left:18px;right:18px;bottom:22px"><div class="btn p" style="margin-bottom:8px">Done</div><div class="btn">View progression</div></div></div>
<div class="note">Today the finish screen shows only “Total time”. Best-set labels use the same estimate the Progression page already computes.</div></div>
</div>`)

const exCard = (n, sub, lvl, added) => `<div class="card" style="padding:14px 16px;border-radius:16px;display:flex;align-items:center;gap:12px;margin-bottom:10px"><div style="flex:1"><div style="font-weight:700;font-size:16px">${n}</div><div class="sub" style="font-size:13px;margin-top:2px">${sub}</div><div style="font-size:12px;color:var(--text-3);margin-top:6px">${lvl}</div></div><div style="width:40px;height:40px;border-radius:12px;display:grid;place-items:center;border:1px solid var(--hair2);background:${added ? 'rgba(34,197,139,.14)' : 'var(--surface-2)'};color:${added ? 'var(--emerald)' : 'var(--text)'}">${I(added ? 'check' : 'plus')}</div></div>`
files['GR-ux-4-workouts-and-library'] = wrap(`
<div class="row">
<div><div class="cap">A. Workouts: names only</div><div class="frame m"><div class="pad"><h1>My workouts</h1><div class="sub" style="margin:6px 0 16px">Create, edit, and organize your routines.</div>
<div class="btn p" style="margin-bottom:14px">${I('plus')}New workout</div>
${wk(['Push day', 'Pull day', 'Leg day', 'Upper body, heavy'])}
<div class="sub" style="font-size:12px;text-align:center;margin-top:6px">Tap a workout to edit its exercises</div></div>${tabbar('Workouts')}</div>
<div class="note">No exercise counts, no muscle chips on the row. The name gets the full line instead of wrapping to three lines. Rename and delete move into the ⋯ menu, where delete asks to confirm.</div></div>
<div><div class="cap">B. Editing a workout</div><div class="frame m"><div style="display:flex;align-items:center;gap:10px;padding:16px 18px"><div class="btn" style="padding:8px 10px;border-radius:12px">${I('back')}</div><div style="font-weight:800;font-size:18px">Push day</div><div style="margin-left:auto;font-size:13px;color:var(--text-3)">Saved</div></div><div class="pad" style="padding-top:4px">
<div class="card" style="padding:12px 14px;border-radius:14px;color:var(--text-3);font-size:14px;margin-bottom:12px">Add exercises: search by name or muscle…</div>
${['Bench press', 'Incline bench press', 'Overhead press'].map((n, k) => `<div class="card" style="padding:12px 14px;border-radius:14px;display:flex;align-items:center;gap:10px;margin-bottom:8px"><span style="color:var(--text-4)">⋮⋮</span><div style="flex:1"><div style="font-weight:700;font-size:15px">${n}</div><div class="sub" style="font-size:12px">${['Chest · Barbell', 'Chest · Barbell', 'Shoulders · Barbell'][k]}</div></div><div class="card" style="padding:8px 14px;border-radius:10px;font-weight:700;background:var(--surface-2)">3 <span style="font-weight:500;color:var(--text-3);font-size:12px">sets</span></div></div>`).join('')}</div></div>
<div class="note">Edits autosave, so the Save button and the stale “0 exercises” label disappear. The state is just “Saving…” / “Saved”.</div></div>
<div><div class="cap">C. Exercise library</div><div class="frame m"><div class="pad"><h1>Exercise library</h1><div class="card" style="padding:12px 14px;border-radius:14px;color:var(--text-3);font-size:14px;margin:14px 0 10px">Search by name or muscle…</div>
<div style="display:flex;gap:8px;margin-bottom:14px;overflow:hidden">${['All', 'Back', 'Biceps', 'Chest', 'Core'].map((c, k) => `<span class="chip" style="font-size:12px;letter-spacing:0;text-transform:none;padding:7px 13px;${k === 0 ? 'background:var(--text);color:#000;font-weight:700' : ''}">${c}</span>`).join('')}</div>
${exCard('Assisted pull-ups', 'Back · Biceps, rear deltoids', 'Beginner', false)}${exCard('Back extensions', 'Back · Lower back, glutes', 'Beginner', true)}${exCard('Barbell rows', 'Back · Biceps, rear deltoids', 'Intermediate', false)}${exCard('Deadlift', 'Back · Hamstrings, glutes', 'Advanced', false)}</div>${tabbar('Library')}</div>
<div class="note">The whole card opens details; the + adds to a workout without leaving the list. Difficulty is plain text, not green/blue/orange pills. Muscle chips replace the dropdown and the repeated section heading.</div></div>
</div>`)

const b = await chromium.launch({ executablePath: 'C:/Program Files/Google/Chrome/Application/chrome.exe' })
for (const [name, html] of Object.entries(files)) {
  fs.writeFileSync(name + '.html', html)
  const p = await b.newPage({ viewport: { width: 1400, height: 900 }, deviceScaleFactor: 2 })
  await p.setContent(html); await p.waitForTimeout(300)
  const w = await p.evaluate(() => Math.ceil(document.body.scrollWidth)), h = await p.evaluate(() => Math.ceil(document.body.scrollHeight))
  await p.setViewportSize({ width: w, height: h })
  await p.screenshot({ path: name + '.png', fullPage: true }); await p.close(); console.log(name, w, h)
}
await b.close()
