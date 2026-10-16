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
      playerCell(row, entry);
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
  if (!fight || fight.schemaVersion !== 2 || !Array.isArray(fight.players) || !Array.isArray(fight.targets)) throw Error('Choose a GoatMeter v2 export with per-target damage. Re-export older fights from the updated desktop build. Raw validation logs cannot be uploaded here.');
  return fight;
}
$('file').onchange = async () => {
  pendingUpload = null;
  try {
    const fight = await readFight(); $('boss-label').value = fight.boss || '';
    $('file-preview').textContent = `${fight.players.length} named players · ${fight.durationSeconds} seconds · ${format(fight.players.reduce((n, p) => n + (Number(p.damage) || 0), 0))} captured damage. Review your encounter label and visibility before uploading.`;
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
    const title = document.createElement('div'), heading = document.createElement('h2'), badge = document.createElement('span');
    title.className = 'detail-title'; heading.textContent = fight.boss; badge.className = 'badge'; badge.textContent = 'UNVERIFIED CAPTURE'; title.append(heading,badge); section.append(title);
    const context = document.createElement('p'); context.className = 'detail-context';
    context.textContent = `${fight.visibility} · ${fight.durationSeconds.toFixed(1)} seconds · ${new Date(fight.startedUtc).toLocaleString()} · A selected target is not a verified boss identity or confirmed kill. DPS uses the selected target first-to-last captured player damage window.`; section.append(context);
    const pickerLabel = document.createElement('label'), picker = document.createElement('select'); pickerLabel.textContent = 'Inspect captured damage to';
    picker.add(new Option('All targets — archive totals',''));
    for (const target of fight.targets || []) picker.add(new Option(target.name,target.id));
    picker.value = fight.rankingTargetId || ''; pickerLabel.append(picker); section.append(pickerLabel);
    const metricNote = document.createElement('p'); metricNote.className = 'subtle'; section.append(metricNote);
    const summary = document.createElement('div'); summary.className = 'detail-summary'; section.append(summary);
    const table = document.createElement('table'), head = table.createTHead().insertRow();
    for (const text of ['PLAYER', 'CLASS', 'DAMAGE', 'DPS', 'SHARE']) { const th = document.createElement('th'); th.textContent = text; head.append(th); }
    const body = table.createTBody(), wrap = document.createElement('div'); wrap.className = 'table-scroll'; wrap.append(table); section.append(wrap);
    function renderTarget() {
      const target = (fight.targets || []).find(t => t.id === picker.value);
      const players = fight.players.map((p,i)=>({...p,damage:target ? target.damageByPlayer[i] : p.damage}));
      const total = players.reduce((sum,p)=>sum+p.damage,0), duration = target ? target.durationSeconds : fight.durationSeconds;
      metricNote.textContent = target ? `Only damage to ${target.name} · ${target.durationSeconds.toFixed(1)}s observed window. ${target.id === fight.rankingTargetId ? 'This is the target selected for the community leaderboard.' : 'This view does not change the upload’s ranking target.'}` : 'All-target totals are for analysis and do not enter boss rankings. Older v1 logs are excluded from boss rankings.';
      summary.replaceChildren(); detailStat(summary,target ? 'SELECTED TARGET DAMAGE' : 'ALL-TARGET DAMAGE',format(total)); detailStat(summary,target ? 'TARGET DPS' : 'ENCOUNTER DPS',format(total/duration)); detailStat(summary,'CAPTURED PLAYERS',String(players.length));
      body.replaceChildren();
      for (const player of players.sort((a,b)=>b.damage-a.damage)) {
        const row = body.insertRow(); playerCell(row,player); cell(row,player.className); cell(row,format(player.damage)); cell(row,format(player.damage/duration),'score');
        const value = total > 0 ? 100*player.damage/total : 0, share = cell(row,value.toFixed(1)+'%'), progress = document.createElement('progress');
        progress.max=100; progress.value=value; progress.setAttribute('aria-label',player.name+' captured damage share'); share.append(progress);
      }
    }
    picker.onchange=renderTarget; renderTarget(); section.scrollIntoView({behavior:'smooth'});
  } catch (error) { section.textContent=error.message; }
}
window.addEventListener('hashchange', detail);
$('manage-visibility').onchange = () => { deleteArmed = false; $('delete').textContent = 'Delete this upload'; };
api('/api/health').then(health => { if (!health.uploadsEnabled) { $('upload-form').querySelector('button').disabled = true; $('upload-status').textContent = 'Uploads are disabled on this test environment.'; } }).catch(() => {});
bosses().catch(() => {}); rankings(); detail();
const classCodes = {Gladiator:'GL',Templar:'TE',Assassin:'AS',Ranger:'RA',Sorcerer:'SO',Spiritmaster:'SP',Cleric:'CL',Chanter:'CH',Unknown:'?'};
function playerCell(row, player) {
  const td = cell(row, ''), mark = document.createElement('span'), info = document.createElement('span'), label = document.createElement('small');
  const className = Object.hasOwn(classCodes, player.className) ? player.className : 'Unknown';
  mark.className = 'class-mark class-' + className.toLowerCase(); mark.textContent = classCodes[className]; mark.setAttribute('aria-hidden','true');
  info.className = 'player-info'; info.textContent = player.name; label.textContent = className + (player.serverId ? ' · Server ' + player.serverId : ' · Server unknown'); info.append(label); td.append(mark,info);
  return td;
}
function detailStat(container, title, value) {
  const card = document.createElement('div'), label = document.createElement('small'), number = document.createElement('strong');
  card.className = 'detail-stat'; label.textContent = title; number.textContent = value; card.append(label,number); container.append(card);
}

$('ranking-target').onchange = async () => {
  try { const fight=await readFight(), target=fight.targets.find(t=>t.id===$('ranking-target').value); if(target)$('boss-label').value=target.name; }
  catch(error){$('file-preview').textContent=error.message;}
};
