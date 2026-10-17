import {test} from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {DELAYS,effectivePreset,eligibility,visualSlots,highestReady,modelCycle,copyPlan,validateCatalog} from '../../website/builds/model.mjs';
const catalog=JSON.parse(readFileSync('website/builds/presets.json'));
const build=id=>catalog.classes.find(b=>b.id===id);
test('eight classes include required comparison modes and cannot claim unearned evidence',()=>{
 assert.equal(validateCatalog(catalog),true);assert.deepEqual(catalog.classes.map(b=>b.id).sort(),['templar','gladiator','assassin','ranger','sorcerer','spiritmaster','cleric','chanter'].sort());
 for(const b of catalog.classes){for(const id of ['one-button','boss-manual','solo','group','pvp','leveling'])assert.ok(b.modes.some(m=>m.id===id),b.name+id);for(const m of b.modes){assert.equal(m.evidence.mechanicallyVerified,false);assert.equal(m.evidence.logTested,false);assert.equal(m.evidence.benchmarkValidated,false);}}
 const forged=structuredClone(catalog);forged.classes[0].modes[0].evidence.benchmarkValidated=true;assert.throws(()=>validateCatalog(forged),/evidence/);
});
test('Templar candidate has exact bottom-first stack, separate sustained line, manual charged Punishment',()=>{
 const b=build('templar'),one=effectivePreset(b,'one-button'),boss=effectivePreset(b,'boss-manual');
 assert.deepEqual(one.lines[0].priority,['Judgment','Shield Smite','Warding Strike','Doom Shield']);assert.deepEqual(one.lines[1].priority,['Annihilate','Debilitating Smash','Pummel']);assert.deepEqual(one.steps,['Q','E']);
 assert.equal(visualSlots(one.lines[0])[0].skill,'Doom Shield');assert.equal(visualSlots(one.lines[0])[3].skill,'Judgment');
 assert.ok(one.manual.includes('Punishment charge'));assert.ok(boss.manual.includes('Doom Shield'));assert.ok(boss.lines.every(l=>!l.priority.includes('Doom Shield')));
 assert.match(copyPlan(b,one),/Row 3: Doom Shield/);assert.ok(eligibility(one,{characterLevel:45,skillTier:8}));
});
test('charge exceptions remove the skill from every automatic line without mutating the preset',()=>{
 for(const [id,skill] of [['templar','Doom Shield'],['spiritmaster','Elemental Fusion']]){
  const b=build(id),before=JSON.stringify(b),m=effectivePreset(b,'one-button',{charged:true});assert.ok(m.lines.every(l=>!l.priority.includes(skill)));assert.ok(m.manual.some(s=>s.includes(skill)));assert.equal(JSON.stringify(b),before);
 }
});
test('copy enforces delay, character/skill tiers and unresolved configuration gates',()=>{
 assert.deepEqual(DELAYS,[10,20]);const b=build('templar'),m=effectivePreset(b,'one-button');
 for(const delay of [10,20])assert.match(copyPlan(b,m,{delay}),new RegExp(`delay ${delay} ms`));
 for(const delay of [0,40,50,100])assert.throws(()=>copyPlan(b,m,{delay}));
 for(const options of [{characterLevel:20},{characterLevel:46},{skillTier:8},{skillTier:999}])assert.throws(()=>copyPlan(b,m,options));
 for(const id of ['assassin','ranger','sorcerer'])assert.throws(()=>copyPlan(build(id),effectivePreset(build(id),'pvp')),/unresolved/);
});
test('abstract priority contract skips unavailable and cooling-down skills and follows line order',()=>{
 const m=effectivePreset(build('templar'),'one-button');
 assert.equal(highestReady(m.lines[0],{disabled:['Judgment']}),'Shield Smite');
 assert.equal(highestReady(m.lines[0],{now:5,cooldowns:{Judgment:10, 'Shield Smite':10,'Warding Strike':10}}),'Doom Shield');
 assert.equal(highestReady(m.lines[0],{disabled:m.lines[0].priority}),null);
 const cycle=modelCycle(m,{delay:20,disabled:['Judgment']});assert.deepEqual(cycle.map(x=>[x.key,x.at,x.skill]),[['Q',0,'Shield Smite'],['E',20,'Annihilate']]);
 assert.throws(()=>modelCycle(m,{delay:50}));
});
test('Sorcerer requested sequence is isolated as research; Hellfire never auto-charges',()=>{
 const b=build('sorcerer'),m=effectivePreset(b,'published-sequence');
 assert.deepEqual(m.lines.flatMap(l=>l.priority),['Element Enhancement','Delayed Explosion','Winter’s Shackles','Fire Wall','Cold Storm','Bittercold Wind','Firestorm','Flame Arrow']);
 assert.equal(m.copyEnabled,false);assert.ok(m.manual.includes('Hellfire charge'));
 for(const m of b.modes)assert.ok(m.lines.every(l=>!l.priority.some(s=>/Hellfire/.test(s))));
});
test('Cleric has separate damage/healing/composition modes; unresolved Chanter swap cannot copy',()=>{
 const b=build('cleric');assert.ok(b.modes.find(m=>m.id==='solo'));assert.ok(b.modes.find(m=>m.id==='healing'));const m=effectivePreset(b,'chanter-present');assert.equal(m.copyEnabled,false);assert.ok(!m.stigmas.includes('Light of Protection'));
});
test('all copy-enabled plans agree with their selected preview rows and never auto-cast manual charge skills',()=>{
 for(const b of catalog.classes)for(const m of b.modes){
  if(!m.copyEnabled)continue;const plan=copyPlan(b,m);
  for(const l of m.lines)for(const [row,skill] of l.priority.entries())assert.ok(plan.includes(`Row ${row}: ${skill}`));
  for(const l of m.lines)assert.ok(l.priority.every(s=>!['Punishment','Hellfire','Deadshot','Bolt'].includes(s)));
 }
});
test('source-comparison variants retain basic attacks and keep manual/automatic choices distinct',()=>{
 const assassin=effectivePreset(build('assassin'),'one-button');assert.ok(assassin.lines.some(l=>l.priority.includes('Quick Slice')));assert.equal(assassin.steps.at(-1),'R');
 const glad=effectivePreset(build('gladiator'),'community-single-line');assert.deepEqual(glad.lines[0].priority,['Rage Burst','Overhead Slam','Sword Aura Rampage','Rending Blow']);assert.ok(!glad.manual.includes('Rage Burst'));
 const sorc=effectivePreset(build('sorcerer'),'community-wish-line');assert.equal(sorc.lines[0].priority[3],'Wish of Concentration');assert.ok(!sorc.manual.includes('Wish of Concentration'));assert.ok(sorc.manual.includes('Hellfire charge'));
 for(const b of catalog.classes)assert.ok(b.researchComparison);
});
