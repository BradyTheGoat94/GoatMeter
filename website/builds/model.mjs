export const DELAYS=Object.freeze([10,20]);
export const STATUS=[['communitySupported','Community-supported'],['mechanicallyVerified','Mechanically verified'],['logTested','Log-tested'],['benchmarkValidated','Benchmark-validated']];
export function effectivePreset(build,modeId,{charged=false}={}){
 const source=build.modes.find(m=>m.id===modeId);if(!source)throw new Error('Unknown build mode');
 const mode=structuredClone(source);
 const chargeSkill=build.id==='spiritmaster'?'Elemental Fusion':build.id==='templar'?'Doom Shield':null;
 if(charged&&chargeSkill){for(const line of mode.lines)line.priority=line.priority.filter(s=>s===chargeSkill?false:true);mode.manual=[...new Set([...mode.manual,chargeSkill+' (manual charge)'])];}
 return mode;
}
export function eligibility(mode,{characterLevel=45,skillTier=16}={}){
 if(!Number.isInteger(characterLevel)||characterLevel<1||characterLevel>45)return 'Choose a character level from 1 to 45.';
 if(![8,12,16].includes(Number(skillTier)))return 'Choose the equipped skill tier.';
 if(characterLevel<mode.minCharacterLevel)return `This candidate is scoped to character level ${mode.minCharacterLevel}; use the leveling worksheet.`;
 if(skillTier<mode.minSkillTier)return `Requires the relevant skill at ${mode.minSkillTier} and its selected specialty. Character level alone is insufficient.`;
 if(!mode.copyEnabled)return 'Research worksheet: exact compatibility or configuration is unresolved; copying is disabled.';
 return '';
}
export function visualSlots(line){return [3,2,1,0].map(row=>({row,skill:line.priority[row]??null}));}
export function highestReady(line,{now=0,cooldowns={},disabled=[]}={}){
 if(!Number.isFinite(now)||now<0)throw new RangeError('Invalid model time');
 return line.priority.find(skill=>!disabled.includes(skill)&&(cooldowns[skill]??0)<=now)??null;
}
export function modelCycle(mode,{delay=10,now=0,cooldowns={},disabled=[]}={}){
 if(!DELAYS.includes(Number(delay)))throw new RangeError('Use 10 or 20 ms.');
 return mode.steps.map((key,i)=>{const line=mode.lines.find(l=>l.key===key);if(!line)throw new Error('Macro references a missing line');const at=now+i*Number(delay);return {key,at,skill:highestReady(line,{now:at,cooldowns,disabled})};});
}
export function copyPlan(build,mode,options={}){
 const {delay=10}=options;if(!DELAYS.includes(Number(delay)))throw new RangeError('Use 10 or 20 ms.');
 const reason=eligibility(mode,options);if(reason)throw new Error(reason);
 return [`GoatMeter Build Lab · ${build.name} · ${mode.label} · TEST CANDIDATE`,
 `Global Season 1 source scope; exact client build and execution unverified. Character level ${options.characterLevel??45}; equipped skill tier ${options.skillTier??16}.`,
 'Native macro: bind one held key; add the following hotbar lines in order. Row 0 = bottom / highest priority.',
 ...mode.lines.map(l=>`${l.key}: ${l.priority.map((s,i)=>`Row ${i}: ${s}`).join('; ')}`),
 `Macro steps: ${mode.steps.join(' → ')}; delay ${delay} ms.`,
 `Keep manual: ${mode.manual.join(' · ')}`,
 ...mode.notes,
 'Skill Queue ON and OFF both require in-game testing. This plan is not an import file or proven best-DPS macro.'
 ].join('\n');
}
export function validateCatalog(catalog){
 if(catalog.classes.length!==8)throw new Error('Eight Global classes required');
 for(const b of catalog.classes){
  if(!b.compatibility.clientBuild||!b.specializations.length)throw new Error('Compatibility and specialties required');
  for(const m of b.modes){
   if(m.stigmas.length!==4||m.lines.some(l=>l.priority.length>4)||m.steps.length>20)throw new Error('Invalid native configuration bounds');
   if(new Set(m.lines.map(l=>l.key)).size!==m.lines.length)throw new Error('Duplicate line binding');
   if(m.steps.some(k=>!m.lines.some(l=>l.key===k)))throw new Error('Missing line');
   if(m.evidence.benchmarkValidated&&!m.benchmarkEvidence)throw new Error('Benchmark claim needs evidence');
   if(m.evidence.mechanicallyVerified&&!m.clientTestEvidence)throw new Error('Mechanical claim needs client evidence');
   if(m.evidence.logTested&&!m.logEvidence)throw new Error('Log-tested claim needs evidence');
  }
 }
 return true;
}
