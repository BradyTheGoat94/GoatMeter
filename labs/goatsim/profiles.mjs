/** Import contract only: does not infer damage formulas or fetch an armory. */
export const CLASSES=Object.freeze(['templar','gladiator','assassin','ranger','sorcerer','spiritmaster','cleric','chanter']);
const object=v=>v!==null&&typeof v==='object'&&!Array.isArray(v);
function finiteMap(value,label){
 if(!object(value))throw new Error(`${label} must be a map`);
 for(const [key,n] of Object.entries(value))if(!key||!(n===null||(typeof n==='number'&&Number.isFinite(n)&&n>=0)))throw new Error(`${label}: use nonnegative numbers or explicit null`);
}
export function validateProfile(profile){
 if(!object(profile)||profile.schemaVersion!==1)throw new Error('Unsupported profile schema');
 const {character,target,source}=profile;
 if(!object(character)||!CLASSES.includes(character.classId)||character.region!=='GLOBAL'||!Number.isInteger(character.level)||character.level<1||character.level>45)throw new Error('Invalid Global character');
 if(!object(source)||typeof source.kind!=='string'||!['manual','authorized-import'].includes(source.kind)||typeof source.clientPatch!=='string'||!source.clientPatch.trim()||typeof source.capturedAt!=='string'||!Number.isFinite(Date.parse(source.capturedAt)))throw new Error('Profile provenance required');
 finiteMap(character.stats,'Character stats');
 if(!Array.isArray(character.equipment)||!Array.isArray(character.stigmas)||character.stigmas.length>4||!Array.isArray(character.skills)||!object(character.specializations))throw new Error('Invalid loadout');
 for(const list of [character.skills,character.stigmas]){
  const ids=new Set();
  for(const s of list){if(!object(s)||typeof s.id!=='string'||!s.id.trim()||ids.has(s.id)||!(s.level===null||(Number.isInteger(s.level)&&s.level>=0)))throw new Error('Invalid or duplicate skill');ids.add(s.id);}
 }
 for(const [id,choices] of Object.entries(character.specializations))if(!id||!(choices===null||(Array.isArray(choices)&&choices.every(c=>typeof c==='string'&&c.trim()))))throw new Error('Invalid specialties');
 if(!object(target)||target.kind!=='stationary-dummy'||target.count!==1||target.uptime!==1||target.movement!==false)throw new Error('First benchmark requires one stationary full-uptime dummy');
 finiteMap(target.stats,'Target stats');
 if(typeof target.level!=='number'&&! (target.level===null))throw new Error('Unknown target level must be null');
 if(target.level!==null&&(!Number.isInteger(target.level)||target.level<1))throw new Error('Invalid target level');
 return true;
}
/** Required fields belong to a future validated mechanics model; never silently default to zero. */
export function profileReadiness(profile,requirements){
 validateProfile(profile);
 if(!object(requirements)||!Array.isArray(requirements.characterStats)||!Array.isArray(requirements.targetStats)||!Array.isArray(requirements.specializations))throw new Error('Explicit mechanics requirements required');
 const missing=[];
 for(const key of requirements.characterStats)if(profile.character.stats[key]==null)missing.push(`character.stats.${key}`);
 for(const key of requirements.targetStats)if(profile.target.stats[key]==null)missing.push(`target.stats.${key}`);
 for(const key of requirements.specializations)if(profile.character.specializations[key]==null)missing.push(`character.specializations.${key}`);
 return {inputsComplete:missing.length===0,missing,gameDpsValidated:false};
}
