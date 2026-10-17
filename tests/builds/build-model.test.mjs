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
test('Cleric composition candidate requires the equipped Absolution specialty level',()=>{
 const b=build('cleric');assert.ok(b.modes.find(m=>m.id==='solo'));assert.ok(b.modes.find(m=>m.id==='healing'));const m=effectivePreset(b,'chanter-present');assert.equal(m.copyEnabled,true);assert.ok(!m.stigmas.includes('Light of Protection'));assert.ok(m.stigmas.includes('Absolution'));assert.throws(()=>copyPlan(b,m),/Absolution/);assert.ok(copyPlan(b,m,{stigmaLevels:{Absolution:15}}));for(const level of [14,NaN,99,15.5])assert.throws(()=>copyPlan(b,m,{stigmaLevels:{Absolution:level}}));
});
test('all copy-enabled plans agree with their selected preview rows and never auto-cast manual charge skills',()=>{
 for(const b of catalog.classes)for(const m of b.modes){
  if(!m.copyEnabled)continue;const plan=copyPlan(b,m,{stigmaLevels:m.stigmaRequirements??{},skillLevels:m.skillRequirements??{}});
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
test('Sorcerer creator correction keeps Flame Arrow separate and gates the Wish specialty',()=>{
 const b=build('sorcerer'),m=effectivePreset(b,'boss-manual');assert.equal(m.basicAttack.binding,'LMB');assert.equal(m.basicAttack.skill,'Flame Arrow');assert.match(copyPlan(b,m),/Basic attack: Flame Arrow on LMB/);assert.ok(m.lines.every(l=>!l.priority.includes('Flame Arrow')));
 const wish=effectivePreset(b,'community-wish-line');assert.ok(eligibility(wish,{characterLevel:45,skillTier:12}));assert.equal(eligibility(wish,{characterLevel:45,skillTier:16,skillLevels:{'Wish of Concentration':16}}),'');
});
test('support-class comparisons preserve deliberate order and manual emergency buttons',()=>{
 const spirit=effectivePreset(build('spiritmaster'),'one-button');assert.deepEqual(spirit.steps,['E','Q']);assert.equal(spirit.basicAttack.skill,'Cold Shock');assert.ok(spirit.lines.every(l=>!l.priority.some(s=>/Ancient Spirit|Benediction|Flame Blessing/.test(s))));
 const chanter=effectivePreset(build('chanter'),'community-trigger-line');assert.equal(chanter.minSkillTier,16);assert.deepEqual(chanter.lines[0].priority,['Dark Crush','Spinning Strike','Impactful Crush','Incandescent Blow']);assert.ok(chanter.manual.includes('Marchutan’s Wrath'));assert.ok(chanter.lines.every(l=>!l.priority.some(s=>/Mantra|Recuperation|Wave Blow/.test(s))));
 const cleric=effectivePreset(build('cleric'),'community-lucia');assert.equal(cleric.lines[0].priority[0],'Prayer of Amplification');assert.equal(cleric.lines[1].priority[0],'Divine Aura');assert.ok(cleric.manual.includes('Healing and cleanse'));
});
test('Assassin and Ranger comparisons preserve distinct basic inputs and four stigma slots',()=>{
 const assassin=effectivePreset(build('assassin'),'community-separate-basic');assert.deepEqual(assassin.steps,['Q']);assert.equal(assassin.basicAttack.skill,'Quick Slice');assert.ok(assassin.lines.every(l=>!l.priority.includes('Quick Slice')));
 const ranger=effectivePreset(build('ranger'),'community-explosive');assert.deepEqual(ranger.steps,['Q','T']);assert.equal(ranger.stigmas.length,4);assert.ok(ranger.stigmas.includes('Explosive Arrow'));assert.ok(!ranger.stigmas.includes('Griffon Arrow'));assert.deepEqual(ranger.lines[1].priority,['Drill Dart','Burst Arrow','Tempest Shot']);assert.ok(ranger.lines.every(l=>!l.priority.includes('Deadshot')));
});
test('copy instructions distinguish a single held macro key from its internal hotbar steps',()=>{
 const b=build('templar'),plan=copyPlan(b,effectivePreset(b,'one-button'));assert.match(plan,/ONE native macro button/);assert.match(plan,/not separate buttons to hold/);assert.match(plan,/Macro steps: Q → E/);
});
test('Gladiator split setup prioritizes its enabler and requires actual equipped core levels',()=>{
 const b=build('gladiator'),m=effectivePreset(b,'one-button');assert.deepEqual(m.lines[0].priority,['Rage Burst','Overhead Slam']);assert.equal(highestReady(m.lines[0]),'Rage Burst');assert.equal(highestReady(m.lines[0],{disabled:['Rage Burst']}),'Overhead Slam');assert.ok(!m.manual.includes('Rage Burst'));
 assert.throws(()=>copyPlan(b,m),/equipped Overhead Slam/);assert.throws(()=>copyPlan(b,m,{skillLevels:{'Overhead Slam':16,'Rending Blow':15}}),/Rending Blow/);assert.ok(copyPlan(b,m,{skillLevels:{'Overhead Slam':16,'Rending Blow':16}}));
 const early=effectivePreset(b,'community-single-line');assert.ok(early.manual.includes('Ruinous Blow'));assert.equal(early.basicAttack.skill,'Keen Strike');
 const boss=effectivePreset(b,'boss-manual');assert.ok(boss.manual.includes('Rage Burst'));assert.ok(boss.manual.includes('Ruinous Blow'));
});
test('Templar separate-Pummel candidate copies three stack inputs instead of individual skill steps',()=>{
 const b=build('templar'),m=effectivePreset(b,'separate-pummel');assert.deepEqual(m.steps,['Q','E','R']);assert.deepEqual(m.lines[1].priority,['Annihilate','Debilitating Smash']);assert.deepEqual(m.lines[2].priority,['Pummel']);assert.equal(m.lines[0].priority[3],'Doom Shield');assert.ok(m.manual.includes('Punishment charge'));assert.match(copyPlan(b,m),/Macro steps: Q → E → R/);assert.equal(m.evidence.communitySupported,false);
 assert.deepEqual(effectivePreset(b,'one-button').steps,['Q','E']);
});
test('input instructions distinguish the basic attack from the macro and retain Gladiator progression exception',()=>{
 for(const b of catalog.classes){assert.equal(b.inputGuide.basicBinding,'LMB');assert.equal(b.inputGuide.macroBinding,'RMB');assert.match(b.inputGuide.instructions,/Hold LMB and RMB together/);}
 const b=build('gladiator'),m=effectivePreset(b,'one-button');assert.match(copyPlan(b,m,{skillLevels:m.skillRequirements}),/LMB = Keen Strike; RMB = native macro/);assert.match(b.inputGuide.exception,/Sources disagree/);assert.equal(effectivePreset(b,'pvp').copyEnabled,false);
});
test('remaining classes expose deliberate actions and named-skill prerequisites',()=>{
 assert.equal(build('assassin').defaultMode,'community-separate-basic');
 const ranger=effectivePreset(build('ranger'),'boss-manual');assert.ok(ranger.manual.includes('Supporting Fire'));assert.ok(ranger.manual.some(s=>/Marking Shot/.test(s)));assert.ok(ranger.manual.some(s=>/Deadshot/.test(s)));
 const sorc=effectivePreset(build('sorcerer'),'boss-manual');for(const skill of ['Fire Wall','Cold Storm','Hellfire charge'])assert.ok(sorc.manual.includes(skill));
 const wish=effectivePreset(build('sorcerer'),'community-wish-line');assert.throws(()=>copyPlan(build('sorcerer'),wish),/equipped Wish/);
 const cleric=effectivePreset(build('cleric'),'boss-manual');assert.deepEqual(cleric.lines[1].priority,['Divine Aura','Judgment Thunder']);assert.ok(cleric.lines.every(l=>!l.priority.includes('Lightning Strike Scattershot')));
 const chanter=effectivePreset(build('chanter'),'community-trigger-line');assert.throws(()=>copyPlan(build('chanter'),chanter),/equipped Dark Crush/);
 for(const id of ['assassin','ranger','sorcerer','spiritmaster','cleric','chanter'])assert.equal(build(id).mechanics.length,3);
});
