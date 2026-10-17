import {estimateReplay} from '../empirical.mjs';
import {compareModes} from '../compare.mjs';
const $=id=>document.getElementById(id);
let lastResult;
const esc=s=>String(s).replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
async function start(){
 const response=await fetch('../synthetic-example.json');if(!response.ok)throw Error('Example unavailable');const config=await response.json();config.apl=[{skill:'Synthetic spender',minResource:2},{skill:'Synthetic generator'}];$('config').value=JSON.stringify(config,null,2);
}
$('run').onclick=()=>{try{
 const config=JSON.parse($('config').value);config.durationMs=Number($('duration').value);config.delayMs=Number($('delay').value);
 const result=compareModes(config);const selected=$('execution').value;const modes=selected==='both'?['macro','apl']:[selected];
 lastResult={...result,displayedModes:modes,scenario:config};
 $('summary').innerHTML=modes.map(mode=>{const r=result[mode];return `<article><p class="eyebrow">${mode==='macro'?'MACRO':'IDEAL APL'}</p><h3>${Object.values(r.counts).reduce((a,b)=>a+b,0)} casts</h3><p>${r.totalSyntheticUnits} fictional impact units · not game damage</p><p>${r.timeline.filter(e=>e.skipped).length} skipped manual taps · ${r.resource} resource remaining</p><p class="muted">${mode==='macro'?'Cycles the configured hotbar slots.':'Checks ordered priorities; not a human model.'}</p></article>`;}).join('');
 $('counts').innerHTML=result.skills.map(r=>`<tr><td>${esc(r.skill)}</td><td>${modes.includes('macro')?r.macroCasts:'—'}</td><td>${modes.includes('apl')?r.aplCasts:'—'}</td></tr>`).join('');
 $('timeline').innerHTML=modes.map(mode=>`<details><summary>${mode==='macro'?'Macro':'Ideal APL'} timeline</summary><ol>${result[mode].timeline.slice(0,100).map(e=>`<li><time>${e.atMs} ms</time> ${esc(e.skill)} · ${esc(e.input)}${e.skipped?' · SKIPPED':''}</li>`).join('')}</ol></details>`).join('');
 $('results').hidden=false;$('status').textContent='Synthetic model completed. No game DPS calculated.';
 }catch(e){lastResult=null;$('results').hidden=true;$('status').textContent='Cannot run: '+e.message;}};
$('download').onclick=()=>{if(!lastResult)return;const url=URL.createObjectURL(new Blob([JSON.stringify(lastResult,null,2)],{type:'application/json'}));const a=document.createElement('a');a.href=url;a.download='GoatSim-synthetic-comparison.json';a.click();setTimeout(()=>URL.revokeObjectURL(url),1000);};
start().catch(e=>{$('status').textContent=e.message;$('run').disabled=true;});

$('empirical-file').onchange=async e=>{
 $('empirical-result').textContent='';
 try{const file=e.target.files[0];if(!file)return;if(file.size>1024*1024)throw Error('Replay limit is 1 MiB');const r=estimateReplay(JSON.parse(await file.text()));const pct=n=>n===null?'Unavailable':(n*100).toFixed(1)+'%';
 $('empirical-result').innerHTML=`<p class="warning"><strong>Observed-schedule estimate only</strong>No new rotation, gear scaling or independent game validation.</p><p>Capture class: ${esc(r.className)} · Observed window: ${r.observedWindowSeconds.toFixed(3)} seconds</p><table><tbody><tr><th>Held-out hit coverage</th><td>${pct(r.coveredHitFraction)}</td></tr><tr><th>Held-out damage coverage</th><td>${pct(r.coveredDamageFraction)}</td></tr><tr><th>Predicted covered-window DPS</th><td>${r.predictedCoveredWindowDps.toFixed(1)}</td></tr><tr><th>Observed covered-window DPS</th><td>${r.observedCoveredWindowDps.toFixed(1)}</td></tr><tr><th>Covered damage total error</th><td>${r.coveredTotalErrorPercent===null?'Unavailable':r.coveredTotalErrorPercent.toFixed(2)+'%'}</td></tr></tbody></table><p class="muted">The DPS figures cover supported hits only. Unmodeled damage is excluded from both; these are not full encounter DPS predictions. The time window comes from observed hits, not a controlled 120-second dummy test.</p>`;
 const hitError=r.coveredHitWeightedAbsoluteErrorPercent;
 $('empirical-result').innerHTML+=`<p><strong>Hit-by-hit absolute error:</strong> ${hitError===null?'Unavailable — regenerate the private bundle':hitError.toFixed(2)+'%'}. Aggregate total error can hide over- and under-predictions cancelling out. This is not game-formula validation.</p>`;
 if(r.excluded?.length)$('empirical-result').innerHTML+=`<h3>Excluded evidence</h3><table><thead><tr><th>Skill</th><th>Reason</th><th>Hits</th></tr></thead><tbody>${r.excluded.map(x=>`<tr><td>${esc(x.skill)}</td><td>${esc(x.reason)}</td><td>${x.hits}</td></tr>`).join('')}</tbody></table>`;
 $('empirical-status').textContent='Local replay analyzed. No file or character data was uploaded.';
 }catch(error){$('empirical-status').textContent='Cannot analyze replay: '+error.message;}
};
