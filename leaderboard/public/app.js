const $ = id => document.getElementById(id);
const classes = ['Gladiator', 'Templar', 'Assassin', 'Ranger', 'Sorcerer', 'Spiritmaster', 'Cleric', 'Chanter', 'Unknown'];
for (const name of classes) $('class').add(new Option(name, name));
let mode = 'verified', receipt, pendingUpload, offset = 0, nextOffset = null, rankingRequest = 0;
const format = n => Math.round(n).toLocaleString();
const key = () => [...crypto.getRandomValues(new Uint8Array(32))].map(b => b.toString(16).padStart(2, '0')).join('');
function cell(row, value, className) {
  const td = row.insertCell(); td.textContent = value;
  if (className) td.className = className;
  return td;
}
async function api(path, options) {
  const response = await fetch(path, options);
  let data;
  try { data = await response.json(); } catch { throw Error('The archive did not return a valid response. Please try again.'); }
  if (!response.ok) throw Error(data.error || 'Request failed');
  return data;
}
async function rankings() {
  const request = ++rankingRequest;
  $('rows').setAttribute('aria-busy', 'true');
  try {
    const query = new URLSearchParams({mode, boss: $('boss').value, class: $('class').value, offset, limit: 50});
    const data = await api('/api/leaderboard?' + query);
    if (request !== rankingRequest) return;
    nextOffset = data.nextOffset; $('rows').replaceChildren();
    data.rows.forEach((entry, index) => {
      const row = $('rows').insertRow(); cell(row, String(offset + index + 1).padStart(2, '0'));
      const player = cell(row, entry.name), label = document.createElement('small');
      label.textContent = entry.className; player.append(label);
      const link = document.createElement('a'); link.href = '#fight=' + entry.id; link.textContent = entry.boss + ' ↗';
      cell(row, '').append(link);
      cell(row, Math.floor(entry.durationSeconds / 60) + ':' + String(Math.floor(entry.durationSeconds % 60)).padStart(2, '0'));
      cell(row, format(entry.dps), 'score');
      const badge = document.createElement('span'); badge.className = 'badge'; badge.textContent = 'Unverified';
      cell(row, '').append(badge);
    });
    $('empty').hidden = data.rows.length > 0;
    $('empty').textContent = mode === 'verified'
      ? 'No verified records yet. Trusted verification is required before rankings can open.'
      : 'No public encounters match these filters. Upload a fight to start the archive.';
    $('previous').hidden = offset === 0; $('next').hidden = nextOffset === null;
    $('page-note').textContent = data.rows.length ? `Showing ${offset + 1}–${offset + data.rows.length}` : '';
  } catch (error) {
    if (request !== rankingRequest) return;
    $('rows').replaceChildren(); $('empty').hidden = false; $('empty').textContent = error.message;
    $('previous').hidden = $('next').hidden = true;
  } finally { if (request === rankingRequest) $('rows').removeAttribute('aria-busy'); }
}
async function bosses() {
  const {bosses} = await api('/api/catalog'), selected = $('boss').value;
  $('boss').replaceChildren(new Option('All bosses', ''));
  for (const boss of bosses) $('boss').add(new Option(boss, boss));
  if (bosses.includes(selected)) $('boss').value = selected;
}
for (const selected of ['verified', 'community']) $(selected).onclick = () => {
  mode = selected; offset = 0;
  for (const value of ['verified', 'community']) $(value).setAttribute('aria-pressed', String(value === mode));
  $('trust').textContent = mode === 'verified'
    ? 'Verified rankings are locked until trusted combat validation exists.'
    : 'UNVERIFIED · Self-reported captured damage. Scores may be incomplete or altered and never enter verified rankings.';
  rankings();
};
$('boss').onchange = $('class').onchange = () => { offset = 0; rankings(); };
$('previous').onclick = () => { offset = Math.max(0, offset - 50); rankings(); };
$('next').onclick = () => { if (nextOffset !== null) { offset = nextOffset; rankings(); } };
async function readFight() {
  const file = $('file').files[0];
  if (!file || file.size > 65536) throw Error('Choose a JSON file under 64 KiB.');
  const fight = JSON.parse(await file.text());
  if (!fight || fight.schemaVersion !== 1 || !Array.isArray(fight.players)) throw Error('Choose a GoatMeter v1 fight export. Raw validation logs are private diagnostics and cannot be uploaded here.');
  return fight;
}
$('file').onchange = async () => {
  pendingUpload = null;
  try {
    const fight = await readFight(); $('boss-label').value = fight.boss || '';
    $('file-preview').textContent = `${fight.players.length} player aliases · ${fight.durationSeconds} seconds · ${format(fight.players.reduce((n, p) => n + (Number(p.damage) || 0), 0))} captured damage. Review your encounter label and visibility before uploading.`;
  } catch (error) { $('file-preview').textContent = error.message; }
};
function showReceipt() {
  const readKey = receipt.visibility === 'private' ? receipt.ownerToken : receipt.shareToken;
  receipt.url = location.origin + '/logs/#fight=' + receipt.id + (receipt.visibility === 'public' ? '' : '&key=' + readKey);
  $('keys').textContent = JSON.stringify(receipt, null, 2);
  $('open-fight').href = receipt.url; $('receipt').hidden = false; $('delete').disabled = false; $('change-visibility').disabled = false;
  deleteArmed = false; $('delete').textContent = 'Delete this upload';
  $('manage-visibility').value = receipt.visibility; $('manage-status').textContent = '';
  $('receipt').scrollIntoView({behavior: 'smooth'});
}
$('upload-form').onsubmit = async event => {
  event.preventDefault(); const button = event.target.querySelector('button'); button.disabled = true;
  try {
    const input = await readFight(); input.boss = $('boss-label').value; input.visibility = $('visibility').value;
    const body = JSON.stringify(input);
    if (pendingUpload?.body !== body) pendingUpload = {body, key: key()};
    receipt = {...await api('/api/fights', {method: 'POST', headers: {'Content-Type': 'application/json', Authorization: 'Bearer ' + pendingUpload.key}, body}), visibility: input.visibility};
    showReceipt(); pendingUpload = null;
    $('upload-status').textContent = 'Uploaded as unverified. Save your access receipt below.';
    await bosses(); await rankings();
  } catch (error) { $('upload-status').textContent = error.message; }
  finally { button.disabled = false; }
};
$('restore-receipt').onchange = async () => {
  try {
    const file = $('restore-receipt').files[0]; if (!file || file.size > 8192) throw Error('Choose your downloaded access receipt under 8 KiB.');
    const data = JSON.parse(await file.text());
    if (!/^[a-f0-9-]{36}$/.test(data.id) || !/^[a-f0-9]{64}$/.test(data.ownerToken) || !/^[a-f0-9]{64}$/.test(data.shareToken)) throw Error('Invalid access receipt.');
    const fight = await api('/api/fights/' + data.id, {headers: {Authorization: 'Bearer ' + data.ownerToken}});
    receipt = {id: data.id, ownerToken: data.ownerToken, shareToken: data.shareToken, visibility: fight.visibility, verification: fight.verification};
    showReceipt(); $('restore-status').textContent = 'Access restored.';
  } catch (error) { $('restore-status').textContent = error.message; }
};
$('save-receipt').onclick = () => {
  const url = URL.createObjectURL(new Blob([JSON.stringify(receipt, null, 2)], {type: 'application/json'}));
  const link = document.createElement('a'); link.href = url; link.download = 'goatmeter-access-' + receipt.id + '.json'; link.click();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
};
$('change-visibility').onclick = async () => {
  try {
    const visibility = $('manage-visibility').value;
    await api('/api/fights/' + receipt.id, {method: 'PATCH', headers: {'Content-Type': 'application/json', Authorization: 'Bearer ' + receipt.ownerToken}, body: JSON.stringify({visibility})});
    receipt.visibility = visibility; showReceipt(); $('manage-status').textContent = 'Visibility saved. Download an updated receipt if you need the new link.';
    await bosses(); await rankings(); await detail();
  } catch (error) { $('manage-status').textContent = error.message; }
};
let deleteArmed = false;
$('delete').onclick = async () => {
  if (!deleteArmed) { deleteArmed = true; $('delete').textContent = 'Confirm permanent deletion'; return; }
  try {
    await api('/api/fights/' + receipt.id, {method: 'DELETE', headers: {Authorization: 'Bearer ' + receipt.ownerToken}});
    $('keys').textContent = 'Fight deleted.'; $('delete').disabled = true; $('change-visibility').disabled = true;
    $('open-fight').removeAttribute('href'); $('detail').hidden = true; await bosses(); await rankings();
  } catch (error) { $('manage-status').textContent = error.message; }
  finally { deleteArmed = false; $('delete').textContent = 'Delete this upload'; }
};
async function detail() {
  const params = new URLSearchParams(location.hash.slice(1)); if (!params.has('fight')) return;
  const section = $('detail'); section.hidden = false; section.replaceChildren();
  try {
    const fight = await api('/api/fights/' + encodeURIComponent(params.get('fight')), {headers: params.has('key') ? {Authorization: 'Bearer ' + params.get('key')} : {}});
    const heading = document.createElement('h2'); heading.textContent = fight.boss; section.append(heading);
    const context = document.createElement('p'); context.textContent = `UNVERIFIED · ${fight.visibility} · ${fight.durationSeconds.toFixed(1)} seconds · ${new Date(fight.startedUtc).toLocaleString()} · Captured damage across all targets; not confirmed boss-only damage or a confirmed kill.`; section.append(context);
    const table = document.createElement('table'), head = table.createTHead().insertRow();
    for (const text of ['PLAYER', 'CLASS', 'DAMAGE', 'DPS', 'SHARE']) { const th = document.createElement('th'); th.textContent = text; head.append(th); }
    const body = table.createTBody(), total = fight.players.reduce((sum, p) => sum + p.damage, 0);
    for (const player of [...fight.players].sort((a, b) => b.damage - a.damage)) {
      const row = body.insertRow(); cell(row, player.name); cell(row, player.className); cell(row, format(player.damage));
      cell(row, format(player.damage / fight.durationSeconds), 'score'); cell(row, (100 * player.damage / total).toFixed(1) + '%');
    }
    const wrap = document.createElement('div'); wrap.className = 'table-scroll'; wrap.append(table); section.append(wrap); section.scrollIntoView({behavior: 'smooth'});
  } catch (error) { section.textContent = error.message; }
}
window.addEventListener('hashchange', detail);
$('manage-visibility').onchange = () => { deleteArmed = false; $('delete').textContent = 'Delete this upload'; };
api('/api/health').then(health => { if (!health.uploadsEnabled) { $('upload-form').querySelector('button').disabled = true; $('upload-status').textContent = 'Uploads are disabled on this test environment.'; } }).catch(() => {});
bosses().catch(() => {}); rankings(); detail();
